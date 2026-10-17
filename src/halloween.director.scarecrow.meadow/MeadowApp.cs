using System;
using System.Threading.Tasks;

using Meadow;
using Meadow.Hardware;

using Halloween.Director.Scarecrow.Meadow.Controllers;
using Halloween.Director.Shared;
using Halloween.Director.Shared.Controllers;
using Halloween.Director.Shared.Services.DiagnosticsService;
using Halloween.Director.Shared.Services.HeartbeatService;
using Halloween.Director.Shared.Services.MapleService;
using Halloween.Director.Shared.Services.NetworkService;
using Halloween.Director.Shared.Services.Watchdog;

namespace Halloween.Director.Scarecrow.Meadow
{
    public class MeadowApp : MeadowBase
    {
        // used if app.config.yaml is missing or doesn't have an App.DeviceName entry
        private const string DefaultDeviceName = "Scarecrow-v1";

        public override async Task Initialize()
        {
            Logger.Info("=== Initializing hardware... ===");

            ApplyStartupSettings(DefaultDeviceName, "scarecrow");

            var diagnosticsService = Services.Create<DiagnosticsService>();
            diagnosticsService.OutputMeadowOSInfo();
            diagnosticsService.OutputDeviceInfo();
            diagnosticsService.OutputNtpInfo();

            var network = Device.NetworkAdapters.Primary<IWiFiNetworkAdapter>();
            Logger.StartupSection("WiFi", () =>
            {
                LogWifiConfig(network);

                // this device always runs on the external antenna; persisted so it's already
                // in effect before AutomaticallyStartNetwork connects on every boot after the first
                network.SetAntenna(AntennaType.External, true);
                Services.Add(network);
                Logger.StartupDetail("Antenna set to External; waiting for the network to connect.");
            });

            // red until the Maple server is up, then LedController.ReadyColor
            var ledController = Services.Create<LedController>();
            ledController.Initialize();

            Logger.StartupSection("Services", () =>
            {
                Services.Create<WatchdogService, IWatchdogService>();
            });

            // the controller logs its own section from Initialize()
            var relayController = Services.Create<RelayController>();
            relayController.Initialize(Device.Pins.D05);

            Logger.StartupSection("LiftController", () =>
            {
                var options = new LiftOptions();
                if (Settings.TryGetValue("App.LiftMaxUpSeconds", out var configured))
                {
                    if (int.TryParse(configured, out var seconds) && seconds >= 0)
                    {
                        options.MaxUp = TimeSpan.FromSeconds(seconds);
                    }
                    else
                    {
                        Logger.Warn($"App.LiftMaxUpSeconds '{configured}' is not a number of seconds >= 0; using {LiftOptions.DefaultMaxUpSeconds}.");
                    }
                }

                Services.Add(options);
                Services.Create<LiftController>();
                Logger.Info(options.MaxUp > TimeSpan.Zero
                    ? $"Lift lowers itself after {options.MaxUp.TotalSeconds}s up."
                    : "Lift has no time limit.");
            });

            Logger.StartupSection("Network services", () =>
            {
                Services.Create<HeartbeatService>();
                Services.Create<NetworkService>();
                Services.Add(new MapleServiceOptions());
                Services.Create<MapleService>();
                Logger.StartupDetail("Heartbeat, Network and Maple services created.");
            });

            LogWifiEvents(network);
            network.NetworkConnected += OnNetworkConnected;

            Logger.Info("=== Hardware initialized. ===");

            await base.Initialize();
        }

        public override async Task Run()
        {
            try
            {
                Services.Get<IWatchdogService>().Start();

                var relayController = Services.Get<RelayController>();
                await relayController.Run();

                await Services.Get<HeartbeatService>().Run();

                // network connection is handled by Meadow OS via wifi.config.yaml and
                // AutomaticallyStartNetwork in meadow.config.yaml; see OnNetworkConnected
                Logger.Info($"=== Running @ {BootStopwatch.Elapsed}. Waiting for WiFi connection... ===");
            }

            catch (Exception ex)
            {
                Services.Get<LedController>().SetColor(Color.Red);
                Logger.Error(ex.ToString());
            }
            await base.Run();
        }

        protected override void DisposeServices()
        {
            DisposeIfRegistered<HeartbeatService>();

            // the lift's pending auto-lower first, then the relay (which closes the valve
            // before its pin is released)
            DisposeIfRegistered<LiftController>();
            DisposeIfRegistered<RelayController>();
            DisposeIfRegistered<LedController>();
        }
    }
}
