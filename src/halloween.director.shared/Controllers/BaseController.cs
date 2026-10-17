using System.Threading.Tasks;

using Meadow.Logging;

namespace halloween.director.shared.Controllers
{
    public class BaseController
    {
        public BaseController(Logger logger)
        {
            Logger = logger;
        }

        public Logger Logger { get; }

        public virtual Task Run()
        {
            return Task.CompletedTask;
        }

    }
}
