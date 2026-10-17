using halloween.director.shared.Controllers;
using System;

using Meadow;
using Meadow.Foundation.Relays;
using Meadow.Hardware;
using Meadow.Logging;
using Meadow.Peripherals.Relays;

namespace halloween.director.monsterbox.meadow.Controllers
{
    /// <summary>
    /// The commands all inverted since the pneumatics are keeping the values closed
    /// </summary>
    internal class RelayController : BaseController, IDisposable
    {
        private readonly IMeadowDevice device;

        private IDigitalOutputPort _leftRelayPort;
        private IDigitalOutputPort _rightRelayPort;
        private Relay relayLeft;
        private Relay relayRight;
        private bool initialized = false;

        public RelayController(Logger logger, IMeadowDevice device) : base(logger)
        {
            this.device = device;
        }

        public void Initialize(IPin leftPin, IPin rightPin)
        {
            if (initialized)
            {
                return;
            }
            // true so port is closed as quickly as possible when board boots up.
            //opendrain is what seems to work with a relay board
            _leftRelayPort = device.CreateDigitalOutputPort(leftPin, true, OutputType.OpenDrain);
            _rightRelayPort = device.CreateDigitalOutputPort(rightPin, true, OutputType.OpenDrain);
            relayLeft = new Relay(_leftRelayPort, RelayType.NormallyOpen);
            relayRight = new Relay(_rightRelayPort, RelayType.NormallyOpen);
            TurnOffLeft();
            TurnOffRight();
            initialized = true;

            Logger.Info($"{GetType().Name} is initialized.");
        }

        public void TurnOffLeft()
        {
            relayLeft.State = RelayState.Open;
        }

        public void TurnOffRight()
        {
            relayRight.State = RelayState.Open;
        }

        public void TurnOnLeft()
        {
            relayLeft.State = RelayState.Closed;
        }

        public void TurnOnRight()
        {
            relayRight.State = RelayState.Closed;
        }

        public void Dispose()
        {
            // leave the cylinders de-energized before letting go of the pins
            if (initialized)
            {
                TurnOffLeft();
                TurnOffRight();
            }

            (relayLeft as IDisposable)?.Dispose();
            (relayRight as IDisposable)?.Dispose();
            _leftRelayPort?.Dispose();
            _rightRelayPort?.Dispose();
        }
    }
}
