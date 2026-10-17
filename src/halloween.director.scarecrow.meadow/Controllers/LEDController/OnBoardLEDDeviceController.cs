using System;

using Meadow;
using Meadow.Devices;
using Meadow.Logging;

using Halloween.Director.Shared.Controllers;

namespace Halloween.Director.Scarecrow.Meadow.Controllers.LEDController
{
    public class OnBoardLEDDeviceController : ILEDDeviceController, IDisposable
    {
        private readonly OnboardRgbLed led;

        public OnBoardLEDDeviceController(Logger logger, IMeadowDevice device)
        {
            led = new OnboardRgbLed(logger, device);
            led.StartBlink(Color.White);
        }

        public void SetColor(Color color) => led.SetColor(color);

        public void Stop() => led.Stop();

        public void StartBlink(Color color) => led.StartBlink(color);

        public void TurnOn() => led.TurnOn();

        public void TurnOff() => led.TurnOff();

        public void Dispose() => led.Dispose();
    }
}
