using System;
using System.Net;

using Meadow;
using Meadow.Foundation.Web.Maple;
using Meadow.Foundation.Web.Maple.Routing;

using Halloween.Director.Shared.Controllers;

namespace Halloween.Director.Scarecrow.Meadow.Controllers
{
    public class ControllerRequestHandler : RequestHandlerBase
    {
        private readonly RelayController _relayController;
        private readonly LedController _ledController;

        // Maple constructs this handler itself (outside the DI container), so resolve
        // dependencies from the global registry here. Safe because MapleService only
        // starts after MeadowApp.Initialize() has registered these controllers.
        public ControllerRequestHandler()
        {
            _relayController = Resolver.Services.Get<RelayController>();
            _ledController = Resolver.Services.Get<LedController>();
        }

        // Maple runs requests in parallel, and per-request state (QueryString,
        // Context) lives on the handler instance, so a fresh handler per request
        // keeps concurrent requests from overwriting each other.
        public override bool IsReusable => false;

        [HttpPost("/up")]
        public IActionResult Up()
        {
            return Switch("Up", _relayController.TurnOn);
        }

        [HttpPost("/down")]
        public IActionResult Down()
        {
            return Switch("Down", _relayController.TurnOff);
        }

        // 200 OK when the relay switched, 500 if it failed. An accepted command blinks
        // the LED so it's visible the board got it.
        private IActionResult Switch(string name, Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Resolver.Log.Error($"Controller {name} failed: {ex.Message}");
                return new ServerErrorResult();
            }

            _ledController.SignalActivity();
            return new OkResult();
        }
    }
}
