using System;
using System.Threading;

using Meadow;
using Meadow.Logging;

namespace Halloween.Director.Shared.Services.Watchdog
{
    public class WatchdogService : BaseService, IWatchdogService
    {
        /// <summary>The device resets if the watchdog isn't petted within this many seconds.</summary>
        public const int DefaultTimeoutSeconds = 15;

        /// <summary>How often the background thread pets the watchdog.</summary>
        public const int DefaultPetIntervalSeconds = 10;

        private readonly IMeadowDevice device;

        public WatchdogService(Logger logger, IMeadowDevice device) : base(logger)
        {
            this.device = device;
        }

        public void Start()
        {
            Enable(DefaultTimeoutSeconds);
            Pet(DefaultPetIntervalSeconds);
        }

        public void Enable(int inSeconds)
        {
            Logger.StartupSection("WatchdogService.Enable", () =>
            {
                device.WatchdogEnable(TimeSpan.FromSeconds(inSeconds));
                Logger.StartupDetail($"Device resets if the watchdog isn't petted within {inSeconds}s.");
            });
        }

        public void Pet(int inSeconds)
        {
            Logger.StartupSection("WatchdogService.Pet", () =>
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
                Logger.StartupDetail($"Petting the watchdog every {inSeconds}s.");
            });
        }
    }
}
