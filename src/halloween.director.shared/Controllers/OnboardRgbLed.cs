using System;
using System.Threading;
using System.Threading.Tasks;

using Meadow;
using Meadow.Devices;
using Meadow.Foundation.Leds;
using Meadow.Logging;
using Meadow.Peripherals.Leds;

namespace Halloween.Director.Shared.Controllers
{
    /// <summary>
    /// The F7 Feather's onboard RGB LED. It is a common anode LED (pin low = lit), so the PWM
    /// has to be inverted or the colors come out inverted.
    ///
    /// When the device isn't an F7FeatherV1 there is no LED, so every method is a no-op
    /// instead of a NullReferenceException (see <see cref="IsAvailable"/>).
    /// </summary>
    public class OnboardRgbLed : IOnOrOffController, IDisposable
    {
        private static readonly TimeSpan DefaultBlinkPeriod = TimeSpan.FromMilliseconds(500);

        private readonly Logger logger;
        private readonly RgbPwmLed led;

        private Task animationTask;
        private CancellationTokenSource cancellationTokenSource;

        public OnboardRgbLed(Logger logger, IMeadowDevice device)
        {
            this.logger = logger;

            if (device is F7FeatherV1 f7Device)
            {
                led = new RgbPwmLed(
                    redPwmPin: f7Device.Pins.OnboardLedRed,
                    greenPwmPin: f7Device.Pins.OnboardLedGreen,
                    bluePwmPin: f7Device.Pins.OnboardLedBlue,
                    commonType: CommonType.CommonAnode);
            }
        }

        public bool IsAvailable => led != null;

        /// <summary>Stops any animation, then shows <paramref name="color"/>.</summary>
        public void SetColor(Color color)
        {
            if (led == null)
            {
                return;
            }

            Stop();
            led.SetColor(color);
        }

        /// <summary>
        /// Shows <paramref name="color"/> without stopping the running animation. For
        /// the animation callbacks passed to <see cref="Animate"/>, which own the LED.
        /// </summary>
        public void WriteColor(Color color)
        {
            led?.SetColor(color);
        }

        public void StartBlink(Color color)
        {
            StartBlink(color, DefaultBlinkPeriod, DefaultBlinkPeriod);
        }

        public void StartBlink(Color color, TimeSpan onDuration, TimeSpan offDuration)
        {
            if (led == null)
            {
                return;
            }

            Stop();
            _ = led.StartBlink(color, onDuration, offDuration);
        }

        /// <summary>
        /// Runs <paramref name="animation"/> on its own task, replacing any previous
        /// animation. It should write with <see cref="WriteColor"/> and end when the token
        /// is cancelled.
        /// </summary>
        public void Animate(Func<CancellationToken, Task> animation)
        {
            if (led == null)
            {
                return;
            }

            // cancels any previous animation; the new token exists before the task
            // starts, so an immediate Stop() can't miss it
            Stop();
            cancellationTokenSource = new CancellationTokenSource();
            var token = cancellationTokenSource.Token;
            animationTask = Task.Run(() => animation(token));
        }

        public void TurnOn()
        {
            if (led == null)
            {
                return;
            }

            Stop();
            led.SetColor(led.Color);
            led.IsOn = true;
        }

        public void TurnOn(Color color)
        {
            if (led == null)
            {
                return;
            }

            Stop();
            led.SetColor(color);
            led.IsOn = true;
        }

        public void TurnOff()
        {
            if (led == null)
            {
                return;
            }

            Stop();
            led.IsOn = false;
        }

        public void Stop()
        {
            cancellationTokenSource?.Cancel();

            // our own animation task must also be done writing
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
                led?.StopAnimation().GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                // the animation was cancelled before it began; nothing left to stop
            }
            catch (Exception ex)
            {
                logger.Warn($"Stopping LED animation failed: {ex.Message}");
            }
        }

        public void Dispose()
        {
            cancellationTokenSource?.Cancel();
            led?.Dispose();
        }
    }
}
