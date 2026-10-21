using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using Meadow;
using Meadow.Logging;

namespace Halloween.Director.Shared.Controllers
{
    /// <summary>
    /// Status LED on the F7's onboard RGB LED. It shows the boot and network stage
    /// (<see cref="LedStatus"/>, set with <see cref="SetStatus"/>), with a hard white blink
    /// while commands are being handled:
    ///
    ///   Booting          solid yellow (the app has just started)
    ///   Initializing     solid red
    ///   WaitingForWifi   slow blue blink
    ///   WifiRetrying     fast orange blink (connect failed or WiFi dropped)
    ///   StartingServer   solid cyan
    ///   ServerFailed     fast purple blink
    ///   Ready            dark, with a short <see cref="ReadyColor"/> (green) flash each time
    ///                    the heartbeat's health check passes (<see cref="PulseHealthy"/>)
    ///
    /// The LED is off before Initialize(), while Meadow OS itself boots: no app code runs then.
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

            Logger.StartupSection($"{GetType().Name}.Initialize", () =>
            {
                led = new OnboardRgbLed(Logger, device);
                initialized = true;
                ShowStatus(_status);

                Logger.StartupDetail($"Onboard LED available: {led.IsAvailable}. Yellow while booting.");
            });

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
            Logger.StartupDetail("LED: blinking for activity.");
            HardBlink(ActivityColor, BlinkOn, BlinkOff);
        }

        // a hard on/off blink (StartBlink fades)
        private void HardBlink(Color color, TimeSpan on, TimeSpan off)
        {
            Led.Animate(cancellationToken => BlinkLoop(color, on, off, cancellationToken));
        }

        async Task BlinkLoop(Color color, TimeSpan on, TimeSpan off, CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    Led.WriteColor(color);
                    await Task.Delay(on, cancellationToken);
                    Led.WriteColor(Color.Black);
                    await Task.Delay(off, cancellationToken);
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

        // Status blink tuning: slow while waiting, fast when something needs attention.
        private static readonly TimeSpan SlowBlink = TimeSpan.FromMilliseconds(500);
        private static readonly TimeSpan FastBlink = TimeSpan.FromMilliseconds(150);

        private static readonly Color RetryingColor = new Color(255, 64, 0);
        private static readonly Color FailedColor = new Color(160, 0, 255);

        private LedStatus _status = LedStatus.Booting;

        /// <summary>
        /// Shows <paramref name="status"/>. During an activity blink it is only recorded, and
        /// shown once the blink ends.
        /// </summary>
        public void SetStatus(LedStatus status)
        {
            lock (_activityLock)
            {
                SetStatusLocked(status);
            }
        }

        /// <summary>
        /// Shows <paramref name="status"/> only if the LED is still at <paramref name="expected"/>,
        /// so a late "waiting" can't overwrite a stage that has already moved on.
        /// </summary>
        public void SetStatus(LedStatus expected, LedStatus status)
        {
            lock (_activityLock)
            {
                if (_status == expected)
                {
                    SetStatusLocked(status);
                }
            }
        }

        // call with _activityLock held
        private void SetStatusLocked(LedStatus status)
        {
            if (status == _status)
            {
                return;
            }

            _status = status;
            Logger.StartupDetail($"LED: {status}.");

            if (initialized && !_blinking)
            {
                ShowStatus(status);
            }
        }

        private void ShowStatus(LedStatus status)
        {
            switch (status)
            {
                case LedStatus.Booting:
                    SetColor(Color.Yellow);
                    break;
                case LedStatus.Initializing:
                    SetColor(Color.Red);
                    break;
                case LedStatus.WaitingForWifi:
                    HardBlink(Color.Blue, SlowBlink, SlowBlink);
                    break;
                case LedStatus.WifiRetrying:
                    HardBlink(RetryingColor, FastBlink, FastBlink);
                    break;
                case LedStatus.StartingServer:
                    SetColor(Color.Cyan);
                    break;
                case LedStatus.ServerFailed:
                    HardBlink(FailedColor, FastBlink, FastBlink);
                    break;
                case LedStatus.Ready:
                    // one flash now, then one on every passing health check (PulseHealthy)
                    Led.Animate(HealthPulse);
                    break;
            }
        }

        // how long the green health flash stays lit
        private static readonly TimeSpan HealthPulseOn = TimeSpan.FromMilliseconds(150);

        /// <summary>
        /// A short green flash while ready, to show the board is healthy. Called by the heartbeat
        /// each time its health check passes; does nothing in any other status or during an
        /// activity blink.
        /// </summary>
        public void PulseHealthy()
        {
            lock (_activityLock)
            {
                if (!initialized || _status != LedStatus.Ready || _blinking)
                {
                    return;
                }

                Led.Animate(HealthPulse);
            }
        }

        async Task HealthPulse(CancellationToken cancellationToken)
        {
            try
            {
                Led.WriteColor(ReadyColor);
                await Task.Delay(HealthPulseOn, cancellationToken);
                Led.WriteColor(Color.Black);
            }
            catch (OperationCanceledException)
            {
                // a new status or activity blink took over the LED
            }
        }

        // Blinks while at least one command is being handled, then returns to the status
        // (ready, unless WiFi dropped meanwhile).
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
            Logger.StartupDetail($"LED: activity done, back to {_status}.");
            ShowStatus(_status);
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
