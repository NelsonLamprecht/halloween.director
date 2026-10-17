using System;

using Meadow;
using Meadow.Hardware;
using Meadow.Logging;
using Meadow.Peripherals.Relays;

using Halloween.Director.Shared.Controllers;

namespace Halloween.Director.Monsterbox.Meadow.Controllers
{
    /// <summary>
    /// The commands all inverted since the pneumatics are keeping the values closed
    /// </summary>
    internal class RelayController : BaseController, IDisposable
    {
        private readonly IMeadowDevice device;

        private RelayOutput left;
        private RelayOutput right;
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

            // closed is on: the pneumatics keep their valves closed
            left = new RelayOutput(device, leftPin, RelayState.Closed);
            right = new RelayOutput(device, rightPin, RelayState.Closed);
            initialized = true;

            Logger.Info($"{GetType().Name} is initialized.");
        }

        public void TurnOffLeft()
        {
            Left.TurnOff();
        }

        public void TurnOffRight()
        {
            Right.TurnOff();
        }

        public void TurnOnLeft()
        {
            Left.TurnOn();
        }

        public void TurnOnRight()
        {
            Right.TurnOn();
        }

        public void Dispose()
        {
            // each output leaves its cylinder de-energized before releasing its pin
            left?.Dispose();
            right?.Dispose();
        }

        private RelayOutput Left => left ?? throw NotInitialized();

        private RelayOutput Right => right ?? throw NotInitialized();

        private InvalidOperationException NotInitialized()
        {
            return new InvalidOperationException($"{GetType().Name} is not initialized.");
        }
    }
}
