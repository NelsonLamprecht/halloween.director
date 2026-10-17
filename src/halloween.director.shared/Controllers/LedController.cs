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
    /// <see cref="ReadyColor"/>, with a pulse while commands are being handled.
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

        public void StartPulse()
        {
            // RgbPwmLed.StartPulse(Color, ...) showed white on the board, so the fade is
            // done here by scaling the brightness of a fixed-hue color with SetColor
            var hue = (float)(_random.NextDouble() * 360);
            Led.Animate(token => PulseLoop(hue, token));
        }

        async Task PulseLoop(float hue, CancellationToken cancellationToken)
        {
            var clock = Stopwatch.StartNew();
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var phase = (clock.Elapsed.TotalMilliseconds % PulseDuration.TotalMilliseconds)
                        / PulseDuration.TotalMilliseconds;
                    // 0 -> 1 -> 0 over one cycle, starting dark
                    var wave = (float)(0.5 - 0.5 * Math.Cos(2 * Math.PI * phase));
                    var brightness = PulseLowBrightness + (1 - PulseLowBrightness) * wave;
                    Led.WriteColor(Color.FromHsba(hue, 1, brightness));
                    await Task.Delay(40, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Stop() was called; nothing to clean up
            }
        }

        // one full fade up/down of the activity pulse
        private static readonly TimeSpan PulseDuration = TimeSpan.FromSeconds(2);
        private const float PulseLowBrightness = 0.1f;

        // Pulses while at least one command is being handled, then returns to ReadyColor.
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

                if (!_pulsing)
                {
                    _pulsing = true;
                    _pulseClock.Restart();
                    StartPulse();
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

                var remaining = MinActivityPulse - _pulseClock.Elapsed;
                if (remaining <= TimeSpan.Zero)
                {
                    ReturnToReady();
                    return;
                }

                // finished faster than the pulse is noticeable (e.g. /sound only queues
                // the file); hold the pulse without delaying the HTTP response. A newer
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

        // For commands that complete instantly: a pulse of MinActivityPulse, then ready.
        public void SignalActivity()
        {
            BeginActivity();
            EndActivity();
        }

        // call with _activityLock held
        private void ReturnToReady()
        {
            _pulsing = false;
            SetColor(ReadyColor);
        }

        // Color shown when the board is idle and ready to accept a command
        public static readonly Color ReadyColor = Color.Green;

        // shortest time the pulse stays visible (one full pulse cycle)
        private static readonly TimeSpan MinActivityPulse = PulseDuration;

        private readonly object _activityLock = new object();
        private readonly Stopwatch _pulseClock = new Stopwatch();
        private int _activeRequests;
        private int _activityVersion;
        private bool _pulsing;

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
