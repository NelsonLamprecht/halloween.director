using Meadow;
using Meadow.Devices;
using Meadow.Logging;

namespace halloween.director.scarecrow.meadow
{
    public abstract class MeadowBase: App<F7FeatherV1>
    {
        //protected TestAppSettings AppSettings { get; } = (TestAppSettings)Resolver.Services.Get(typeof(TestAppSettings));

        protected Logger Logger { get; } = Resolver.Log;

        protected ServiceCollection Services { get; } = Resolver.Services;

        //protected new IMeadowDevice Device { get; } = Resolver.Device;


        public MeadowBase() { }
    }
}
