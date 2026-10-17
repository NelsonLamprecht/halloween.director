using Meadow.Hardware;
using Meadow.Logging;

namespace meadow_scarecrow.Services.NetworkService
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
