# Agent notes: architecture and patterns

This is a Wilderness Labs Meadow F7 firmware project (Meadow.Sdk / .NET, `netstandard2.1`, runs on physical hardware — relays driving pneumatic cylinders and an MP3 player, controlled over HTTP by a companion mobile app). It shares its architecture with the sibling project `meadow_scarecrow` (same author, same hardware family). If you're building a new Meadow app, or extending either of these, follow the pattern described here rather than reinventing it — read `meadow_scarecrow`'s code too, since some pieces (e.g. `WatchdogService`) were authored there first and ported here verbatim.

## Composition root: `MeadowApp` + `MeadowBase`

`MeadowBase.cs` is a thin abstract base (`MeadowBase : App<F7FeatherV1>`) that exposes two conveniences so nothing else in the app has to reach for the static `Resolver` directly:

```csharp
protected Logger Logger { get; } = Resolver.Log;
protected ServiceCollection Services { get; } = Resolver.Services;
```

`MeadowApp : MeadowBase` is the only place that wires the app together, in a fixed lifecycle:

1. **`Initialize()`** — construct and register every controller/service, and do the *cheap, synchronous* parts of hardware setup. Two-phase construction is deliberate: `Services.Create<T>()` builds the object and resolves its constructor dependencies (see below); a separate explicit `.Initialize(...)` call on controllers that own real hardware then opens ports/peripherals. Keeping these separate means initialization order is controlled explicitly here, not implicitly by constructor side effects.
2. **`Run()`** — start anything that should begin ticking once the app is otherwise ready (here: enabling the watchdog). Returns/awaits `base.Run()`.
3. **`OnWifiConnected` (a `NetworkConnected` handler)** — anything that needs a live IP address (the Maple HTTP server) is deferred to here, not `Initialize()`, since the network isn't up yet at boot. The handler is `async void` (it's an event handler) and awaits `MapleService.Run()` inside a try/catch that logs failures. `AutomaticallyReconnect` can fire this event again after a drop, and the Maple server is bound to one IP, so the handler compares the event's IP with `MapleService.BoundAddress`: unchanged means leave the server alone, changed means call `MapleService.Run()` again (it stops the existing server first and re-binds). `_servicesStarted` is only set after a successful start, so a failed start is retried on the next `NetworkConnected` event.
4. **`OnError` / `OnShutdown`** — log unhandled errors; dispose every controller that implements `IDisposable` (`LedController`, `MP3Controller`, `CylindersController`, `RelayController`, in that order — `CylindersController` before `RelayController` so an in-flight shake is cancelled and switches the relays off before they're released), each checked via `Services.ContainsRegisteredType<T>()` first, since shutdown can be reached before those services were ever created.

WiFi connection itself is **not** hand-rolled — `meadow.config.yaml`'s `Coprocessor.AutomaticallyStartNetwork`/`AutomaticallyReconnect` plus a gitignored `wifi.config.yaml` (credentials) handle it. `MeadowApp` only listens for the `NetworkConnected` event; it never calls `Connect()` itself.

## Controllers vs. Services

- **`Controllers/`** = thin hardware drivers (`RelayController`, `MP3Controller`, `LedController`). Each owns one piece of physical hardware and exposes simple imperative methods (`TurnOnLeft()`, `QueueFile(...)`, `SetColor(...)`).
- **`Services/`** = app-level concerns that aren't tied to one piece of hardware (`MapleService` — the HTTP server; `NetworkService` — post-connect diagnostics; `DiagnosticsService` — device/OS/NTP/WiFi info logging; `Watchdog/WatchdogService` — hang recovery).
- `CylindersController` sits one level up from `RelayController` — it's still in `Controllers/` because it's still hardware-shaped (shake orchestration), but note it depends on another controller (`RelayController`) via constructor injection, not on `IMeadowDevice` directly. That's fine — dependencies compose.

Both layers share the same shape: `Controllers/BaseController.cs` and `Services/BaseService.cs` are near-identical — constructor takes a `Logger`, exposes it as a property, and a `virtual Task Run()` that defaults to a no-op. `Run()` is only overridden by services that need to actually do something at start (`MapleService` builds and starts the server; `WatchdogService` doesn't override it — its work happens via explicit `Enable()`/`Pet()` calls instead). Most services (`DiagnosticsService`, `NetworkService`) never have `Run()` called at all — they're just DI-registered helpers invoked via their own named methods.

## Real constructor DI via `Resolver.Services` (`ServiceCollection`)

Every controller/service constructor declares what it needs, and `Services.Create<T>()` resolves and injects it — no controller or service reaches into `Resolver.Services.Get<T>()` from inside its own business logic. Patterns in use:

- `Services.Create<T>()` — construct `T`, resolving constructor params against already-registered services, and register it.
- `Services.Create<TImpl, TInterface>()` — same, but register it under an interface (e.g. `Services.Create<WatchdogService, IWatchdogService>()`), so later code depends on the abstraction (`Services.Get<IWatchdogService>()`), not the concrete type.
- `Services.Add(instance)` — register an already-constructed object (used for the `IWiFiNetworkAdapter` obtained from `Device.NetworkAdapters.Primary<IWiFiNetworkAdapter>()`, which isn't constructed by us).
- `Services.Get<T>()` — retrieve a previously registered instance.
- `Services.ContainsRegisteredType<T>()` — check before `Get<T>()` when the caller might run before that type was ever registered (shutdown paths).

**Ordering matters**: register a dependency before anything that needs it. E.g. `RelayController` is `Create`d before `CylindersController` (which takes `RelayController` in its constructor); the WiFi adapter is `Add`ed before `MapleService`/`NetworkService` are `Create`d (both take `INetworkAdapter`).

**The one sanctioned exception**: `ControllerRequestHandler : RequestHandlerBase` (the Maple HTTP route handler) is instantiated by the Maple framework itself, outside this DI container — it has no way to receive constructor-injected dependencies. It correctly reaches into the global registry directly (`Resolver.Services.Get<MP3Controller>()`, `Resolver.Services.Get<CylindersController>()`). Don't "fix" this to use constructor injection; it can't.

## The `IMeadowDevice` vs. concrete device-type split

Controllers take `IMeadowDevice device` via constructor injection — that interface covers the common surface (`CreateDigitalOutputPort`, `PlatformOS`, `Information`, etc.). But **named onboard pins** (`OnboardLedRed`, `OnboardLedGreen`, `OnboardLedBlue`) only exist on the concrete `F7FeatherV1` type's `Pins` property, not on the generic interface. Two ways this gets handled here:

- `LedController` receives the generic `IMeadowDevice` and casts internally: `if (device is F7FeatherV1 f7Device) { ... f7Device.Pins.OnboardLedRed ... }`.
- `RelayController` avoids the cast entirely by not needing named pins inside the controller at all — `MeadowApp.Initialize()` (which has the strongly-typed `Device: F7FeatherV1` from `App<F7FeatherV1>`) resolves `Device.Pins.D05`/`Device.Pins.D06` itself and passes them in as generic `IPin` arguments to `RelayController.Initialize(IPin leftPin, IPin rightPin)`.

Prefer the second pattern (resolve concrete pins at the composition root, inject `IPin`) when practical — it keeps the controller decoupled from the concrete device type. Only reach for the `is F7FeatherV1` cast when the API you need (like named onboard LED pins) genuinely isn't available any other way.

When the cast is used, the hardware field stays null on any other device type, so `LedController`'s public methods return without doing anything in that case rather than throwing.

## LED status

The onboard LED is red until the Maple server is up, then `LedController.ReadyColor` (green) while idle. Accepted commands pulse it via `LedController.BeginActivity()`/`EndActivity()`, which are counted because requests run in parallel: the pulse starts on the first active request and the LED returns to ready only when the last one finishes, and never sooner than one full pulse cycle (2s, `MinActivityPulse` = `PulseDuration`) after the pulse started, with the delay done on a background task so it never holds up an HTTP response. `SignalActivity()` is begin+end for commands that complete instantly. `/shake` pulses for the whole shake (begin after validation, end in a `finally`); `/sound` pulses once the file is queued. Rejected requests (400) don't pulse.

The pulse is hand-rolled (`LedController.PulseLoop`): one random hue per activity, brightness faded 10%→100%→10% over `PulseDuration` via `SetColor(Color.FromHsba(...))`. It doesn't use `RgbPwmLed.StartPulse(Color, ...)`, which doesn't render the color on the board (plain white). `Color.FromHsba` takes hue in degrees (0–360), not 0–1. `Stop()` cancels and waits for `animationTask` as well as the library animation, so no late write lands after the caller's next `SetColor`.

## Relay wiring

The relay ports are created with `OutputType.OpenDrain` (initial state `true`, so the valves stay closed at boot). Open-drain is required when the F7 is powered from the relay board's 5V and ground; keep it when touching `RelayController.Initialize`. `RelayController.Dispose` switches both relays off before releasing the pins.

## Watchdog

`WatchdogService : BaseService, IWatchdogService` wraps `device.WatchdogEnable(...)`/`WatchdogReset()`. `Enable(seconds)` arms it; `Pet(seconds)` spins up an **independent `Thread`** (the same pattern as Meadow's documented example) that resets the watchdog on its own schedule. This independence is intentional: petting isn't coupled to request handling at all, so a slow or blocking handler can't trip the watchdog (e.g. `/shake` holds its request open for the full shake duration). Current values here (15s timeout / 10s pet interval) were copied from `meadow_scarecrow`; retune against real command durations if commands ever run longer.

## Maple HTTP server: `Serial` vs `Parallel`

`MapleService.Run()` builds the `MapleServer` with `RequestProcessMode.Parallel` here, because `sound` and `shake` are often triggered close together and must be able to overlap:

- `/shake` **holds its request open until the shake finishes** (`await CylindersController.TryShakeAsync()`), so the mobile app knows when it's done: `200 OK` = finished, `409 Conflict` (returned immediately) = another shake was already running, `500` = the shake threw (relays are switched off first), `400 Bad Request` = the query parameters failed `ShakeConfiguration.TryValidate()` (checked before the busy flag is taken). An `Interlocked` busy flag in `CylindersController` is what prevents two shakes overlapping — Parallel mode means two `/shake` requests really can race.
  - Query parameters: `bi`/`ei` (begin/end iterations, default 25/50, must satisfy `0 <= bi <= ei <= 50`) and `bd`/`ed` (begin/end delay in ms, default 50/75, must satisfy `1 <= bd <= ed <= 1000`). The limits are `ShakeConfiguration.MaxIterations`/`MaxDelayMs`; they exist so a bad value can't hang a shake (`Task.Delay(-1)` waits forever) or hold the relays for minutes.
  - A shake picks a random iteration count in `[bi, ei)` and runs exactly that many actions; each action randomly switches one side on or off, then waits a random `[bd, ed)` ms. Defaults give ~1.25–3.6s and the worst case allowed by the limits is ~50s, so the app's HTTP timeout must allow for that.
- `/sound` requires a `filenumber` query parameter, `0` to `MP3Controller.MaxFileNumber` (254; the module is sent `filenumber + 1` in a single byte): `200 OK` = queued, `400 Bad Request` = missing, not a byte, or above the maximum, `500` = queueing threw.
- `MP3Controller.QueueFile()` puts the file in a **one-slot queue** and returns. A single background playback worker (started in `MP3Controller.Initialize()`) plays it once the current track finishes; a newer request replaces an older pending one, so rapid taps don't stack up. The Yx5300 driver has no "track finished" event, so the worker polls `GetStatus()` — defensively, because the driver's response reader is fragile: a track only counts as finished after several consecutive non-`Playing` reads, a few consecutive errors (or a 1s per-call timeout) end the wait early, and `MaxPlayDuration` (measured with a `Stopwatch`) is a final backstop. The per-call timeout is a cancellable `Task.Delay` so no timer is left running after each poll. `MP3Controller` is `IDisposable`: `Dispose()` cancels the worker's token, which interrupts its waits and ends the loop.

**The playback worker is the only code that touches the Yx5300 UART** after `Initialize()` — that's what keeps `Play()` and `GetStatus()` from colliding. Don't call `_mp3Player` from request handlers or anywhere else; go through the queue. `QueueFile()` itself only touches lock-protected state, so it's safe to call from parallel requests.

**`ControllerRequestHandler.IsReusable` must stay `false` in Parallel mode.** Maple keeps per-request state (`QueryString`, `Context`) on the handler instance, so a shared, reusable handler would let concurrent requests overwrite each other's query string.

(`meadow_scarecrow` also uses `Parallel`, because its `up`/`down` relay commands are simple, non-overlapping toggles with no meaningful concurrency hazard.) **When replicating this pattern in a new project, choose the mode based on whether your command handlers have shared mutable state or hardware that can't tolerate concurrent access** — don't default to one or the other without thinking about it.

## Adding a new controller or service

1. Subclass `BaseController` (hardware-shaped) or `BaseService` (app-concern-shaped). Constructor takes `Logger logger` (pass to `base(logger)`) plus whatever else it needs — `IMeadowDevice`, another already-registered controller/service, `INetworkAdapter`, etc.
2. If it owns real hardware (ports, peripherals) and needs idempotent setup, give it a separate `Initialize(...)` method rather than doing it in the constructor, and call that explicitly from `MeadowApp.Initialize()` after `Services.Create<T>()`.
3. Register it in `MeadowApp.Initialize()` in dependency order: `Services.Create<T>()` (or `Create<TImpl, TInterface>()` if other code should depend on an abstraction).
4. If it owns unmanaged/hardware resources, implement `IDisposable` and add a `Services.ContainsRegisteredType<T>() → Services.Get<T>().Dispose()` guard to `MeadowApp.OnShutdown()`.
5. Don't add a restart-testing/chaos service (see `meadow_scarecrow`'s `HeartbeatService`, which intentionally throws periodically to test recovery) to production wiring without being explicitly asked — it's a debug tool, not a default.
