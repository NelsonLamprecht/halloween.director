# Agent notes: shared architecture and patterns

Monorepo for the Halloween props. Open `src/halloween.director.sln`. Project-specific details are in each project's own `agents.md`; this file covers what the Meadow firmware projects share.

| Project | What it is |
| --- | --- |
| `halloween.director.shared` | netstandard2.1 library used by both firmware projects: `MeadowBase`, base controller/service, LED, relay, watchdog, heartbeat, diagnostics, network and Maple services, `LoggerExtensions`. |
| `halloween.director.monsterbox.meadow` | Meadow F7 firmware: two relays (cylinder shake) and an MP3 player. See its `agents.md`. |
| `halloween.director.scarecrow.meadow` | Meadow F7 firmware: one relay driving a scissor lift. See its `agents.md`. |
| `halloween.director.remote` | .NET MAUI app that controls the props over HTTP. Not a Meadow project (needs the .NET 10 SDK and MAUI workload). |

Namespaces are `Halloween.Director.*` (project and folder names stay lowercase). Both firmware projects are `OutputType` `Library` with `AssemblyName` `App` and **no `Program.cs`**: Meadow OS finds the `App` subclass itself.

## Composition root: `MeadowApp` + `MeadowBase`

`MeadowBase : App<F7FeatherV1>` (shared) gives each app `Logger`, `Services` (the `Resolver.Services` collection), `BootStopwatch` and these helpers, so apps don't repeat them:

- `ApplyStartupSettings(defaultDeviceName)` reads `App.StartupDetails` and `App.DeviceName` from `app.config.yaml`.
- `LogWifiConfig` / `LogWifiEvents` log the WiFi config and the connecting/failed/disconnected/error events.
- `OnNetworkConnected` is the `NetworkConnected` handler (see below).
- `OnShutdown` logs and calls `DisposeServices()`; apps override that and list what to release with `DisposeIfRegistered<T>()`.
- `OnError` logs unhandled errors.

Each `MeadowApp` runs a fixed lifecycle:

1. **`Initialize()`**: apply settings, create diagnostics, set up WiFi, then `Services.Create<T>()` every controller/service and call `Initialize(...)` on the ones that own hardware (two-phase construction is deliberate: initialization order is explicit here, not hidden in constructors).
2. **`Run()`**: start the watchdog (`IWatchdogService.Start()`) and the heartbeat, then wait for the network.
3. **`OnNetworkConnected`** (shared): anything needing an IP is deferred to here. It runs the network diagnostics, starts `MapleService` (or re-binds it if DHCP changed the IP, using `BoundAddress`), sets the LED to ready and logs startup complete. It is `async void` on purpose, with a try/catch that logs a failed start; `_networkServicesStarted` is only set after a successful start, so the next event retries.

WiFi itself is not hand-rolled: `meadow.config.yaml` (`Coprocessor.AutomaticallyStartNetwork`/`AutomaticallyReconnect`) plus a gitignored `wifi.config.yaml` (copy `wifi.config.yaml.template`) connect it.

## Controllers vs. services, and DI

- **Controllers** are hardware-shaped (`LedController`, relay controllers, MP3, the scarecrow's `LiftController`). **Services** are app concerns (`MapleService`, `NetworkService`, `DiagnosticsService`, `WatchdogService`, `HeartbeatService`). Both derive from `BaseController` / `BaseService`: a `Logger` and a `virtual Task Run()` that defaults to a no-op.
- `IOnOrOffController` (`TurnOn`/`TurnOff`) is the contract for every switchable output (relays, LED). "On" means active, whatever the electrical polarity.
- `Services.Create<T>()` builds `T` and resolves its constructor parameters from already-registered instances; `Create<TImpl, TInterface>()` registers under an interface; `Add(instance)` registers something we didn't build (the WiFi adapter, options objects); `Get<T>()` retrieves; `ContainsRegisteredType<T>()` guards shutdown paths. Resolution is purely by registered instance type, so **register a dependency before anything that needs it**.
- **One sanctioned exception**: `ControllerRequestHandler : RequestHandlerBase` is created by Maple outside the container, so it resolves its controllers from `Resolver.Services` in its constructor. Don't "fix" that.
- Named onboard pins (`OnboardLedRed/Green/Blue`) only exist on `F7FeatherV1`, so `OnboardRgbLed` casts internally and every method no-ops on other devices. Elsewhere prefer passing generic `IPin`s from `MeadowApp.Initialize()` (which has the typed `Device`) into `Initialize(IPin ...)`.

## Shared pieces

- **`OnboardRgbLed` / `LedController`**: the onboard LED is common anode (PWM inverted). Red until the Maple server is up, then `ReadyColor` (green). Accepted commands call `BeginActivity()`/`EndActivity()` (counted, because requests run in parallel): a hard white blink (`BlinkOn`/`BlinkOff`) for at least `MinActivityBlink`, then back to ready. `SignalActivity()` is begin + end for instant commands.
- **`RelayOutput`**: one relay on an open-drain pin (initial state `true`, so it is off as fast as possible at boot; open-drain is required when the F7 is powered from the relay board). The caller passes which `RelayState` means "on". `Dispose()` turns it off before releasing the pin.
- **`WatchdogService`**: `Start()` arms it (`DefaultTimeoutSeconds` 15) and starts an independent thread that pets it every `DefaultPetIntervalSeconds` (10), so a slow request handler can't trip it.
- **`HeartbeatService`**: logs `<time> <ip> (up d.hh:mm:ss)` every `DefaultIntervalSeconds` (30), like the Meadow.Core.Samples `Config_Files` heartbeat. `TestRestartAfterBeats` throws on purpose to test restarts; never set it on a prop.
- **`MapleService`** + `MapleServiceOptions`: both props listen on Maple's default port `5417` (they run on separate devices) and advertise every 2s. `RequestProcessMode.Parallel`; handlers must keep `IsReusable => false` because Maple keeps per-request state (`QueryString`) on the handler instance. Choose Serial vs Parallel by whether your commands can tolerate concurrency.
  **Route methods must return `IActionResult` synchronously, never `Task<IActionResult>`.** Maple 2.5.0 only awaits `IActionResult` handlers: for an async one it replies with an empty `200` immediately (so 400/409/500 never reach the client) and swallows exceptions. To wait on async work (the monsterbox `/shake`), block with `.GetAwaiter().GetResult()`; the thread-pool thread and Meadow's lack of a synchronization context make that safe.
  **`QueryString` is `null` when the URL has no `?`** (Maple only creates it then), so read parameters null-safely (`QueryString?["name"]`); a plain `POST /shake` otherwise throws a NullReferenceException.

## Logging

- Use `Logger.StartupSection("Title", () => ...)` for a startup step (logs `Title:Begin` / `Title:End` banners around the work) and `Logger.StartupDetail("...")` for extra lines. Both are controlled by `App.StartupDetails` in `app.config.yaml` (true while developing, false for production); the work inside a section always runs.
- Controllers log `"<Name> is initialized."` at Information. Request handlers log each accepted command at Information, warnings for rejected ones and errors for failures.

## Config files

- `app.config.yaml` (per app): `Lifecycle` (`RestartOnAppFailure`, `AppFailureRestartDelaySeconds`; note Meadow 2.5 reads `RestartOnAppFailure`, not `ResetOnAppFailure`), `Logging`, and an `App:` block (`DeviceName`, `StartupDetails`, plus project-specific keys). Read with `Settings["App.Key"]`.
- `meadow.config.yaml`: the two projects use identical files (auto start/reconnect network, NTP, DNS).
- `app.build.yaml`: `Deploy: NoLink: [ halloween.director.shared ]`. Meadow trims dependency assemblies on deploy and would strip constructors that are only reached by reflection (`Services.Create<T>()`). **Any new assembly that holds DI-created types must be added here.**
- Meadow package versions live in `src/Directory.Packages.props` (`MeadowVersion`). The MAUI app opts out with its own `Directory.Packages.props`.

## Adding a controller or service

1. Subclass `BaseController` or `BaseService`; the constructor takes `Logger` (pass to `base(logger)`) plus whatever else it needs.
2. If it owns hardware, give it a separate `Initialize(...)` and call it from `MeadowApp.Initialize()` after `Services.Create<T>()`. Wrap the work in `Logger.StartupSection`.
3. Register it in dependency order.
4. If it owns hardware or threads, implement `IDisposable` and add `DisposeIfRegistered<T>()` to the app's `DisposeServices()` (before anything it depends on).
5. If it can be shared by both props, put it in `halloween.director.shared`.
6. Keep chaos/testing code out of builds that control hardware.
