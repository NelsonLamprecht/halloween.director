using System;
using System.Threading;
using System.Threading.Tasks;

using Meadow.Logging;

using Halloween.Director.Shared.Controllers;

namespace Halloween.Director.Scarecrow.Meadow.Controllers
{
    /// <summary>
    /// The scissor lift, as wired: relay CLOSED = lift DOWN (the rest state: where it starts at
    /// boot, where it returns on Down() and where the auto-lower puts it), relay OPEN = lift UP.
    /// Up() and Down() sit on top of the relay, with the state tracked so a
    /// repeated command is a no-op, and a time limit (<see cref="LiftOptions.MaxUp"/>) after
    /// which it lowers itself, so the prop isn't left up if the remote loses connection.
    /// </summary>
    public class LiftController : BaseController, IDisposable
    {
        private readonly RelayController relayController;
        private readonly LiftOptions options;
        private readonly object sync = new object();
        private CancellationTokenSource autoLower;
        private bool isUp;

        public LiftController(Logger logger, RelayController relayController, LiftOptions options) : base(logger)
        {
            this.relayController = relayController;
            this.options = options;
        }

        public bool IsUp
        {
            get { lock (sync) { return isUp; } }
        }

        /// <summary>Raises the lift (opens the relay). Returns false if it was already up (the limit timer is not extended).</summary>
        public bool Up()
        {
            lock (sync)
            {
                if (isUp)
                {
                    Logger.Info("Lift is already up.");
                    return false;
                }

                relayController.TurnOn(); // opens the relay
                isUp = true;
                StartAutoLower();
                return true;
            }
        }

        /// <summary>Lowers the lift (closes the relay, its rest state). Returns false if it was already down.</summary>
        public bool Down()
        {
            lock (sync)
            {
                CancelAutoLower();

                if (!isUp)
                {
                    Logger.Info("Lift is already down.");
                    return false;
                }

                relayController.TurnOff(); // closes the relay
                isUp = false;
                return true;
            }
        }

        // call with sync held
        private void StartAutoLower()
        {
            if (options.MaxUp <= TimeSpan.Zero)
            {
                return;
            }

            autoLower = new CancellationTokenSource();
            var token = autoLower.Token;
            Logger.Info($"Lift will lower itself after {options.MaxUp.TotalSeconds}s.");

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(options.MaxUp, token);
                }
                catch (OperationCanceledException)
                {
                    // lowered (or disposed) before the limit
                    return;
                }

                Logger.Warn($"Lift was up for {options.MaxUp.TotalSeconds}s; lowering it.");
                Down();
            });
        }

        // call with sync held
        private void CancelAutoLower()
        {
            autoLower?.Cancel();
            autoLower = null;
        }

        public void Dispose()
        {
            lock (sync)
            {
                CancelAutoLower();
            }
        }
    }
}
