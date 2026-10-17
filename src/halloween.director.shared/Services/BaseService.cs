using System.Threading.Tasks;

using Meadow.Logging;

namespace Halloween.Director.Shared.Services
{
    public class BaseService: IRunableService
    {
        public BaseService(Logger logger)
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
