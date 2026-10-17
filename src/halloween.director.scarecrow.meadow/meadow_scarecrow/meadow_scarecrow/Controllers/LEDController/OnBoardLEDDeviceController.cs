using System;
using System.Threading;

using Meadow;
using Meadow.Devices;
using Meadow.Foundation.Leds;
using Meadow.Foundation;

namespace meadow_scarecrow.Controllers.LEDController
{
    public class OnBoardLEDDeviceController : ILEDDeviceController
    {
        CancellationTokenSource cancellationTokenSource = null;
        private readonly IMeadowDevice device;
        private readonly RgbPwmLed rgbLed = null;

        public OnBoardLEDDeviceController(IMeadowDevice device)
        {
            this.device = device;

            // TODO: handle other devices
            if (this.device is F7FeatherV1 f7DeviceV1)
            {
                rgbLed = new RgbPwmLed(
                    redPwmPin: f7DeviceV1.Pins.OnboardLedRed,
                    greenPwmPin: f7DeviceV1.Pins.OnboardLedGreen,
                    bluePwmPin: f7DeviceV1.Pins.OnboardLedBlue,
                    commonType: Meadow.Peripherals.Leds.CommonType.CommonAnode);
            }

            rgbLed?.StartBlink(Color.White);
        }

        public void SetColor(Color color)
        {
            Stop();
            rgbLed?.SetColor(color);
        }

        public void Stop()
        {
            rgbLed?.StopAnimation();
            cancellationTokenSource?.Cancel();
        }

        public void StartBlink(Color color)
        {
            Stop();
            rgbLed?.StartBlink(color, TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(500));
        }

        public void TurnOn()
        {
            Stop();
            if (rgbLed != null)
            {
                rgbLed.SetColor(rgbLed.Color);
                rgbLed.IsOn = true;
            }
        }

        public void TurnOff()
        {
            Stop();
            if (rgbLed != null)
            {
                rgbLed.IsOn = false;
            }
        }
    }
}

