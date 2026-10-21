using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using Meadow.Hardware;
using Meadow.Logging;

using Halloween.Director.Shared.Controllers;

namespace Halloween.Director.Shared.Services.HeartbeatService
{
    /// <summary>
    /// Follows the Meadow.Core.Samples "Config_Files" heartbeat: a background loop that logs
    /// the time and IP address every <see cref="DefaultIntervalSeconds"/> seconds, so the device log shows the app is alive
    /// and when it stopped or restarted. <see cref="Run"/> starts the loop and returns
    /// straight away, like the sample's <c>StartHeartbeat()</c>.
    ///
    /// Every <see cref="DefaultCheckIntervalSeconds"/> seconds it also checks that WiFi is
    /// connected with an IP address and the Maple server is running on it, and flashes the
    /// LED green when it is (<see cref="LedController.PulseHealthy"/>). If the flashes stop,
    /// one of those failed or the app has hung.
    /// </summary>
    public class HeartbeatService : BaseService, IDisposable
    {
        private readonly INetworkAdapter networkAdapter;
        private readonly MapleService.MapleService mapleService;
        private readonly LedController ledController;
        private readonly Stopwatch uptime = Stopwatch.StartNew();
        private CancellationTokenSource cancellationTokenSource;

        public HeartbeatService(Logger logger, INetworkAdapter networkAdapter, MapleService.MapleService mapleService, LedController ledController) : base(logger)
        {
            this.networkAdapter = networkAdapter;
            this.mapleService = mapleService;
            this.ledController = ledController;
        }

        /// <summary>How often the heartbeat is logged, for every prop. Change it here.</summary>
        public const int DefaultIntervalSeconds = 30;

        /// <summary>How often the health check runs and the LED flashes, for every prop.</summary>
        public const int DefaultCheckIntervalSeconds = 3;

        public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(DefaultIntervalSeconds);

        public TimeSpan CheckInterval { get; set; } = TimeSpan.FromSeconds(DefaultCheckIntervalSeconds);

        /// <summary>
        /// For testing Lifecycle.RestartOnAppFailure only: throws from the heartbeat task after
        /// this many beats, as the sample does ("Testing restart..."). Leave null on a prop.
        /// </summary>
        public int? TestRestartAfterBeats { get; set; }

        public override Task Run()
        {
            Logger.StartupSection("HeartbeatService.Run", () =>
            {
                if (cancellationTokenSource == null)
                {
                    cancellationTokenSource = new CancellationTokenSource();
                    var token = cancellationTokenSource.Token;
                    _ = Task.Run(() => BeatAsync(token));
                }

                Logger.StartupDetail($"Logging a heartbeat every {Interval.TotalSeconds}s; health check and LED flash every {CheckInterval.TotalSeconds}s.");
            });

            return base.Run();
        }

        private async Task BeatAsync(CancellationToken cancellationToken)
        {
            Logger.Trace("Heartbeat Task Started");
            var beats = 0;
            var sinceLastBeat = Stopwatch.StartNew();
            string lastProblem = null;
            var first = true;

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var problem = CheckHealth();

                    // log only when the result changes, so a long outage is one line
                    if (!first && problem != lastProblem)
                    {
                        if (problem == null)
                        {
                            Logger.Info("Health check OK.");
                        }
                        else
                        {
                            Logger.Warn($"Health check failed: {problem}.");
                        }
                    }
                    lastProblem = problem;

                    if (problem == null)
                    {
                        PulseLed();
                    }

                    if (first || sinceLastBeat.Elapsed >= Interval)
                    {
                        first = false;
                        sinceLastBeat.Restart();
                        beats++;
                        Logger.Info($"{DateTime.Now} {networkAdapter.IpAddress} (up {uptime.Elapsed:d\\.hh\\:mm\\:ss}) {problem ?? "healthy"}");

                        if (TestRestartAfterBeats.HasValue && beats >= TestRestartAfterBeats.Value)
                        {
                            throw new Exception("Testing restart...");
                        }
                    }

                    await Task.Delay(CheckInterval, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Dispose() was called; nothing to clean up
            }
        }

        // null when healthy, otherwise what is wrong
        private string CheckHealth()
        {
            if (!networkAdapter.IsConnected)
            {
                return "WiFi not connected";
            }

            var address = networkAdapter.IpAddress;
            if (address == null || IPAddress.Any.Equals(address))
            {
                return "no IP address";
            }

            if (!mapleService.IsRunning)
            {
                return "Maple server not running";
            }

            if (!address.Equals(mapleService.BoundAddress))
            {
                return $"Maple server is on {mapleService.BoundAddress}, not {address}";
            }

            return null;
        }

        // the LED must never stop the heartbeat
        private void PulseLed()
        {
            try
            {
                ledController.PulseHealthy();
            }
            catch (Exception ex)
            {
                Logger.Warn($"Health LED flash failed: {ex.Message}");
            }
        }

        public void Dispose()
        {
            cancellationTokenSource?.Cancel();
        }
    }
}
