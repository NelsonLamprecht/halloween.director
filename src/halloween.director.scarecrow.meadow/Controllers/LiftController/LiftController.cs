using System;
using System.Threading;
using System.Threading.Tasks;

using Meadow.Logging;

using Halloween.Director.Scarecrow.Meadow.Controllers.RelayController;
using Halloween.Director.Shared.Controllers;

namespace Halloween.Director.Scarecrow.Meadow.Controllers.LiftController
{
    public enum LiftState
    {
        Down,
        Rising,
        Up,
        Falling
    }

    /// <summary>
    /// Drives the scissor lift's single cylinder relay. There are no limit switches,
    /// so rising/falling are timed: the lift is treated as moving for RiseMs/FallMs
    /// after the relay switches. A command that would reverse the lift mid-travel is
    /// rejected, and the lift lowers itself if it is left up longer than MaxUpMs.
    /// </summary>
    public class LiftController : BaseController
    {
        // how long the lift takes to travel; commands that reverse it are rejected meanwhile
        public const int RiseMs = 3000;
        public const int FallMs = 3000;

        // never leave the cylinder energized indefinitely
        public const int MaxUpMs = 30000;

        private readonly RelayController.RelayController relayController;

        // Maple runs requests in parallel, so /up and /down can race; every read or
        // change of _state (and the relay switch that goes with it) happens under this lock
        private readonly object _sync = new object();
        private LiftState _state = LiftState.Down;
        private CancellationTokenSource _autoLower;

        public LiftController(Logger logger, RelayController.RelayController relayController) : base(logger)
        {
            this.relayController = relayController;
        }

        public LiftState State
        {
            get
            {
                lock (_sync)
                {
                    return _state;
                }
            }
        }

        // Completes once the lift is up. Returns false immediately (without moving)
        // if the lift is falling. Already rising or up returns true straight away;
        // when up, it also restarts the auto-lower countdown.
        public async Task<bool> TryRaiseAsync()
        {
            lock (_sync)
            {
                switch (_state)
                {
                    case LiftState.Falling:
                        Logger.Warn("Lift is falling; ignoring up request.");
                        return false;
                    case LiftState.Rising:
                        Logger.Info("Lift is already rising.");
                        return true;
                    case LiftState.Up:
                        Logger.Info("Lift is already up; restarting auto-lower countdown.");
                        StartAutoLower();
                        return true;
                }

                _state = LiftState.Rising;
                relayController.TurnOn();
            }

            Logger.Info("Lift rising.");
            await Task.Delay(RiseMs);

            lock (_sync)
            {
                _state = LiftState.Up;
                StartAutoLower();
            }
            Logger.Info("Lift up.");
            return true;
        }

        // Completes once the lift is down. Returns false immediately (without moving)
        // if the lift is rising. Already falling or down returns true straight away.
        public async Task<bool> TryLowerAsync()
        {
            lock (_sync)
            {
                switch (_state)
                {
                    case LiftState.Rising:
                        Logger.Warn("Lift is rising; ignoring down request.");
                        return false;
                    case LiftState.Falling:
                        Logger.Info("Lift is already falling.");
                        return true;
                    case LiftState.Down:
                        Logger.Info("Lift is already down.");
                        return true;
                }

                BeginFall();
            }

            await FallAsync();
            return true;
        }

        // caller holds _sync and has checked the lift is up
        private void BeginFall()
        {
            CancelAutoLower();
            _state = LiftState.Falling;
            relayController.TurnOff();
        }

        private async Task FallAsync()
        {
            Logger.Info("Lift falling.");
            await Task.Delay(FallMs);

            lock (_sync)
            {
                _state = LiftState.Down;
            }
            Logger.Info("Lift down.");
        }

        // caller holds _sync
        private void StartAutoLower()
        {
            CancelAutoLower();
            _autoLower = new CancellationTokenSource();
            _ = AutoLowerAsync(_autoLower.Token);
        }

        // caller holds _sync
        private void CancelAutoLower()
        {
            if (_autoLower == null)
            {
                return;
            }
            _autoLower.Cancel();
            _autoLower.Dispose();
            _autoLower = null;
        }

        private async Task AutoLowerAsync(CancellationToken token)
        {
            try
            {
                await Task.Delay(MaxUpMs, token);

                lock (_sync)
                {
                    // a /down or a restarted countdown may have got here first
                    if (token.IsCancellationRequested || _state != LiftState.Up)
                    {
                        return;
                    }
                    Logger.Warn($"Lift has been up for {MaxUpMs}ms; lowering automatically.");
                    BeginFall();
                }

                await FallAsync();
            }
            catch (OperationCanceledException)
            {
                // countdown restarted or the lift was lowered on request
            }
            catch (Exception ex)
            {
                Logger.Error($"Auto-lower failed: {ex.Message}");
            }
        }
    }
}
