using System;
using System.Net;
using System.Threading.Tasks;

using Meadow;
using Meadow.Foundation.Web.Maple;
using Meadow.Foundation.Web.Maple.Routing;
using Meadow.Logging;

using Halloween.Director.Scarecrow.Meadow.Controllers.LiftController;

namespace Halloween.Director.Scarecrow.Meadow
{
    public class ControllerRequestHandler : RequestHandlerBase
    {
        public ControllerRequestHandler()
        {
        }

        public override bool IsReusable => true;

        private Logger Logger { 
            get
            {
                return Resolver.Log;
            }
        }

        private LiftController LiftController
        {
            get
            {
                return Resolver.Services.Get<LiftController>();
            }
        }

        // Responds once the lift is up: 200 OK when up, 409 Conflict immediately
        // if the lift is still falling, 500 if raising failed.
        [HttpPost("/up")]
        public async Task<IActionResult> UpAsync()
        {
            Logger.Debug("Controller Up");
            try
            {
                if (!await LiftController.TryRaiseAsync())
                {
                    return new StatusCodeResult(HttpStatusCode.Conflict);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
                return new ServerErrorResult();
            }
            return new OkResult();
        }

        // Responds once the lift is down: 200 OK when down, 409 Conflict immediately
        // if the lift is still rising, 500 if lowering failed.
        [HttpPost("/down")]
        public async Task<IActionResult> DownAsync()
        {
            Logger.Debug("Controller Down");
            try
            {
                if (!await LiftController.TryLowerAsync())
                {
                    return new StatusCodeResult(HttpStatusCode.Conflict);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
                return new ServerErrorResult();
            }
            return new OkResult();
        }
    }
}
