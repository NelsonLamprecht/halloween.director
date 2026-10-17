using System;
using System.Diagnostics;
using System.Threading.Tasks;

using Meadow;
using Meadow.Hardware;

using Halloween.Director.Monsterbox.Meadow.Controllers;
using Halloween.Director.Shared;
using Halloween.Director.Shared.Controllers;
using Halloween.Director.Shared.Services.DiagnosticsService;
using Halloween.Director.Shared.Services.MapleService;
using Halloween.Director.Shared.Services.NetworkService;
using Halloween.Director.Shared.Services.Watchdog;

namespace Halloween.Director.Monsterbox.Meadow
{
    public class MeadowApp : MeadowBase
    {
        private bool _servicesStarted = false;
        private readonly Stopwatch _bootStopwatch = Stopwatch.StartNew();

        // used if app.config.yaml is missing or doesn't have an App.DeviceName entry
        private const string DefaultDeviceName = "Monsterbox-v1";

        public override async Task Initialize()
        {
            Logger.Info("=== Initializing hardware... ===");

            if (Settings.TryGetValue("App.DeviceName", out var configuredDeviceName) && !string.IsNullOrWhiteSpace(configuredDeviceName))
            {
                Device.Information.DeviceName = configuredDeviceName;
            }
            else
            {
                Logger.Warn($"App.DeviceName not found in app.config.yaml; falling back to '{DefaultDeviceName}'.");
                Device.Information.DeviceName = DefaultDeviceName;
            }
            Logger.Info($"Device name: {Device.Information.DeviceName}");

            var diagnosticsService = Services.Create<DiagnosticsService>();
            diagnosticsService.OutputMeadowOSInfo();
            diagnosticsService.OutputDeviceInfo();
            diagnosticsService.OutputNtpInfo();

            var wifiAdapter = Device.NetworkAdapters.Primary<IWiFiNetworkAdapter>();

            Logger.Info(
                $"WiFi config from wifi.config.yaml -> DefaultSsid: '{wifiAdapter.DefaultSsid}', " +
                $"AutoConnect: {wifiAdapter.AutoConnect}, AutoReconnect: {wifiAdapter.AutoReconnect}");

            // this device always runs on the external antenna; persisted so it's already
            // in effect before AutomaticallyStartNetwork connects on every boot after the first
            Logger.Info("Setting antenna to External...");
            wifiAdapter.SetAntenna(AntennaType.External, true);
            Services.Add(wifiAdapter);

            Logger.Info("Initializing LedController...");
            var ledController = Services.Create<LedController>();
            ledController.Initialize();

            Logger.Info("Initializing RelayController...");
            var relayController = Services.Create<RelayController>();
            relayController.Initialize(Device.Pins.D05, Device.Pins.D06);

            Logger.Info("Initializing MP3Controller...");
            var mp3Controller = Services.Create<MP3Controller>();
            mp3Controller.Initialize();

            Logger.Info("Creating remaining services...");
            Services.Create<WatchdogService, IWatchdogService>();
            Services.Create<NetworkService>();
            Services.Create<CylindersController>();
            Services.Add(new MapleServiceOptions());
            Services.Create<MapleService>();

            wifiAdapter.NetworkConnecting += (sender) =>
                Logger.Info($"WiFi connecting @ {_bootStopwatch.Elapsed}...");
            wifiAdapter.NetworkConnected += OnWifiConnected;
            wifiAdapter.NetworkConnectFailed += (sender) =>
                Logger.Warn($"WiFi connect failed @ {_bootStopwatch.Elapsed}.");
            wifiAdapter.NetworkDisconnected += (sender, args) =>
                Logger.Warn($"WiFi disconnected @ {_bootStopwatch.Elapsed}. Reason: {args.Reason}");
            wifiAdapter.NetworkError += (sender, args) =>
                Logger.Error($"WiFi network error @ {_bootStopwatch.Elapsed}. ErrorCode: {args.ErrorCode}");

            Logger.Info("=== Hardware initialized. ===");

            await base.Initialize();
        }

        public override Task Run()
        {
            Logger.Info("Enabling watchdog...");
            var watchdog = Services.Get<IWatchdogService>();
            watchdog.Enable(15);
            watchdog.Pet(10);

            // network connection is handled by Meadow OS via wifi.config.yaml and
            // AutomaticallyStartNetwork in meadow.config.yaml; see OnWifiConnected
            Logger.Info($"=== Running @ {_bootStopwatch.Elapsed}. Waiting for WiFi connection... ===");
            return base.Run();
        }

        // async void is deliberate: this is an event handler, and awaiting MapleService.Run()
        // lets a startup failure be caught and logged instead of vanishing in an unobserved task
        private async void OnWifiConnected(INetworkAdapter sender, NetworkConnectionEventArgs args)
        {
            Logger.Info($"WiFi connected @ {_bootStopwatch.Elapsed}. IP: {args.IpAddress}, Gateway: {args.Gateway}, Subnet: {args.Subnet}");

            var networkService = Services.Get<NetworkService>();
            networkService.NetworkIsConnected(sender);

            // AutomaticallyReconnect can raise this again after a drop. The Maple server is
            // bound to a specific IP, so leave it alone if the address is unchanged and
            // rebuild it if DHCP handed out a new one.
            var mapleService = Services.Get<MapleService>();
            if (_servicesStarted && args.IpAddress.Equals(mapleService.BoundAddress))
            {
                return;
            }

            try
            {
                Logger.Info(_servicesStarted
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
            _servicesStarted = true;

            Services.Get<LedController>().SetColor(LedController.ReadyColor);

            Logger.Info($"=== Startup complete @ {_bootStopwatch.Elapsed}. ===");
        }

        public override Task OnError(Exception e)
        {
            Logger.Error($"Unhandled application error @ {_bootStopwatch.Elapsed}: {e}");
            return base.OnError(e);
        }

        public override Task OnShutdown()
        {
            Logger.Info($"=== Shutting down @ {_bootStopwatch.Elapsed}... ===");

            if (Services.ContainsRegisteredType<LedController>())
            {
                Services.Get<LedController>().Dispose();
            }

            if (Services.ContainsRegisteredType<MP3Controller>())
            {
                Services.Get<MP3Controller>().Dispose();
            }

            // before RelayController, so an in-flight shake stops before its relays go away
            if (Services.ContainsRegisteredType<CylindersController>())
            {
                Services.Get<CylindersController>().Dispose();
            }

            if (Services.ContainsRegisteredType<RelayController>())
            {
                Services.Get<RelayController>().Dispose();
            }

            return base.OnShutdown();
        }
    }
}
