using System;

using Meadow;
using Meadow.Foundation.Relays;
using Meadow.Hardware;
using Meadow.Peripherals.Relays;

namespace Halloween.Director.Shared.Controllers
{
    /// <summary>
    /// One relay channel on a relay board, driven by a single open-drain output pin.
    ///
    /// Which <see cref="RelayState"/> counts as "on" depends on what the relay switches
    /// (e.g. the pneumatics keep their valves closed), so the caller passes it in.
    /// Starts off, and is switched off again before the port is released.
    /// </summary>
    public class RelayOutput : IOnOrOffController, IDisposable
    {
        private readonly IDigitalOutputPort port;
        private readonly Relay relay;
        private readonly RelayState onState;
        private readonly RelayState offState;

        public RelayOutput(IMeadowDevice device, IPin pin, RelayState onState)
        {
            this.onState = onState;
            offState = onState == RelayState.Closed ? RelayState.Open : RelayState.Closed;

            // true so port is closed as quickly as possible when board boots up.
            // opendrain is what seems to work with a relay board
            port = device.CreateDigitalOutputPort(pin, true, OutputType.OpenDrain);
            relay = new Relay(port, RelayType.NormallyOpen);
            TurnOff();
        }

        public void TurnOn()
        {
            relay.State = onState;
        }

        public void TurnOff()
        {
            relay.State = offState;
        }

        public void Dispose()
        {
            // leave the relay de-energized before letting go of the pin
            TurnOff();

            (relay as IDisposable)?.Dispose();
            port?.Dispose();
        }
    }
}
