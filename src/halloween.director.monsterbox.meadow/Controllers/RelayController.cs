using System;

using Meadow;
using Meadow.Hardware;
using Meadow.Logging;

using Halloween.Director.Shared;
using Halloween.Director.Shared.Controllers;

namespace Halloween.Director.Monsterbox.Meadow.Controllers
{
    /// <summary>
    /// Two relay channels on the active-low SainSmart board, one per cylinder.
    /// On energizes the relay (pin pulled low) and fires the cylinder; off leaves it at rest.
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

            Logger.StartupSection($"{GetType().Name}.Initialize", () =>
            {
                left = new RelayOutput(device, leftPin);
                right = new RelayOutput(device, rightPin);
                initialized = true;

                Logger.StartupDetail($"Left relay on {leftPin.Name}, right relay on {rightPin.Name}; both start off.");
            });

            Logger.Info($"{GetType().Name} is initialized.");
        }

        /// <summary>The left cylinder's relay. On energizes the cylinder.</summary>
        public IOnOrOffController Left => left ?? throw NotInitialized();

        /// <summary>The right cylinder's relay. On energizes the cylinder.</summary>
        public IOnOrOffController Right => right ?? throw NotInitialized();

        public void Dispose()
        {
            // each output leaves its cylinder de-energized before releasing its pin
            left?.Dispose();
            right?.Dispose();
        }

        private InvalidOperationException NotInitialized()
        {
            return new InvalidOperationException($"{GetType().Name} is not initialized.");
        }
    }
}
