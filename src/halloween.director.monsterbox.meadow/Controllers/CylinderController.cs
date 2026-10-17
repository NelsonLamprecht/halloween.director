using System;
using System.Threading;
using System.Threading.Tasks;

using Meadow.Logging;

using Halloween.Director.Shared.Controllers;

namespace Halloween.Director.Monsterbox.Meadow.Controllers
{
    internal class CylindersController : BaseController, IDisposable
    {
        // cancelled on Dispose() so an in-flight shake stops instead of driving
        // relays that are about to be released
        private readonly CancellationTokenSource _shutdown = new CancellationTokenSource();

        // how long Dispose() waits for a cancelled shake to finish switching the relays off
        private const int ShutdownWaitMs = 500;

        private readonly RelayController relayController;
        private readonly Random _random;

        public CylindersController(Logger logger, RelayController relayController) : base(logger)
        {
            this.relayController = relayController;
            _random = new Random();
        }

        // 0 = idle, 1 = shaking; Maple runs requests in parallel, so two /shake
        // requests can race for this — access it via Interlocked
        private int _isShaking;

        // Runs the whole shake and completes when it's done, so the /shake response
        // tells the caller the shake has finished. Returns false immediately (without
        // shaking) if another shake is already running. Exceptions propagate to the
        // caller after the relays are switched off.
        public async Task<bool> TryShakeAsync(ShakeConfiguration config)
        {
            if (_shutdown.IsCancellationRequested)
            {
                throw new ObjectDisposedException(nameof(CylindersController));
            }

            if (Interlocked.CompareExchange(ref _isShaking, 1, 0) != 0)
            {
                Logger.Warn("Already shaking; ignoring request.");
                return false;
            }

            try
            {
                await ShakeAsync(config);
                return true;
            }
            catch
            {
                // never leave a cylinder stuck on
                Stop();
                throw;
            }
            finally
            {
                Interlocked.Exchange(ref _isShaking, 0);
            }
        }

        private async Task ShakeAsync(ShakeConfiguration config)
        {
            Stop();
            var iterations = config.GetIterations();
            Logger.Info($"Shake. Iterations: {iterations}");

            for (int i = 0; i < iterations; i++)
            {
                await ActionAsync(config);
            }
            Stop();
            Logger.Info("Shake finished.");
        }

        private async Task ActionAsync(ShakeConfiguration config)
        {
            // either turn it on or turn it off
            var turnOn = _random.Next(0, 2) == 1;

            // left or right
            var left = _random.Next(0, 2) == 0;

            if (left)
            {
                if (turnOn)
                {
                    relayController.TurnOnLeft();
                }
                else
                {
                    relayController.TurnOffLeft();
                }
            }
            else
            {
                if (turnOn)
                {
                    relayController.TurnOnRight();
                }
                else
                {
                    relayController.TurnOffRight();
                }
            }

            await Task.Delay(config.GetDelay(), _shutdown.Token);
        }

        private void Stop()
        {
            relayController.TurnOffLeft();
            relayController.TurnOffRight();
        }

        // Cancels any running shake and waits briefly for it to switch the relays off,
        // so the caller can safely dispose RelayController right after.
        public void Dispose()
        {
            _shutdown.Cancel();

            var waited = 0;
            while (Volatile.Read(ref _isShaking) != 0 && waited < ShutdownWaitMs)
            {
                Thread.Sleep(10);
                waited += 10;
            }
        }
    }
}
