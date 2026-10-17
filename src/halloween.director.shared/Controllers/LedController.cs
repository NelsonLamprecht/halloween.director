using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using Meadow;
using Meadow.Logging;

namespace Halloween.Director.Shared.Controllers
{
    /// <summary>
    /// Status LED on the F7's onboard RGB LED: red until the app is up, then
    /// <see cref="ReadyColor"/>, with a hard white blink while commands are being handled.
    /// </summary>
    public class LedController : BaseController, IOnOrOffController, IDisposable
    {
        private readonly IMeadowDevice device;

        OnboardRgbLed led;

        bool initialized = false;

        public LedController(Logger logger, IMeadowDevice device) : base(logger)
        {
            this.device = device;
        }

        public void Initialize()
        {
            if (initialized)
            {
                return;
            }

            led = new OnboardRgbLed(Logger, device);
            led.SetColor(Color.Red);

            initialized = true;

            Logger.Info($"{GetType().Name} is initialized.");
        }

        // OnboardRgbLed does nothing on a device without the onboard LED, so every
        // public method is a no-op in that case rather than a NullReferenceException
        private OnboardRgbLed Led => led ?? throw new InvalidOperationException($"{GetType().Name} is not initialized.");

        public void SetColor(Color color)
        {
            Led.SetColor(color);
        }

        public void TurnOn()
        {
            Led.TurnOn(GetRandomColor());
        }

        public void TurnOff()
        {
            Led.TurnOff();
        }

        public void Stop()
        {
            Led.Stop();
        }

        public void StartBlink(Color color)
        {
            Led.StartBlink(color);
        }

        public void StartBlink()
        {
            Led.StartBlink(GetRandomColor());
        }

        public void StartActivityBlink()
        {
            // a hard on/off blink in a fixed color: much easier to notice than a fade
            Led.Animate(BlinkLoop);
        }

        async Task BlinkLoop(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    Led.WriteColor(ActivityColor);
                    await Task.Delay(BlinkOn, cancellationToken);
                    Led.WriteColor(Color.Black);
                    await Task.Delay(BlinkOff, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Stop() was called; nothing to clean up
            }
        }

        // Activity blink tuning: ActivityColor is shown for BlinkOn, then the LED is off for
        // BlinkOff (shorter = faster).
        public static readonly Color ActivityColor = Color.White;
        private static readonly TimeSpan BlinkOn = TimeSpan.FromMilliseconds(120);
        private static readonly TimeSpan BlinkOff = TimeSpan.FromMilliseconds(120);

        // Blinks while at least one command is being handled, then returns to ReadyColor.
        // Counted, because Maple handles requests in parallel: the LED only goes back to
        // ready once the last overlapping request has finished.
        public void BeginActivity()
        {
            if (!Led.IsAvailable)
            {
                return;
            }

            lock (_activityLock)
            {
                _activityVersion++;
                _activeRequests++;

                if (!_blinking)
                {
                    _blinking = true;
                    _blinkClock.Restart();
                    StartActivityBlink();
                }
            }
        }

        public void EndActivity()
        {
            lock (_activityLock)
            {
                if (_activeRequests == 0)
                {
                    return;
                }

                if (--_activeRequests > 0)
                {
                    return;
                }

                var remaining = MinActivityBlink - _blinkClock.Elapsed;
                if (remaining <= TimeSpan.Zero)
                {
                    ReturnToReady();
                    return;
                }

                // finished faster than the blink is noticeable (e.g. /sound only queues
                // the file); hold the blink without delaying the HTTP response. A newer
                // BeginActivity bumps the version, which cancels this return-to-ready.
                var version = _activityVersion;
                _ = Task.Run(async () =>
                {
                    await Task.Delay(remaining);
                    lock (_activityLock)
                    {
                        if (_activeRequests == 0 && version == _activityVersion)
                        {
                            ReturnToReady();
                        }
                    }
                });
            }
        }

        // For commands that complete instantly: a blink of MinActivityBlink, then ready.
        public void SignalActivity()
        {
            BeginActivity();
            EndActivity();
        }

        // call with _activityLock held
        private void ReturnToReady()
        {
            _blinking = false;
            SetColor(ReadyColor);
        }

        // Color shown when the board is idle and ready to accept a command
        public static readonly Color ReadyColor = Color.Green;

        // shortest time the blink stays visible, so a quick command is still noticeable
        private static readonly TimeSpan MinActivityBlink = TimeSpan.FromSeconds(2);

        private readonly object _activityLock = new object();
        private readonly Stopwatch _blinkClock = new Stopwatch();
        private int _activeRequests;
        private int _activityVersion;
        private bool _blinking;

        public void StartRunningColors()
        {
            Led.Animate(StartRunningColors);
        }

        protected async Task StartRunningColors(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    Led.WriteColor(GetRandomColor());
                    await Task.Delay(1000, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Stop() was called; nothing to clean up
            }
        }

        // shared so back-to-back calls don't get the same time-based seed (and color)
        private static readonly Random _random = new Random();

        protected Color GetRandomColor()
        {
            // hue is in degrees (0-360), not 0-1
            return Color.FromHsba((float)(_random.NextDouble() * 360), 1, 1);
        }

        public void Dispose()
        {
            led?.Dispose();
        }
    }
}
