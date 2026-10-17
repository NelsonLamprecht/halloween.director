using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using Meadow.Logging;

namespace Halloween.Director.Shared.Services.HeartbeatService
{
    /// <summary>
    /// Logs a "still alive" line with the uptime on a fixed interval, so the device log
    /// shows when the app stopped or restarted. <see cref="Run"/> starts the loop in the
    /// background and returns straight away.
    /// </summary>
    public class HeartbeatService : BaseService, IDisposable
    {
        private readonly Stopwatch uptime = Stopwatch.StartNew();
        private CancellationTokenSource cancellationTokenSource;

        public HeartbeatService(Logger logger) : base(logger)
        {
        }

        public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(60);

        public override Task Run()
        {
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
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(Interval, cancellationToken);
                    Logger.Info($"Heartbeat: up {uptime.Elapsed:d\\.hh\\:mm\\:ss}");
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
