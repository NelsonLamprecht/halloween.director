using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using Meadow.Hardware;
using Meadow.Logging;

namespace Halloween.Director.Shared.Services.HeartbeatService
{
    /// <summary>
    /// Follows the Meadow.Core.Samples "Config_Files" heartbeat: a background loop that logs
    /// the time and IP address every <see cref="DefaultIntervalSeconds"/> seconds, so the device log shows the app is alive
    /// and when it stopped or restarted. <see cref="Run"/> starts the loop and returns
    /// straight away, like the sample's <c>StartHeartbeat()</c>.
    /// </summary>
    public class HeartbeatService : BaseService, IDisposable
    {
        private readonly INetworkAdapter networkAdapter;
        private readonly Stopwatch uptime = Stopwatch.StartNew();
        private CancellationTokenSource cancellationTokenSource;

        public HeartbeatService(Logger logger, INetworkAdapter networkAdapter) : base(logger)
        {
            this.networkAdapter = networkAdapter;
        }

        /// <summary>How often the heartbeat is logged, for every prop. Change it here.</summary>
        public const int DefaultIntervalSeconds = 30;

        public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(DefaultIntervalSeconds);

        /// <summary>
        /// For testing Lifecycle.RestartOnAppFailure only: throws from the heartbeat task after
        /// this many beats, as the sample does ("Testing restart..."). Leave null on a prop.
        /// </summary>
        public int? TestRestartAfterBeats { get; set; }

        public override Task Run()
        {
            Logger.Debug("+StartHeartbeat");

            if (cancellationTokenSource == null)
            {
                cancellationTokenSource = new CancellationTokenSource();
                var token = cancellationTokenSource.Token;
                _ = Task.Run(() => BeatAsync(token));
            }

            return base.Run();
        }

        private async Task BeatAsync(CancellationToken cancellationToken)
        {
            Logger.Trace("Heartbeat Task Started");
            var beats = 0;

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    beats++;
                    Logger.Info($"{DateTime.Now} {networkAdapter.IpAddress} (up {uptime.Elapsed:d\\.hh\\:mm\\:ss})");
                    await Task.Delay(Interval, cancellationToken);

                    if (TestRestartAfterBeats.HasValue && beats >= TestRestartAfterBeats.Value)
                    {
                        throw new Exception("Testing restart...");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Dispose() was called; nothing to clean up
            }
        }

        public void Dispose()
        {
            cancellationTokenSource?.Cancel();
        }
    }
}
