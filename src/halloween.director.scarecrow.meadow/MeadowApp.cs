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
        private bool _servicesStarted;

        // used if app.config.yaml is missing or doesn't have an App.DeviceName entry
        private const string DefaultDeviceName = "Scarecrow-v1";

        public override async Task Initialize()
        {
            Logger.Info("=== Initializing hardware... ===");

            ApplyStartupSettings(DefaultDeviceName);

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

            Logger.StartupSection("Network services", () =>
            {
                Services.Create<HeartbeatService>();
                Services.Create<NetworkService>();
                Services.Add(new MapleServiceOptions());
                Services.Create<MapleService>();
                Logger.StartupDetail("Heartbeat, Network and Maple services created.");
            });

            LogWifiEvents(network);
            network.NetworkConnected += NetworkConnected;

            Logger.Info("=== Hardware initialized. ===");

            await base.Initialize();
        }

        public override async Task Run()
        {
            try
            {
                var w = Services.Get<IWatchdogService>();
                w.Enable(15);
                w.Pet(10);

                var relayController = Services.Get<RelayController>();
                await relayController.Run();

                await Services.Get<HeartbeatService>().Run();

                // network connection is handled by Meadow OS via wifi.config.yaml and
                // AutomaticallyStartNetwork in meadow.config.yaml; see NetworkConnected
                Logger.Info($"=== Running @ {BootStopwatch.Elapsed}. Waiting for WiFi connection... ===");
            }

            catch (Exception ex)
            {
                Services.Get<LedController>().SetColor(Color.Red);
                Logger.Error(ex.ToString());
            }
            await base.Run();
        }

        // async void is deliberate: this is an event handler, and awaiting MapleService.Run()
        // lets a startup failure be caught and logged instead of vanishing in an unobserved task
        private async void NetworkConnected(INetworkAdapter sender, NetworkConnectionEventArgs args)
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

            // relay first, so the valve is closed before the pin is released
            if (Services.ContainsRegisteredType<RelayController>())
            {
                Services.Get<RelayController>().Dispose();
            }

            if (Services.ContainsRegisteredType<LedController>())
            {
                Services.Get<LedController>().Dispose();
            }

            return base.OnShutdown();
        }
    }
}
