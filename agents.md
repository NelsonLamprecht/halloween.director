# Agent notes: shared architecture and patterns

Monorepo for the Halloween props. Open `src/halloween.director.sln`. Project-specific details are in each project's own `agents.md`; this file covers what the Meadow firmware projects share.

| Project | What it is |
| --- | --- |
| `halloween.director.shared` | netstandard2.1 library used by both firmware projects: `MeadowBase`, base controller/service, LED, relay, watchdog, heartbeat, diagnostics, network and Maple services, `LoggerExtensions`. |
| `halloween.director.monsterbox.meadow` | Meadow F7 firmware: two relays (cylinder shake) and an MP3 player. See its `agents.md`. |
| `halloween.director.scarecrow.meadow` | Meadow F7 firmware: one relay driving a scissor lift. See its `agents.md`. |
| `halloween.director.remote` | .NET MAUI app that controls the props over HTTP. Its sound buttons come from the monsterbox's `GET /sounds` (`SoundCatalogClient`), with a built-in fallback list. Not a Meadow project (needs the .NET 10 SDK and MAUI workload). |

Namespaces are `Halloween.Director.*` (project and folder names stay lowercase). Both firmware projects are `OutputType` `Library` with `AssemblyName` `App` and **no `Program.cs`**: Meadow OS finds the `App` subclass itself.

## Composition root: `MeadowApp` + `MeadowBase`

`MeadowBase : App<F7FeatherV1>` (shared) gives each app `Logger`, `Services` (the `Resolver.Services` collection), `BootStopwatch` and these helpers, so apps don't repeat them:

- `ApplyStartupSettings(defaultDeviceName)` reads `App.StartupDetails` and `App.DeviceName` from `app.config.yaml`.
- `LogWifiConfig` / `TrackWifiEvents` log the WiFi config and the connecting/failed/disconnected/error events; `TrackWifiEvents` also moves the LED to waiting or retrying (create `LedController` first). `ShowWaitingForWifi()` is called from `Run()` to switch the LED from red to waiting.
- `OnNetworkConnected` is the `NetworkConnected` handler (see below).
- `OnShutdown` logs and calls `DisposeServices()`; apps override that and list what to release with `DisposeIfRegistered<T>()`.
- `OnError` logs unhandled errors.

Each `MeadowApp` runs a fixed lifecycle:

1. **`Initialize()`**: apply settings, create diagnostics, set up WiFi, then `Services.Create<T>()` every controller/service and call `Initialize(...)` on the ones that own hardware (two-phase construction is deliberate: initialization order is explicit here, not hidden in constructors).
2. **`Run()`**: start the watchdog (`IWatchdogService.Start()`) and the heartbeat, then wait for the network.
3. **`OnNetworkConnected`** (shared): anything needing an IP is deferred to here. It runs the network diagnostics, starts `MapleService` (or re-binds it if DHCP changed the IP, using `BoundAddress`), sets the LED (`StartingServer`, then `Ready` or `ServerFailed`) and logs startup complete. A reconnect with the same IP only sets the LED back to `Ready`. It is `async void` on purpose, with a try/catch that logs a failed start; `_networkServicesStarted` is only set after a successful start, so the next event retries.

WiFi itself is not hand-rolled: `meadow.config.yaml` (`Coprocessor.AutomaticallyStartNetwork`/`AutomaticallyReconnect`) plus a gitignored `wifi.config.yaml` (copy `wifi.config.yaml.template`) connect it.

## Controllers vs. services, and DI

- **Controllers** are hardware-shaped (`LedController`, relay controllers, MP3, the scarecrow's `LiftController`). **Services** are app concerns (`MapleService`, `NetworkService`, `DiagnosticsService`, `WatchdogService`, `HeartbeatService`). Both derive from `BaseController` / `BaseService`: a `Logger` and a `virtual Task Run()` that defaults to a no-op.
- `IOnOrOffController` (`TurnOn`/`TurnOff`) is the contract for every switchable output (relays, LED). "On" means active, whatever the electrical polarity.
- `Services.Create<T>()` builds `T` and resolves its constructor parameters from already-registered instances; `Create<TImpl, TInterface>()` registers under an interface; `Add(instance)` registers something we didn't build (the WiFi adapter, options objects); `Get<T>()` retrieves; `ContainsRegisteredType<T>()` guards shutdown paths. Resolution is purely by registered instance type, so **register a dependency before anything that needs it**.
- **One sanctioned exception**: `ControllerRequestHandler : RequestHandlerBase` is created by Maple outside the container, so it resolves its controllers from `Resolver.Services` in its constructor. Don't "fix" that.
- Named onboard pins (`OnboardLedRed/Green/Blue`) only exist on `F7FeatherV1`, so `OnboardRgbLed` casts internally and every method no-ops on other devices. Elsewhere prefer passing generic `IPin`s from `MeadowApp.Initialize()` (which has the typed `Device`) into `Initialize(IPin ...)`.

## Shared pieces

- **`OnboardRgbLed` / `LedController`**: the onboard LED is common anode (PWM inverted). It shows a `LedStatus` boot stage set with `SetStatus` (yellow booting, red initializing, blue blink waiting for WiFi, orange blink retrying, cyan starting the server, purple blink server failed, green `ReadyColor`); the two-argument `SetStatus(expected, status)` only changes it from `expected`. Apps create it first in `Initialize()` so it lights as soon as the app starts, and set `Initializing` after the diagnostics. Accepted commands call `BeginActivity()`/`EndActivity()` (counted, because requests run in parallel): a hard white blink (`BlinkOn`/`BlinkOff`) for at least `MinActivityBlink`, then back to the current status (a status set during the blink is shown after it). `SignalActivity()` is begin + end for instant commands.
- **`RelayOutput`**: one relay on an open-drain pin (initial state `true`, so it is off as fast as possible at boot; open-drain is required when the F7 is powered from the relay board). The caller passes which `RelayState` means "on". `Dispose()` turns it off before releasing the pin.
- **`WatchdogService`**: `Start()` arms it (`DefaultTimeoutSeconds` 15) and starts an independent thread that pets it every `DefaultPetIntervalSeconds` (10), so a slow request handler can't trip it.
- **`HeartbeatService`**: logs `<time> <ip> (up d.hh:mm:ss)` every `DefaultIntervalSeconds` (30), like the Meadow.Core.Samples `Config_Files` heartbeat. `TestRestartAfterBeats` throws on purpose to test restarts; never set it on a prop.
- **`MapleService`** + `MapleServiceOptions`: both props listen on Maple's default port `5417` (they run on separate devices) and advertise every 2s. `RequestProcessMode.Parallel`; handlers must keep `IsReusable => false` because Maple keeps per-request state (`QueryString`) on the handler instance. Choose Serial vs Parallel by whether your commands can tolerate concurrency.
  Route handlers have hard rules; see **Maple request handlers** below before adding or changing one.

## Maple request handlers (read before adding a route)

These are Maple 2.5.0 behaviours that fail silently or confusingly. Every `ControllerRequestHandler` must follow them. They were found the hard way (an async `/shake` returned an empty `200` and logged nothing).

1. **Return `IActionResult` synchronously. Never `async Task<IActionResult>`.** Maple only awaits handlers whose return type is `IActionResult`. For an async one it closes the response immediately with an empty `200` (so your `400`/`409`/`500` never reach the client) and swallows any exception, so a broken handler looks like a successful one with no logs. To wait on async work, block: `SomeAsync().GetAwaiter().GetResult()`. That is safe: each request runs on its own thread-pool thread (Parallel mode) and Meadow has no synchronization context to deadlock on. A long-running blocked handler does not stop other requests (a `/sound` is still answered during a `/shake`).
2. **`QueryString` is `null` when the URL has no `?`.** Maple only creates it when a query string is present, so `QueryString["x"]` throws a `NullReferenceException` for a plain `POST /shake`. Read it null-safely (`QueryString?["x"]`, or the handler's `Query("x")` helper) and treat a missing parameter as "use the default" or as a `400`, never as an error.
3. **`IsReusable => false`.** Maple keeps per-request state (`QueryString`, `Context`) on the handler instance, so a shared instance lets parallel requests overwrite each other's parameters.
4. **Resolve controllers in the constructor from `Resolver.Services`.** Maple creates the handler outside DI (see above). Only do it for types registered before `MapleService` starts.
5. **Return explicit status codes and log each outcome.** `OkResult` = done, `400` (`HttpStatusCode.BadRequest`) = bad input, `409` = busy, `ServerErrorResult` = a failure you caught. Log the accepted request at Information, rejects at Warning and failures at Error, and signal activity on the LED only for accepted commands.
6. **Wrap anything that can throw** and return `ServerErrorResult` after logging; an unhandled exception becomes a `500` page with the stack trace, not a log line.

Template:

```csharp
[HttpPost("/thing")]
public IActionResult Thing()                      // 1. synchronous IActionResult
{
    if (!int.TryParse(Query("n"), out var n))     // 2. null-safe query read
    {
        Resolver.Log.Warn("Thing request missing or invalid 'n'.");
        return new StatusCodeResult(HttpStatusCode.BadRequest);
    }

    Resolver.Log.Info($"Thing request: n={n}.");  // 5. log the accepted request
    try
    {
        _controller.DoThingAsync(n).GetAwaiter().GetResult();   // 1. block on async work
    }
    catch (Exception ex)
    {
        Resolver.Log.Error($"Thing failed: {ex.Message}");
        return new ServerErrorResult();           // 6.
    }

    _ledController.SignalActivity();
    return new OkResult();
}
```

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

## Building the remote in a Linux / cloud session

Only the Android target builds on Linux (the project adds the Windows target on Windows). Verified in a cloud session with `dl.google.com` allowed; the Microsoft Android installer (`-t:InstallAndroidDependencies`) also needs `aka.ms`, so installing the SDK with Google's `sdkmanager` avoids that:

```bash
apt-get install -y dotnet-sdk-10.0 openjdk-17-jdk-headless
dotnet workload install maui-android
# Android SDK from Google (command-line tools zip from https://developer.android.com/studio#command-tools)
sdkmanager --sdk_root=/root/android-sdk "platform-tools" "build-tools;36.0.0" "platforms;android-36"
cd src/halloween.director.remote
dotnet build -f net10.0-android -p:AndroidSdkDirectory=/root/android-sdk -p:JavaSdkDirectory=/usr/lib/jvm/java-17-openjdk-amd64
```

## Versioning

`src/Directory.Build.props` is the one place for the version (`VersionPrefix`) and the per-build `BuildStamp` (UTC `yyMMdd.HHmm`) and `BuildNumber` (minutes since 2025, the Android versionCode) and the git hash (`+dirty` when uncommitted); it feeds the firmware's `InformationalVersion` (logged at boot by `MeadowBase.ApplyStartupSettings` through `BuildInfo`) and the remote's `ApplicationDisplayVersion`/`ApplicationVersion` (shown in the page titles via `AppVersion`, which reads the same informational version). Don't set versions in the individual csproj files.

## Finding props from the remote

Two ways, both on the remote's Options tab:

- **Broadcast scan:** each prop advertises `<DeviceName>::<ip>` by UDP to 255.255.255.255:17756 every 2 seconds (Maple's own advertisement); the remote listens for 5 seconds. It runs when the tab opens. Some tablets (a Lenovo on ZUI) drop broadcast packets.
- **Subnet scan:** the remote tries a TCP connection to port 5417 on every address of its own /24 subnet, then asks each one that answers for `GET /info`. No broadcast involved.

`GET /info` is served by every prop from the shared library (`InfoRequestHandler`; Maple picks the handler up because it scans all loaded assemblies): `{"name":"Monsterbox-v1","type":"monsterbox","version":"1.0.0 (build 261017.1742 a3f9c1)"}`. The type is the second argument of `MeadowBase.ApplyStartupSettings`; a new prop passes its own. Don't add per-app copies of this route.
