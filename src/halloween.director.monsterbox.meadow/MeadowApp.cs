using System;
using System.Threading.Tasks;

using Meadow.Hardware;

using Halloween.Director.Monsterbox.Meadow.Controllers;
using Halloween.Director.Shared;
using Halloween.Director.Shared.Controllers;
using Halloween.Director.Shared.Services.DiagnosticsService;
using Halloween.Director.Shared.Services.HeartbeatService;
using Halloween.Director.Shared.Services.MapleService;
using Halloween.Director.Shared.Services.NetworkService;
using Halloween.Director.Shared.Services.Watchdog;

namespace Halloween.Director.Monsterbox.Meadow
{
    public class MeadowApp : MeadowBase
    {
        private bool _servicesStarted = false;

        // used if app.config.yaml is missing or doesn't have an App.DeviceName entry
        private const string DefaultDeviceName = "Monsterbox-v1";

        public override async Task Initialize()
        {
            Logger.Info("=== Initializing hardware... ===");

            ApplyStartupSettings(DefaultDeviceName);

            var diagnosticsService = Services.Create<DiagnosticsService>();
            diagnosticsService.OutputMeadowOSInfo();
            diagnosticsService.OutputDeviceInfo();
            diagnosticsService.OutputNtpInfo();

            var wifiAdapter = Device.NetworkAdapters.Primary<IWiFiNetworkAdapter>();

            Logger.StartupSection("WiFi", () =>
            {
                LogWifiConfig(wifiAdapter);

                // this device always runs on the external antenna; persisted so it's already
                // in effect before AutomaticallyStartNetwork connects on every boot after the first
                Logger.Info("Setting antenna to External...");
                wifiAdapter.SetAntenna(AntennaType.External, true);
                Services.Add(wifiAdapter);
            });

            // each controller logs its own section from Initialize()
            var ledController = Services.Create<LedController>();
            ledController.Initialize();

            var relayController = Services.Create<RelayController>();
            relayController.Initialize(Device.Pins.D05, Device.Pins.D06);

            var mp3Controller = Services.Create<MP3Controller>();
            mp3Controller.Initialize();

            Logger.StartupSection("Services", () =>
            {
                Services.Create<WatchdogService, IWatchdogService>();
                Services.Create<NetworkService>();
                Services.Create<HeartbeatService>();
                Services.Create<CylindersController>();
                Services.Add(new MapleServiceOptions());
                Services.Create<MapleService>();
                Logger.StartupDetail("Watchdog, Network, Heartbeat, Cylinders and Maple services created.");
            });

            LogWifiEvents(wifiAdapter);
            wifiAdapter.NetworkConnected += OnWifiConnected;

            Logger.Info("=== Hardware initialized. ===");

            await base.Initialize();
        }

        public override Task Run()
        {
            var watchdog = Services.Get<IWatchdogService>();
            watchdog.Enable(15);
            watchdog.Pet(10);

            Services.Get<HeartbeatService>().Run();

            // network connection is handled by Meadow OS via wifi.config.yaml and
            // AutomaticallyStartNetwork in meadow.config.yaml; see OnWifiConnected
            Logger.Info($"=== Running @ {BootStopwatch.Elapsed}. Waiting for WiFi connection... ===");
            return base.Run();
        }

        // async void is deliberate: this is an event handler, and awaiting MapleService.Run()
        // lets a startup failure be caught and logged instead of vanishing in an unobserved task
        private async void OnWifiConnected(INetworkAdapter sender, NetworkConnectionEventArgs args)
        {
            Logger.StartupSection("WiFiConnected", () =>
                Logger.Info($"WiFi connected @ {BootStopwatch.Elapsed}. IP: {args.IpAddress}, Gateway: {args.Gateway}, Subnet: {args.Subnet}"));

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

            Logger.Info($"=== Startup complete @ {BootStopwatch.Elapsed}. ===");
        }

        public override Task OnShutdown()
        {
            Logger.Info($"=== Shutting down @ {BootStopwatch.Elapsed}... ===");

            if (Services.ContainsRegisteredType<HeartbeatService>())
            {
                Services.Get<HeartbeatService>().Dispose();
            }

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
