using halloween.director.shared.Services;
using System;
using System.Net;
using System.Threading.Tasks;

using Meadow;
using Meadow.Foundation.Web.Maple;
using Meadow.Hardware;
using Meadow.Logging;

namespace halloween.director.monsterbox.meadow.Services.MapleService
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

        // the address the running server is bound to, or null if it isn't running
        public IPAddress BoundAddress { get; private set; }

        // Starts the server on the adapter's current IP. Calling it again stops the
        // existing server first, so it can be used to re-bind after the IP changes.
        public override Task Run()
        {
            StopServer();

            var address = networkAdapter.IpAddress;
            mapleServer = new MapleServer(
                address,
                port: 5417,
                advertise: true,
                // Parallel so a /sound isn't queued behind a /shake that holds its
                // request open until the shake finishes; see AGENTS.md for why this is safe
                processMode: RequestProcessMode.Parallel)
            {
                AdvertiseIntervalMs = 5000, // every 5 seconds
                DeviceName = device.Information.DeviceName
            };

            mapleServer.Start();
            BoundAddress = address;
            return base.Run();
        }

        private void StopServer()
        {
            if (mapleServer == null)
            {
                return;
            }

            try
            {
                mapleServer.Stop();
            }
            catch (Exception ex)
            {
                // the old address is gone anyway; keep going and bind the new one
                Logger.Warn($"Stopping previous MapleServer failed: {ex.Message}");
            }

            mapleServer = null;
            BoundAddress = null;
        }
    }
}
