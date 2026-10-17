using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;

using Meadow;
using Meadow.Devices;
using Meadow.Foundation.Leds;
using Meadow.Logging;

namespace halloween.director.monsterbox.meadow.Controllers
{
    internal class LedController : BaseController, IDisposable
    {
        private readonly IMeadowDevice device;

        RgbPwmLed onBoardRGBLed;

        Task animationTask = null;
        CancellationTokenSource cancellationTokenSource = null;

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

            if (device is F7FeatherV1 f7Device)
            {
                onBoardRGBLed = new RgbPwmLed(
                    redPwmPin: f7Device.Pins.OnboardLedRed,
                    greenPwmPin: f7Device.Pins.OnboardLedGreen,
                    bluePwmPin: f7Device.Pins.OnboardLedBlue);
                onBoardRGBLed.SetColor(Color.Red);
            }

            initialized = true;

            Logger.Info($"{GetType().Name} is initialized.");
        }

        // onBoardRGBLed stays null if the device isn't an F7FeatherV1, so every
        // public method is a no-op in that case rather than a NullReferenceException
        void Stop()
        {
            cancellationTokenSource?.Cancel();

            // our own animation task (pulse / running colors) must also be done writing
            try
            {
                animationTask?.Wait(500);
            }
            catch (Exception)
            {
                // cancelled or faulted; either way it's finished
            }
            animationTask = null;

            // StopAnimation() only completes once the animation thread has exited, so wait
            // for it: otherwise a final brightness write from that thread can land after the
            // caller's next SetColor. Meadow has no synchronization context and the animation
            // runs on its own thread, so blocking here can't deadlock (it takes <= ~60ms).
            try
            {
                onBoardRGBLed?.StopAnimation().GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                // the animation was cancelled before it began; nothing left to stop
            }
            catch (Exception ex)
            {
                Logger.Warn($"Stopping LED animation failed: {ex.Message}");
            }
        }

        public void SetColor(Color color)
        {
            if (onBoardRGBLed == null)
            {
                return;
            }

            Stop();
            onBoardRGBLed.SetColor(color);
        }

        public void TurnOn()
        {
            if (onBoardRGBLed == null)
            {
                return;
            }

            Stop();
            onBoardRGBLed.SetColor(GetRandomColor());
            onBoardRGBLed.IsOn = true;
        }

        public void TurnOff()
        {
            if (onBoardRGBLed == null)
            {
                return;
            }

            Stop();
            onBoardRGBLed.IsOn = false;
        }

        public void StartBlink()
        {
            if (onBoardRGBLed == null)
            {
                return;
            }

            Stop();
            onBoardRGBLed.StartBlink(GetRandomColor());
        }

        public void StartPulse()
        {
            if (onBoardRGBLed == null)
            {
                return;
            }

            // RgbPwmLed.StartPulse(Color, ...) showed white on the board, so the fade is
            // done here by scaling the brightness of a fixed-hue color with SetColor
            Stop();
            cancellationTokenSource = new CancellationTokenSource();
            var token = cancellationTokenSource.Token;
            var hue = (float)(_random.NextDouble() * 360);
            animationTask = Task.Run(() => PulseLoop(hue, token));
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
                    onBoardRGBLed.SetColor(Color.FromHsba(hue, 1, brightness));
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
            if (onBoardRGBLed == null)
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
            if (onBoardRGBLed == null)
            {
                return;
            }

            // cancels any previous animation; the new token exists before the task
            // starts, so an immediate Stop() can't miss it
            Stop();
            cancellationTokenSource = new CancellationTokenSource();
            var token = cancellationTokenSource.Token;
            animationTask = Task.Run(() => StartRunningColors(token));
        }

        protected async Task StartRunningColors(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    onBoardRGBLed.SetColor(GetRandomColor());
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
            cancellationTokenSource?.Cancel();
            onBoardRGBLed?.Dispose();
        }
    }
}
