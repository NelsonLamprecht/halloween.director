using System;
using System.Threading.Tasks;

using Meadow;
using Meadow.Hardware;

using Halloween.Director.Scarecrow.Meadow.Controllers.LEDController;
using Halloween.Director.Scarecrow.Meadow.Controllers.RelayController;
using Halloween.Director.Scarecrow.Meadow.Services.MapleService;
using Halloween.Director.Shared;
using Halloween.Director.Shared.Services.DiagnosticsService;
using Halloween.Director.Shared.Services.NetworkService;
using Halloween.Director.Shared.Services.Watchdog;

namespace Halloween.Director.Scarecrow.Meadow
{
    public class MeadowApp : MeadowBase
    {
        private ILEDDeviceController _ledDevice;

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

        private void NetworkConnected(INetworkAdapter sender, NetworkConnectionEventArgs args)
        {
            var networkService = Services.Get<NetworkService>();
            networkService.NetworkIsConnected(sender);
            
            var mapleService = Services.Get<MapleService>();
            mapleService.Run();

            var ledDeviceController = Services.Get<ILEDDeviceController>();
            ledDeviceController.StartBlink(Color.Green);
        }
    }
}
