using System;

using Meadow;
using Meadow.Hardware;
using Meadow.Logging;
using Meadow.Peripherals.Relays;

using Halloween.Director.Shared;
using Halloween.Director.Shared.Controllers;

namespace Halloween.Director.Scarecrow.Meadow.Controllers
{
    // <summary>
    // The commands all inverted since we are working with relays that keep the valves closed.
    // TurnOn() opens the relay (lift up); TurnOff() closes it (lift down, the rest state).
    // </summary>
    public class RelayController : BaseController, IOnOrOffController, IDisposable
    {
        private readonly IMeadowDevice device;
        private RelayOutput relay;

        public RelayController(Logger logger, IMeadowDevice device) : base(logger)
        {
            this.device = device;
        }

        public void Initialize(IPin devicePin)
        {
            if (relay != null)
            {
                return;
            }

            Logger.StartupSection($"{GetType().Name}.Initialize", () =>
            {
                // open is on: the relays keep the valves closed. The output starts off so
                // the valve is closed as quickly as possible when the board boots up.
                relay = new RelayOutput(device, devicePin, RelayState.Open);
                Logger.StartupDetail($"Relay on {devicePin.Name}; starts off (valve closed).");
            });

            Logger.Info($"{GetType().Name} is initialized.");
        }

        public void TurnOff()
        {
            Relay.TurnOff();
            Logger.Info("Relay Is Closed.");
        }

        public void TurnOn()
        {
            Relay.TurnOn();
            Logger.Info("Relay Is Open.");
        }

        public void Dispose()
        {
            // closes the valve before releasing the pin
            relay?.Dispose();
        }

        private RelayOutput Relay => relay ?? throw new InvalidOperationException($"{GetType().Name} is not initialized.");
    }
}
