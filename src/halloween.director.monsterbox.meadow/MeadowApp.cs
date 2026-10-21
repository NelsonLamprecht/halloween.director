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

        // used if app.config.yaml is missing or doesn't have an App.DeviceName entry
        private const string DefaultDeviceName = "Monsterbox-v1";

        public override async Task Initialize()
        {
            Logger.Info("=== Initializing hardware... ===");

            ApplyStartupSettings(DefaultDeviceName, "monsterbox");

            // first, so the LED shows the app has started (yellow, then red; see LedStatus)
            var ledController = Services.Create<LedController>();
            ledController.Initialize();

            var diagnosticsService = Services.Create<DiagnosticsService>();
            diagnosticsService.OutputMeadowOSInfo();
            diagnosticsService.OutputDeviceInfo();
            diagnosticsService.OutputNtpInfo();

            ledController.SetStatus(LedStatus.Initializing);

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
            var relayController = Services.Create<RelayController>();
            relayController.Initialize(Device.Pins.D05, Device.Pins.D06);

            var mp3Controller = Services.Create<MP3Controller>();
            mp3Controller.Initialize();

            Logger.StartupSection("Sounds", () =>
            {
                var soundCatalog = SoundCatalog.FromSettings(Settings, Logger);
                Services.Add(soundCatalog);
                Logger.StartupDetail($"{soundCatalog.Count} sounds configured for GET /sounds.");
            });

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

            TrackWifiEvents(wifiAdapter);
            wifiAdapter.NetworkConnected += OnNetworkConnected;

            Logger.Info("=== Hardware initialized. ===");

            await base.Initialize();
        }

        public override Task Run()
        {
            Services.Get<IWatchdogService>().Start();

            Services.Get<HeartbeatService>().Run();

            // network connection is handled by Meadow OS via wifi.config.yaml and
            // AutomaticallyStartNetwork in meadow.config.yaml; see OnNetworkConnected
            Logger.Info($"=== Running @ {BootStopwatch.Elapsed}. Waiting for WiFi connection... ===");
            ShowWaitingForWifi();
            return base.Run();
        }

        protected override void DisposeServices()
        {
            DisposeIfRegistered<HeartbeatService>();
            DisposeIfRegistered<LedController>();
            DisposeIfRegistered<MP3Controller>();

            // before RelayController, so an in-flight shake stops before its relays go away
            DisposeIfRegistered<CylindersController>();
            DisposeIfRegistered<RelayController>();
        }
    }
}
