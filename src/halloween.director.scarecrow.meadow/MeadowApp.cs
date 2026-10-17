using System;
using System.Threading.Tasks;

using Meadow;
using Meadow.Hardware;

using Halloween.Director.Scarecrow.Meadow.Controllers;
using Halloween.Director.Shared;
using Halloween.Director.Shared.Controllers;
using Halloween.Director.Shared.Services.DiagnosticsService;
using Halloween.Director.Shared.Services.MapleService;
using Halloween.Director.Shared.Services.NetworkService;
using Halloween.Director.Shared.Services.Watchdog;

namespace Halloween.Director.Scarecrow.Meadow
{
    public class MeadowApp : MeadowBase
    {
        private bool _servicesStarted;

        public override async Task Initialize()
        {
            var network = Device.NetworkAdapters.Primary<IWiFiNetworkAdapter>();
            network.SetAntenna(AntennaType.External);
            network.NetworkConnected += NetworkConnected;

            Services.Add(network);

            // red until the Maple server is up, then LedController.ReadyColor
            var ledController = Services.Create<LedController>();
            ledController.Initialize();

            Services.Create<WatchdogService, IWatchdogService>();

            Services.Create<DiagnosticsService>();
            var relayController = Services.Create<RelayController>();
            relayController.Initialize(Device.Pins.D05);

            Services.Create<NetworkService>();
            Services.Add(new MapleServiceOptions { AdvertiseIntervalMs = 1500 });
            Services.Create<MapleService>();

            await base.Initialize();
        }

        public override async Task Run()
        {
            try
            {
                Logger.Debug($"+Run");

                var diagnostics = Services.Get<DiagnosticsService>();
                diagnostics.OutputDeviceInfo();
                diagnostics.OutputMeadowOSInfo();
                diagnostics.OutputNtpInfo();

                var w = Services.Get<IWatchdogService>();
                w.Enable(15);
                w.Pet(10);

                var relayController = Services.Get<RelayController>();
                await relayController.Run();
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
        }

        public override Task OnShutdown()
        {
            Logger.Info("=== Shutting down... ===");

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
