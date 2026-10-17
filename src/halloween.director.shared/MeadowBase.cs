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

        // Applies app.config.yaml's App settings: App.StartupDetails (default true) turns the
        // startup banners and detail lines on or off, and App.DeviceName names the device (the
        // name Maple advertises), falling back to defaultDeviceName if the setting is missing.
        protected void ApplyStartupSettings(string defaultDeviceName)
        {
            if (Settings.TryGetValue("App.StartupDetails", out var startupDetails) && bool.TryParse(startupDetails, out var enabled))
            {
                LoggerExtensions.StartupDetails = enabled;
            }

            ApplyDeviceName(defaultDeviceName);
        }

        // Names the device from app.config.yaml's App.DeviceName (the name Maple advertises),
        // falling back to defaultName if the setting is missing.
        private void ApplyDeviceName(string defaultName)
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
