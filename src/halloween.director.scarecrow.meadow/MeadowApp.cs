using System;
using System.Threading.Tasks;

using Meadow;
using Meadow.Hardware;

using Halloween.Director.Scarecrow.Meadow.Controllers.LEDController;
using Halloween.Director.Scarecrow.Meadow.Controllers.RelayController;
using Halloween.Director.Shared;
using Halloween.Director.Shared.Services.DiagnosticsService;
using Halloween.Director.Shared.Services.MapleService;
using Halloween.Director.Shared.Services.NetworkService;
using Halloween.Director.Shared.Services.Watchdog;

namespace Halloween.Director.Scarecrow.Meadow
{
    public class MeadowApp : MeadowBase
    {
        // the remote app sends scarecrow commands to this port (monsterbox uses 5417)
        private const int MaplePort = 5418;

        private ILEDDeviceController _ledDevice;
        private bool _servicesStarted;

        public override async Task Initialize()
        {
            var network = Device.NetworkAdapters.Primary<IWiFiNetworkAdapter>();
            network.SetAntenna(AntennaType.External);
            network.NetworkConnected += NetworkConnected;

            Services.Add(network);

            //rework the device dependancy for the controller so its not just the meadow device
            _ledDevice = Services.Create<OnBoardLEDDeviceController, ILEDDeviceController>();
            Services.Create<WatchdogService, IWatchdogService>();

            Services.Create<DiagnosticsService>();
            var relayController = Services.Create<RelayController>();
            relayController.Initialize(Device.Pins.D05);

            Services.Create<NetworkService>();
            // each prop listens on its own port so several can share one network
            Services.Add(new MapleServiceOptions(MaplePort) { AdvertiseIntervalMs = 1500 });
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

                _ledDevice.StartBlink(Color.Blue);
            }

            catch (Exception ex)
            {
                _ledDevice.SetColor(Color.Red);
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

            Services.Get<ILEDDeviceController>().StartBlink(Color.Green);
        }

        public override Task OnShutdown()
        {
            Logger.Info("=== Shutting down... ===");

            // relay first, so the valve is closed before the pin is released
            if (Services.ContainsRegisteredType<RelayController>())
            {
                Services.Get<RelayController>().Dispose();
            }

            if (Services.ContainsRegisteredType<ILEDDeviceController>() && Services.Get<ILEDDeviceController>() is IDisposable led)
            {
                led.Dispose();
            }

            return base.OnShutdown();
        }
    }
}
