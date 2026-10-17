using System.Threading.Tasks;

using Meadow;
using Meadow.Foundation.Web.Maple;
using Meadow.Hardware;
using Meadow.Logging;

namespace meadow_scarecrow.Services.MapleService
{
    internal class MapleService : BaseService
    {
        private readonly IMeadowDevice device;
        private readonly INetworkAdapter networkAdapter;
        private MapleServer mapleServer;

        public MapleService(Logger logger, IMeadowDevice device, INetworkAdapter networkAdapter) : base(logger)
        {
            this.device = device;
            this.networkAdapter = networkAdapter;
        }

        public override Task Run()
        {
            mapleServer = new MapleServer(
                networkAdapter.IpAddress,
                port: 5417,
                advertise: true, 
                processMode: RequestProcessMode.Parallel)
            {
                AdvertiseIntervalMs = 1500, // every 1.5 seconds
                DeviceName = device.Information.DeviceName
            };

            mapleServer.Start();
            return base.Run();
        }
    }
}
