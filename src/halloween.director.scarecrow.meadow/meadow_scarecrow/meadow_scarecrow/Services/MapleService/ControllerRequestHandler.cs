using System;
using System.Threading.Tasks;

using Meadow;
using Meadow.Foundation.Web.Maple;
using Meadow.Foundation.Web.Maple.Routing;
using Meadow.Logging;

using meadow_scarecrow.Controllers.RelayController;

namespace meadow_scarecrow
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

        private RelayController RelayController
        {
            get
            {
                return Resolver.Services.Get<RelayController>();
            }
        }


        [HttpPost("/up")]
        public async Task<IActionResult> UpAsync()
        {
            Logger.Debug("Controller Up");
            try
            {
                RelayController.TurnOn();
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
            }
            return new OkResult();
        }

        [HttpPost("/down")]
        public async Task<IActionResult> DownAsync()
        {
            Logger.Debug("Controller Down");
            try
            {
                RelayController.TurnOff();
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
            }
            return new OkResult();
        }
    }
}
