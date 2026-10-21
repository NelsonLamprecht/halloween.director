using System;
using System.Diagnostics;
using System.Threading.Tasks;

using Meadow;
using Meadow.Devices;
using Meadow.Hardware;
using Meadow.Logging;

using Halloween.Director.Shared.Controllers;
using Halloween.Director.Shared.Services.DiagnosticsService;
using Halloween.Director.Shared.Services.HeartbeatService;
using Halloween.Director.Shared.Services.MapleService;
using Halloween.Director.Shared.Services.NetworkService;
using Halloween.Director.Shared.Services.Watchdog;

namespace Halloween.Director.Shared
{
    /// <summary>
    /// The boot sequence every prop shares. <see cref="Initialize"/> brings up the LED,
    /// diagnostics, WiFi and the watchdog, calls <see cref="InitializeProp"/> for the prop's
    /// own hardware, then creates the network services. <see cref="Run"/> starts the watchdog
    /// and heartbeat and waits for WiFi; <see cref="OnNetworkConnected"/> starts Maple.
    /// </summary>
    public abstract class MeadowBase: App<F7FeatherV1>
    {
        protected Logger Logger { get; } = Resolver.Log;

        protected ServiceCollection Services { get; } = Resolver.Services;

        /// <summary>Time since the app object was created, for the "@ 00:00:04" log timestamps.</summary>
        protected Stopwatch BootStopwatch { get; } = Stopwatch.StartNew();

        public MeadowBase() { }

        /// <summary>Used if app.config.yaml is missing or doesn't have an App.DeviceName entry.</summary>
        protected abstract string DefaultDeviceName { get; }

        /// <summary>What GET /info reports as the type, such as "monsterbox".</summary>
        protected abstract string DeviceType { get; }

        /// <summary>
        /// Creates and initializes the prop's own controllers (relays, MP3, lift...). Runs after
        /// the LED, diagnostics, WiFi adapter and watchdog exist, and before the network
        /// services, so anything a Maple handler resolves is registered by then.
        /// </summary>
        protected abstract void InitializeProp();

        public override async Task Initialize()
        {
            Logger.Info("=== Initializing hardware... ===");

            ApplyStartupSettings(DefaultDeviceName, DeviceType);

            // first, so the LED shows the app has started (yellow, then red; see LedStatus)
            var ledController = Services.Create<LedController>();
            ledController.Initialize();

            var diagnosticsService = Services.Create<DiagnosticsService>();
            diagnosticsService.OutputMeadowOSInfo();
            diagnosticsService.OutputDeviceInfo();
            diagnosticsService.OutputNtpInfo();

            ledController.SetStatus(LedStatus.Initializing);

            var wifi = Device.NetworkAdapters.Primary<IWiFiNetworkAdapter>();
            Logger.StartupSection("WiFi", () =>
            {
                LogWifiConfig(wifi);

                // every prop runs on the external antenna; persisted so it's already in
                // effect before AutomaticallyStartNetwork connects on every boot after the first
                wifi.SetAntenna(AntennaType.External, true);
                Services.Add(wifi);
                Logger.StartupDetail("Antenna set to External; waiting for the network to connect.");
            });

            Logger.StartupSection("Watchdog", () =>
            {
                Services.Create<WatchdogService, IWatchdogService>();
            });

            InitializeProp();

            Logger.StartupSection("Network services", () =>
            {
                Services.Create<HeartbeatService>();
                Services.Create<NetworkService>();
                Services.Add(new MapleServiceOptions());
                Services.Create<MapleService>();
                Logger.StartupDetail("Heartbeat, Network and Maple services created.");
            });

            TrackWifiEvents(wifi);
            wifi.NetworkConnected += OnNetworkConnected;

            Logger.Info("=== Hardware initialized. ===");

            await base.Initialize();
        }

        public override async Task Run()
        {
            try
            {
                Services.Get<IWatchdogService>().Start();

                await Services.Get<HeartbeatService>().Run();

                // network connection is handled by Meadow OS via wifi.config.yaml and
                // AutomaticallyStartNetwork in meadow.config.yaml; see OnNetworkConnected
                Logger.Info($"=== Running @ {BootStopwatch.Elapsed}. Waiting for WiFi connection... ===");
                ShowWaitingForWifi();
            }
            catch (Exception ex)
            {
                // stays solid red: the board won't go on to WiFi and Maple
                Services.Get<LedController>().SetColor(Color.Red);
                Logger.Error($"Run failed @ {BootStopwatch.Elapsed}: {ex}");
            }

            await base.Run();
        }

        // Logs what wifi.config.yaml and meadow.config.yaml gave the adapter.
        private void LogWifiConfig(IWiFiNetworkAdapter wifi)
        {
            Logger.Info(
                $"WiFi config from wifi.config.yaml -> DefaultSsid: '{wifi.DefaultSsid}', " +
                $"AutoConnect: {wifi.AutoConnect}, AutoReconnect: {wifi.AutoReconnect}");
        }

        // Logs the WiFi lifecycle events and shows them on the LED. NetworkConnected is
        // handled by OnNetworkConnected, which starts the network services.
        private void TrackWifiEvents(IWiFiNetworkAdapter wifi)
        {
            var led = Services.Get<LedController>();

            wifi.NetworkConnecting += (sender) =>
            {
                Logger.Info($"WiFi connecting @ {BootStopwatch.Elapsed}...");
                // a retry after a failure stays orange until it actually connects
                led.SetStatus(LedStatus.Initializing, LedStatus.WaitingForWifi);
            };
            wifi.NetworkConnectFailed += (sender) =>
            {
                Logger.Warn($"WiFi connect failed @ {BootStopwatch.Elapsed}.");
                led.SetStatus(LedStatus.WifiRetrying);
            };
            wifi.NetworkDisconnected += (sender, args) =>
            {
                Logger.Warn($"WiFi disconnected @ {BootStopwatch.Elapsed}. Reason: {args.Reason}");
                led.SetStatus(LedStatus.WifiRetrying);
            };
            wifi.NetworkError += (sender, args) =>
                Logger.Error($"WiFi network error @ {BootStopwatch.Elapsed}. ErrorCode: {args.ErrorCode}");
        }

        // Once initialization is done, the LED goes from red to the slow blue "waiting for
        // WiFi" blink, unless WiFi has already moved it on.
        private void ShowWaitingForWifi()
        {
            Services.Get<LedController>().SetStatus(LedStatus.Initializing, LedStatus.WaitingForWifi);
        }

        private bool _networkServicesStarted;

        // The NetworkConnected handler. It runs the network diagnostics, then starts the Maple
        // server, then turns the LED to ready (cyan while starting, purple if it fails).
        //
        // async void is deliberate: this is an event handler, and awaiting MapleService.Run()
        // lets a startup failure be caught and logged instead of vanishing in an unobserved task.
        private async void OnNetworkConnected(INetworkAdapter sender, NetworkConnectionEventArgs args)
        {
            Logger.StartupSection("WiFiConnected", () =>
                Logger.Info($"WiFi connected @ {BootStopwatch.Elapsed}. IP: {args.IpAddress}, Gateway: {args.Gateway}, Subnet: {args.Subnet}"));

            Services.Get<NetworkService>().NetworkIsConnected(sender);

            // AutomaticallyReconnect can raise this again after a drop. The Maple server is
            // bound to a specific IP, so leave it alone if the address is unchanged and
            // rebuild it if DHCP handed out a new one.
            var led = Services.Get<LedController>();
            var mapleService = Services.Get<MapleService>();
            if (_networkServicesStarted && args.IpAddress.Equals(mapleService.BoundAddress))
            {
                // reconnected after a drop; the server is still running
                led.SetStatus(LedStatus.Ready);
                return;
            }

            led.SetStatus(LedStatus.StartingServer);

            try
            {
                Logger.Info(_networkServicesStarted
                    ? $"IP changed to {args.IpAddress}; restarting MapleService..."
                    : "Starting MapleService...");
                await mapleService.Run();
            }
            catch (Exception ex)
            {
                // the next NetworkConnected event retries
                Logger.Error($"MapleService failed to start: {ex}");
                led.SetStatus(LedStatus.ServerFailed);
                return;
            }
            _networkServicesStarted = true;

            led.SetStatus(LedStatus.Ready);

            Logger.Info($"=== Startup complete @ {BootStopwatch.Elapsed}. ===");
        }

        // Logs the shutdown, stops the heartbeat, then DisposeServices() for the prop's own
        // controllers, and the LED last.
        public override Task OnShutdown()
        {
            Logger.Info($"=== Shutting down @ {BootStopwatch.Elapsed}... ===");

            DisposeIfRegistered<HeartbeatService>();
            DisposeServices();
            DisposeIfRegistered<LedController>();

            return base.OnShutdown();
        }

        // Override to dispose the prop's own controllers, in the order they must go
        // (e.g. whatever drives a relay before the relay itself) using DisposeIfRegistered.
        protected virtual void DisposeServices()
        {
        }

        // Shutdown can be reached before a service was ever created, so check first.
        protected void DisposeIfRegistered<T>() where T : class, IDisposable
        {
            if (Services.ContainsRegisteredType<T>())
            {
                Services.Get<T>().Dispose();
            }
        }

        public override Task OnError(Exception e)
        {
            Logger.Error($"Unhandled application error @ {BootStopwatch.Elapsed}: {e}");
            return base.OnError(e);
        }

        // Applies app.config.yaml's App settings: App.StartupDetails (default true) turns the
        // startup banners and detail lines on or off, and App.DeviceName names the device (the
        // name Maple advertises), falling back to defaultDeviceName if the setting is missing. deviceType (such as
        // "monsterbox") and the version are what GET /info reports.
        private void ApplyStartupSettings(string defaultDeviceName, string deviceType)
        {
            var version = BuildInfo.Describe(GetType().Assembly);
            Logger.Info($"{GetType().Assembly.GetName().Name} {version}");

            // what GET /info reports
            DeviceInfo.Type = deviceType;
            DeviceInfo.Version = version;

            if (Settings.TryGetValue("App.StartupDetails", out var startupDetails) && bool.TryParse(startupDetails, out var enabled))
            {
                LoggerExtensions.StartupDetails = enabled;
            }

            ApplyDeviceName(defaultDeviceName);
        }

        // Names the device from app.config.yaml's App.DeviceName (the name Maple advertises),
        // falling back to defaultName if the setting is missing.
        private void ApplyDeviceName(string defaultName)
        {
            if (Settings.TryGetValue("App.DeviceName", out var configuredDeviceName) && !string.IsNullOrWhiteSpace(configuredDeviceName))
            {
                Device.Information.DeviceName = configuredDeviceName;
            }
            else
            {
                Logger.Warn($"App.DeviceName not found in app.config.yaml; falling back to '{defaultName}'.");
                Device.Information.DeviceName = defaultName;
            }
            Logger.Info($"Device name: {Device.Information.DeviceName}");
        }
    }
}
