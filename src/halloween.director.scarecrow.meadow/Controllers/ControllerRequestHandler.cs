using System;

using Meadow;
using Meadow.Foundation.Web.Maple;
using Meadow.Foundation.Web.Maple.Routing;

using Halloween.Director.Shared.Controllers;

namespace Halloween.Director.Scarecrow.Meadow.Controllers
{
    public class ControllerRequestHandler : RequestHandlerBase
    {
        private readonly LiftController _liftController;
        private readonly LedController _ledController;

        // Maple constructs this handler itself (outside the DI container), so resolve
        // dependencies from the global registry here. Safe because MapleService only
        // starts after MeadowApp.Initialize() has registered these controllers.
        public ControllerRequestHandler()
        {
            _liftController = Resolver.Services.Get<LiftController>();
            _ledController = Resolver.Services.Get<LedController>();
        }

        // Maple runs requests in parallel, and per-request state (QueryString,
        // Context) lives on the handler instance, so a fresh handler per request
        // keeps concurrent requests from overwriting each other.
        public override bool IsReusable => false;

        [HttpPost("/up")]
        public IActionResult Up()
        {
            return Switch("Up", () => _liftController.Up());
        }

        [HttpPost("/down")]
        public IActionResult Down()
        {
            return Switch("Down", () => _liftController.Down());
        }

        // 200 OK when the lift moved (or was already there), 500 if the relay failed. An accepted command blinks
        // the LED so it's visible the board got it.
        private IActionResult Switch(string name, Action action)
        {
            Resolver.Log.Info($"Controller {name} request.");

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
