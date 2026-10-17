using System;
using System.Net;
using System.Threading.Tasks;

using Meadow;
using Meadow.Foundation.Web.Maple;
using Meadow.Hardware;
using Meadow.Logging;

namespace Halloween.Director.Shared.Services.MapleService
{
    /// <summary>
    /// Runs the Maple HTTP server on the network adapter's current IP and advertises it.
    /// Maple finds the request handlers by scanning the loaded assemblies, so the app's
    /// own <c>RequestHandlerBase</c> classes are picked up even though the server is
    /// created here.
    /// </summary>
    public class MapleService : BaseService
    {
        private readonly IMeadowDevice device;
        private readonly INetworkAdapter networkAdapter;
        private readonly MapleServiceOptions options;
        private MapleServer mapleServer;

        public MapleService(Logger logger, IMeadowDevice device, INetworkAdapter networkAdapter, MapleServiceOptions options) : base(logger)
        {
            this.device = device;
            this.networkAdapter = networkAdapter;
            this.options = options;
        }

        // the address the running server is bound to, or null if it isn't running
        public IPAddress BoundAddress { get; private set; }

        // Starts the server on the adapter's current IP. Calling it again stops the
        // existing server first, so it can be used to re-bind after the IP changes.
        public override Task Run()
        {
            Logger.StartupSection("MapleService.Run", () =>
            {
                StopServer();

                var address = networkAdapter.IpAddress;
                mapleServer = new MapleServer(
                    address,
                    port: options.Port,
                    advertise: true,
                    processMode: options.ProcessMode)
                {
                    AdvertiseIntervalMs = options.AdvertiseIntervalMs,
                    DeviceName = device.Information.DeviceName
                };

                mapleServer.Start();
                BoundAddress = address;
                Logger.StartupDetail($"Listening on http://{address}:{options.Port}/ ({options.ProcessMode}), advertising as '{device.Information.DeviceName}' every {options.AdvertiseIntervalMs}ms.");
            });

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
