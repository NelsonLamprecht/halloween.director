using System;
using System.Net;

using Meadow;
using Meadow.Foundation.Web.Maple;
using Meadow.Foundation.Web.Maple.Routing;
using Meadow.Hardware;

using Halloween.Director.Shared.Services.HeartbeatService;

namespace Halloween.Director.Shared.Controllers
{
    /// <summary>
    /// GET /info on every prop: its name, kind, version, IP address, uptime and health as JSON
    /// (see <see cref="DeviceInfo"/>). Maple finds request handlers by scanning the loaded
    /// assemblies, so this one is picked up in each app just by referencing the shared library.
    /// </summary>
    public class InfoRequestHandler : RequestHandlerBase
    {
        private readonly HeartbeatService _heartbeatService;

        public InfoRequestHandler()
        {
            // created before MapleService starts (see MeadowBase.Initialize)
            _heartbeatService = Resolver.Services.Get<HeartbeatService>();
        }

        // a fresh handler per request, like the apps' own handlers (see agents.md)
        public override bool IsReusable => false;

        [HttpGet("/info")]
        public IActionResult Info()
        {
            var name = Resolver.Device.Information.DeviceName;
            string ip = null;
            long uptimeSeconds = 0;
            string health;

            // /info is how the remote finds a prop, so a failed lookup is reported, not a 500
            try
            {
                var address = Resolver.Device.NetworkAdapters.Primary<IWiFiNetworkAdapter>()?.IpAddress;
                if (address != null && !IPAddress.Any.Equals(address))
                {
                    ip = address.ToString();
                }
                uptimeSeconds = (long)_heartbeatService.Uptime.TotalSeconds;
                health = _heartbeatService.CheckHealth() ?? "healthy";
            }
            catch (Exception ex)
            {
                Resolver.Log.Warn($"Info request: status lookup failed: {ex.Message}");
                health = $"status lookup failed: {ex.Message}";
            }

            Resolver.Log.Info($"Info request: {name}, {DeviceInfo.Type}, {DeviceInfo.Version}, {ip}, up {uptimeSeconds}s, {health}.");

            return new ContentResult(DeviceInfo.ToJson(name, ip, uptimeSeconds, health), "application/json");
        }
    }
}
