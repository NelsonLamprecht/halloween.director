using Meadow;
using Meadow.Foundation.Web.Maple;
using Meadow.Foundation.Web.Maple.Routing;

namespace Halloween.Director.Shared.Controllers
{
    /// <summary>
    /// GET /info on every prop: its name, kind and version as JSON. Maple finds request handlers by
    /// scanning the loaded assemblies, so this one is picked up in each app just by referencing the
    /// shared library.
    /// </summary>
    public class InfoRequestHandler : RequestHandlerBase
    {
        // a fresh handler per request, like the apps' own handlers (see agents.md)
        public override bool IsReusable => false;

        [HttpGet("/info")]
        public IActionResult Info()
        {
            var name = Resolver.Device.Information.DeviceName;
            Resolver.Log.Info($"Info request: {name}, {DeviceInfo.Type}, {DeviceInfo.Version}.");

            return new ContentResult(DeviceInfo.ToJson(name), "application/json");
        }
    }
}
