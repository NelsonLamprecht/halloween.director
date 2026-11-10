using System.Threading.Tasks;

using Meadow.Logging;

namespace FieldKit.Meadow.Controllers
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
