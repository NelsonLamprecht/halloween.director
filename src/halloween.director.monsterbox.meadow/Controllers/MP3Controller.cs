using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using Meadow;
using Meadow.Devices;
using Meadow.Foundation.Audio.Mp3;
using Meadow.Logging;

namespace halloween.director.monsterbox.meadow.Controllers
{
    internal class MP3Controller : BaseController, IDisposable
    {
        // Yx5300 module is wired to the Feather's COM4 UART
        private const string SerialPortName = "COM4";

        // give the module a moment to actually start, otherwise the first
        // GetStatus() call can still read Stopped from before Play() took effect
        private static readonly TimeSpan StartDelay = TimeSpan.FromMilliseconds(250);

        // how often to ask the module whether it's still playing
        private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(200);

        // a single GetStatus() that never completes shouldn't stall the worker forever
        private static readonly TimeSpan StatusTimeout = TimeSpan.FromSeconds(1);

        // safety net in case the module never reports a stop; the next queued file
        // plays (cutting this one off) once this elapses
        private static readonly TimeSpan MaxPlayDuration = TimeSpan.FromMinutes(1);

        // GetStatus() can misread (see the driver note in WaitForTrackToEnd), so only
        // treat the track as finished after several non-Playing reads in a row
        private const int RequiredNotPlayingReads = 2;

        // ...and stop waiting altogether if the reads keep failing, rather than
        // blocking the queue for the full MaxPlayDuration
        private const int MaxConsecutiveErrors = 3;

        // files are sent to the module as fileNumber + 1 in a single byte, so 255 would wrap to 0
        public const byte MaxFileNumber = 254;

        private readonly IMeadowDevice device;

        private Yx5300 _mp3Player;
        private bool initialized = false;

        // one-slot queue: the most recent request waiting for the current track to
        // finish. A newer request replaces an older pending one.
        private readonly object _queueLock = new object();
        private byte? _pendingFile;

        // released once per queued request to wake the playback worker
        private readonly SemaphoreSlim _wake = new SemaphoreSlim(0);

        // cancelled on Dispose() to stop the playback worker
        private readonly CancellationTokenSource _shutdown = new CancellationTokenSource();

        public MP3Controller(Logger logger, IMeadowDevice device) : base(logger)
        {
            this.device = device;
        }

        public void Initialize()
        {
            if (initialized)
            {
                return;
            }

            if (device is F7FeatherV1 f7Device)
            {
                _mp3Player = new Yx5300(f7Device, f7Device.PlatformOS.GetSerialPortName(SerialPortName));

                _mp3Player.SetVolume(30);

                // the worker is the only thing that talks to the module's UART from
                // here on, so Play() and GetStatus() can never collide
                _ = Task.Run(RunPlaybackWorker);
            }

            initialized = true;

            Logger.Info($"{GetType().Name} is initialized.");
        }

        // Returns immediately. The file plays as soon as the current track (if any)
        // finishes; if another file is already waiting, it's replaced by this one.
        public void QueueFile(byte fileNumber)
        {
            if (_mp3Player == null)
            {
                Logger.Warn($"MP3 player not initialized; ignoring request for file: {fileNumber}.");
                return;
            }

            if (fileNumber > MaxFileNumber)
            {
                Logger.Warn($"File number {fileNumber} is above the maximum of {MaxFileNumber}; ignoring.");
                return;
            }

            lock (_queueLock)
            {
                if (_pendingFile.HasValue)
                {
                    Logger.Info($"Replacing pending file {_pendingFile.Value} with file: {fileNumber}.");
                }
                _pendingFile = fileNumber;
            }

            _wake.Release();
        }

        private async Task RunPlaybackWorker()
        {
            var token = _shutdown.Token;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    await _wake.WaitAsync(token);

                    byte? next;
                    lock (_queueLock)
                    {
                        next = _pendingFile;
                        _pendingFile = null;
                    }

                    // extra wake-ups (several requests while one track played) find
                    // the slot already taken; just go back to waiting
                    if (!next.HasValue)
                    {
                        continue;
                    }

                    Logger.Info($"Playing file: {next.Value}.");
                    _mp3Player.Play((byte)(next.Value + 1));

                    await WaitForTrackToEnd(token);

                    Logger.Info($"Finished playing file: {next.Value}.");
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // never let the worker die, or sound stops working until reboot
                    Logger.Error($"Playback worker error: {ex.Message}");
                }
            }
        }

        private async Task WaitForTrackToEnd(CancellationToken token)
        {
            await Task.Delay(StartDelay, token);

            // wall-clock, so the time spent inside GetStatus() counts toward the cap
            var stopwatch = Stopwatch.StartNew();
            var notPlayingReads = 0;
            var consecutiveErrors = 0;

            while (stopwatch.Elapsed < MaxPlayDuration)
            {
                try
                {
                    var status = await GetStatusWithTimeout();
                    consecutiveErrors = 0;

                    if (status == Yx5300.PlayStatus.Playing)
                    {
                        notPlayingReads = 0;
                    }
                    else if (++notPlayingReads >= RequiredNotPlayingReads)
                    {
                        return;
                    }
                }
                catch (Exception ex)
                {
                    // the Yx5300 driver's serial response reader uses a fixed 12-byte
                    // buffer with no bounds check, and can throw IndexOutOfRangeException
                    // on a malformed/overlapping read (e.g. racing an unsolicited
                    // "finished" notification from the module)
                    if (++consecutiveErrors >= MaxConsecutiveErrors)
                    {
                        Logger.Warn($"GetStatus() failed {consecutiveErrors} times in a row; assuming track ended. Last error: {ex.Message}");
                        return;
                    }
                }

                await Task.Delay(PollInterval, token);
            }

            Logger.Warn($"Track still reported playing after {MaxPlayDuration.TotalSeconds}s; moving on.");
        }

        private async Task<Yx5300.PlayStatus> GetStatusWithTimeout()
        {
            // the timeout timer is cancelled as soon as the status arrives, rather than
            // left running for the full StatusTimeout on every poll
            using (var timeoutCts = new CancellationTokenSource())
            {
                var statusTask = _mp3Player.GetStatus();
                if (await Task.WhenAny(statusTask, Task.Delay(StatusTimeout, timeoutCts.Token)) != statusTask)
                {
                    throw new TimeoutException("GetStatus() timed out.");
                }

                timeoutCts.Cancel();
                return await statusTask;
            }
        }

        public void Dispose()
        {
            _shutdown.Cancel();
            (_mp3Player as IDisposable)?.Dispose();
        }
    }
}
