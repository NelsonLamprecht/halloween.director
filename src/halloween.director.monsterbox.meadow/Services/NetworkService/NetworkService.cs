using Meadow.Hardware;
using Meadow.Logging;

namespace halloween.director.monsterbox.meadow.Services.NetworkService
{
    internal class NetworkService: BaseService
    {
        private readonly DiagnosticsService.DiagnosticsService diagnosticsService;

        public NetworkService(Logger logger,
            DiagnosticsService.DiagnosticsService diagnosticsService) : base(logger)
        {
            this.diagnosticsService = diagnosticsService;
        }

        public void NetworkIsConnected(INetworkAdapter sender)
        {
            diagnosticsService.OutputDeviceWifiInfo(sender);
        }
    }
}
