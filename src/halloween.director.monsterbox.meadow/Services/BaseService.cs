using Meadow.Logging;
using System.Threading.Tasks;

namespace halloween.director.monsterbox.meadow.Services
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
