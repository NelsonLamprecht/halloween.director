using System;
using System.Threading;

using Meadow;
using Meadow.Logging;

namespace meadow_scarecrow.Services.Watchdog
{
    internal class WatchdogService : BaseService, IWatchdogService
    {
        private readonly IMeadowDevice device;

        public WatchdogService(Logger logger, IMeadowDevice device) : base(logger)
        {
            this.device = device;
        }
        
        public void Enable(int inSeconds)
        {
            device.WatchdogEnable(TimeSpan.FromSeconds(inSeconds));
        }

        public void Pet(int inSeconds)
        {
            // Just for good measure, let's reset the watchdog to begin with.
            device.WatchdogReset();
            // Start a thread that restarts it.
            Thread t = new Thread(() => {
                while (true)
                {
                    Thread.Sleep(TimeSpan.FromSeconds(inSeconds));
                    Logger.Debug($"Petting watchdog @ {DateTime.UtcNow}");
                    device.WatchdogReset();
                }
            });
            t.Start();
        }
    }
}
