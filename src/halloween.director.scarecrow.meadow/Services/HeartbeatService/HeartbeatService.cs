using System;
using System.Threading.Tasks;

using Meadow.Logging;

using Halloween.Director.Shared.Services;

namespace Halloween.Director.Scarecrow.Meadow.Services.HeartbeatService
{
    // right now, used to test restarts
    internal class HeartbeatService : BaseService
    {
        public HeartbeatService(Logger logger) : base(logger)
        {

        }

        public override async Task Run()
        {
            Logger.Debug($"+StartHeartbeat");

            await Task.Run(async () =>
            {
                Logger.Trace($"Heartbeat Task Started");
                var countToReset = 1;

                while (true)
                {
                    Logger.Debug($"Count to reset: {countToReset}");
                    await Task.Delay(TimeSpan.FromSeconds(10));

                    Logger.Trace($"Testing for throw");
                    if (--countToReset <= 0) throw new Exception("Testing restart...");

                }
            });
        }
    }
}
