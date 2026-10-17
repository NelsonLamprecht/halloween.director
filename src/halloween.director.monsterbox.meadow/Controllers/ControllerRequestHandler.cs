using System;
using System.Net;
using System.Threading.Tasks;

using Meadow;
using Meadow.Foundation.Web.Maple;
using Meadow.Foundation.Web.Maple.Routing;

using Halloween.Director.Shared.Controllers;

namespace Halloween.Director.Monsterbox.Meadow.Controllers
{
    public class ControllerRequestHandler : RequestHandlerBase
    {
        private readonly MP3Controller _mp3Controller;
        private readonly CylindersController _cylindersController;
        private readonly LedController _ledController;

        // Maple constructs this handler itself (outside the DI container), so resolve
        // dependencies from the global registry here. Safe because MapleService only
        // starts after MeadowApp.Initialize() has registered these controllers.
        public ControllerRequestHandler()
        {
            _mp3Controller = Resolver.Services.Get<MP3Controller>();
            _cylindersController = Resolver.Services.Get<CylindersController>();
            _ledController = Resolver.Services.Get<LedController>();
        }

        // Maple runs requests in parallel, and per-request state (QueryString,
        // Context) lives on the handler instance — a shared instance would let two
        // concurrent requests overwrite each other's query string. A fresh handler
        // per request costs only the two lookups above.
        public override bool IsReusable => false;

        [HttpPost("/sound")]
        public IActionResult Sound()
        {
            if (!byte.TryParse(QueryString["filenumber"], out var fileNumber) || fileNumber > MP3Controller.MaxFileNumber)
            {
                Resolver.Log.Warn($"Sound request missing or invalid 'filenumber' (0-{MP3Controller.MaxFileNumber}).");
                return new StatusCodeResult(HttpStatusCode.BadRequest);
            }

            try
            {
                _mp3Controller.QueueFile(fileNumber);
            }
            catch (Exception ex)
            {
                Resolver.Log.Error($"Sound failed: {ex.Message}");
                return new ServerErrorResult();
            }

            // the command was accepted: pulse so it's visible the board got it
            _ledController.SignalActivity();
            return new OkResult();
        }

        // Responds when the shake has finished: 200 OK when done, 409 Conflict
        // immediately if a shake is already running, 500 if the shake failed.
        [HttpPost("/shake")]
        public async Task<IActionResult> ShakeAsync()
        {
            var config = new ShakeConfiguration();

            if (int.TryParse(QueryString["bi"], out var beginIterations))
            {
                config.BeginIterations = beginIterations;
            }

            if (int.TryParse(QueryString["ei"], out var endIterations))
            {
                config.EndIterations = endIterations;
            }

            if (int.TryParse(QueryString["bd"], out var beginDelay))
            {
                config.BeginDelay = beginDelay;
            }

            if (int.TryParse(QueryString["ed"], out var endDelay))
            {
                config.EndDelay = endDelay;
            }

            if (!config.TryValidate(out var validationError))
            {
                Resolver.Log.Warn($"Shake rejected: {validationError}");
                return new StatusCodeResult(HttpStatusCode.BadRequest);
            }

            // pulse for the whole shake and return to ready when it finishes, so the LED
            // shows the board received the command and when it can take another
            _ledController.BeginActivity();
            try
            {
                if (!await _cylindersController.TryShakeAsync(config))
                {
                    return new StatusCodeResult(HttpStatusCode.Conflict);
                }
            }
            catch (Exception ex)
            {
                Resolver.Log.Error($"Shake failed: {ex.Message}");
                return new ServerErrorResult();
            }
            finally
            {
                _ledController.EndActivity();
            }

            return new OkResult();
        }
    }
}
