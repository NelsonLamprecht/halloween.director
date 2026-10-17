using Meadow;
using Meadow.Devices;
using Meadow.Logging;

namespace Halloween.Director.Shared
{
    public abstract class MeadowBase: App<F7FeatherV1>
    {
        protected Logger Logger { get; } = Resolver.Log;

        protected ServiceCollection Services { get; } = Resolver.Services;

        public MeadowBase() { }
    }
}
