using Meadow;
using Meadow.Devices;
using Meadow.Logging;

namespace Halloween.Director.Shared
{
    public abstract class MeadowBase: App<F7FeatherV1>
    {
        protected Logger Logger { get; } = Resolver.Log;

        protected ServiceCollection Services { get; } = Resolver.Services;

        public MeadowBase()
        {
#if DEBUG
            // Debug builds log at Trace so the TraceSection startup banners show. Release
            // builds keep the level from app.config.yaml (Logging.LogLevel.Default).
            Logger.LogLevel = LogLevel.Trace;
#endif
        }

        // Names the device from app.config.yaml's App.DeviceName (the name Maple advertises),
        // falling back to defaultName if the setting is missing.
        protected void ApplyDeviceName(string defaultName)
        {
            if (Settings.TryGetValue("App.DeviceName", out var configuredDeviceName) && !string.IsNullOrWhiteSpace(configuredDeviceName))
            {
                Device.Information.DeviceName = configuredDeviceName;
            }
            else
            {
                Logger.Warn($"App.DeviceName not found in app.config.yaml; falling back to '{defaultName}'.");
                Device.Information.DeviceName = defaultName;
            }
            Logger.Info($"Device name: {Device.Information.DeviceName}");
        }
    }
}
