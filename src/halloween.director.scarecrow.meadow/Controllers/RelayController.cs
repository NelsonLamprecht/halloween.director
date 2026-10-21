using System;

using Meadow;
using Meadow.Hardware;
using Meadow.Logging;

using Halloween.Director.Shared;
using Halloween.Director.Shared.Controllers;

namespace Halloween.Director.Scarecrow.Meadow.Controllers
{
    // <summary>
    // TurnOn() energizes the relay (lift up); TurnOff() releases it (lift down, the rest state).
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
                // the output starts off so the valve is closed as quickly as possible when the board boots up.
                relay = new RelayOutput(device, devicePin);
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
