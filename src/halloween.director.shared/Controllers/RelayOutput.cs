using System;

using Meadow;
using Meadow.Hardware;

namespace Halloween.Director.Shared.Controllers
{
    /// <summary>
    /// One relay channel on the active-low SainSmart relay board, driven by a single open-drain output pin.
    ///
    /// On pulls the pin low, which energizes the relay. Off lets the pin float, which leaves it de-energized.
    /// Starts off, and is switched off again before the port is released.
    /// </summary>
    public class RelayOutput : IOnOrOffController, IDisposable
    {
        private const bool PinOn = false;
        private const bool PinOff = true;

        private readonly IDigitalOutputPort port;

        public RelayOutput(IMeadowDevice device, IPin pin)
        {
            // starts floating so the relay stays off from the moment the port is created.
            // opendrain is what seems to work with a relay board
            port = device.CreateDigitalOutputPort(pin, PinOff, OutputType.OpenDrain);
        }

        public void TurnOn()
        {
            port.State = PinOn;
        }

        public void TurnOff()
        {
            port.State = PinOff;
        }

        public void Dispose()
        {
            // leave the relay de-energized before letting go of the pin
            TurnOff();

            port?.Dispose();
        }
    }
}
