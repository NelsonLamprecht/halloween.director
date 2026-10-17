using Meadow;
using Meadow.Foundation.Relays;
using Meadow.Hardware;
using Meadow.Logging;
using Meadow.Peripherals.Relays;

namespace meadow_scarecrow.Controllers.RelayController
{
    // <summary>
    // The commands all inverted since we are working with relays that keep the valves closed.
    // </summary>
    public class RelayController : BaseController, IOnOrOffController
    {
        private readonly IMeadowDevice device;
        private Relay relay;
        private bool initialized = false;
        private IPin devicePin;

        public RelayController(Logger logger, IMeadowDevice device) : base(logger)
        {
            this.device = device;
        }

        public void Initialize(IPin devicePin)
        {
            if (initialized)
            {
                return;
            }
            this.devicePin = devicePin;
            var outputPort = device.CreateDigitalOutputPort(this.devicePin, true, OutputType.OpenDrain);
            relay = new Relay(outputPort, RelayType.NormallyOpen);

            // so port is closed as quickly as possible when board boots up
            TurnOff();
            initialized = true;
        }

        public void TurnOff()
        {
            relay.State = RelayState.Closed;
            Logger.Debug("Relay Is Closed.");
        }

        public void TurnOn()
        {
            relay.State = RelayState.Open;
            Logger.Debug("Relay Is Open.");
        }
    }
}