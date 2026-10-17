using System;
using System.Diagnostics;
using System.Threading.Tasks;

using Meadow;
using Meadow.Devices;
using Meadow.Hardware;
using Meadow.Logging;

using Halloween.Director.Shared.Controllers;
using Halloween.Director.Shared.Services.MapleService;
using Halloween.Director.Shared.Services.NetworkService;

namespace Halloween.Director.Shared
{
    public abstract class MeadowBase: App<F7FeatherV1>
    {
        protected Logger Logger { get; } = Resolver.Log;

        protected ServiceCollection Services { get; } = Resolver.Services;

        /// <summary>Time since the app object was created, for the "@ 00:00:04" log timestamps.</summary>
        protected Stopwatch BootStopwatch { get; } = Stopwatch.StartNew();

        public MeadowBase() { }

        // Logs what wifi.config.yaml and meadow.config.yaml gave the adapter.
        protected void LogWifiConfig(IWiFiNetworkAdapter wifi)
        {
            Logger.Info(
                $"WiFi config from wifi.config.yaml -> DefaultSsid: '{wifi.DefaultSsid}', " +
                $"AutoConnect: {wifi.AutoConnect}, AutoReconnect: {wifi.AutoReconnect}");
        }

        // Logs the WiFi lifecycle events. The app subscribes to NetworkConnected itself,
        // because that is where it starts its network services.
        protected void LogWifiEvents(IWiFiNetworkAdapter wifi)
        {
            wifi.NetworkConnecting += (sender) =>
                Logger.Info($"WiFi connecting @ {BootStopwatch.Elapsed}...");
            wifi.NetworkConnectFailed += (sender) =>
                Logger.Warn($"WiFi connect failed @ {BootStopwatch.Elapsed}.");
            wifi.NetworkDisconnected += (sender, args) =>
                Logger.Warn($"WiFi disconnected @ {BootStopwatch.Elapsed}. Reason: {args.Reason}");
            wifi.NetworkError += (sender, args) =>
                Logger.Error($"WiFi network error @ {BootStopwatch.Elapsed}. ErrorCode: {args.ErrorCode}");
        }

        private bool _networkServicesStarted;

        // Subscribe to this from NetworkConnected. It runs the network diagnostics, then starts
        // the Maple server, then turns the LED to ready. The app must have created NetworkService,
        // MapleService and LedController first.
        //
        // async void is deliberate: this is an event handler, and awaiting MapleService.Run()
        // lets a startup failure be caught and logged instead of vanishing in an unobserved task.
        protected async void OnNetworkConnected(INetworkAdapter sender, NetworkConnectionEventArgs args)
        {
            Logger.StartupSection("WiFiConnected", () =>
                Logger.Info($"WiFi connected @ {BootStopwatch.Elapsed}. IP: {args.IpAddress}, Gateway: {args.Gateway}, Subnet: {args.Subnet}"));

            Services.Get<NetworkService>().NetworkIsConnected(sender);

            // AutomaticallyReconnect can raise this again after a drop. The Maple server is
            // bound to a specific IP, so leave it alone if the address is unchanged and
            // rebuild it if DHCP handed out a new one.
            var mapleService = Services.Get<MapleService>();
            if (_networkServicesStarted && args.IpAddress.Equals(mapleService.BoundAddress))
            {
                return;
            }

            try
            {
                Logger.Info(_networkServicesStarted
                    ? $"IP changed to {args.IpAddress}; restarting MapleService..."
                    : "Starting MapleService...");
                await mapleService.Run();
            }
            catch (Exception ex)
            {
                // the next NetworkConnected event retries
                Logger.Error($"MapleService failed to start: {ex}");
                return;
            }
            _networkServicesStarted = true;

            Services.Get<LedController>().SetColor(LedController.ReadyColor);

            Logger.Info($"=== Startup complete @ {BootStopwatch.Elapsed}. ===");
        }

        public override Task OnError(Exception e)
        {
            Logger.Error($"Unhandled application error @ {BootStopwatch.Elapsed}: {e}");
            return base.OnError(e);
        }

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
