# Agent notes: architecture and patterns

This is a Wilderness Labs Meadow F7 firmware project (Meadow.Sdk / .NET, `netstandard2.1`, runs on physical hardware — a relay driving a pneumatic actuator, controlled over HTTP by a companion mobile app). This repo is the **origin** of the service-layer/DI/watchdog pattern shared with the sibling project `meadow_monsterbox` (same author, same hardware family) — `meadow_monsterbox` was refactored to match this repo's architecture, not the other way around. If you're building a new Meadow app, start from this repo's shape.

## Composition root: `MeadowApp` + `MeadowBase`

`MeadowBase.cs` is a thin abstract base (`MeadowBase : App<F7FeatherV1>`) exposing two conveniences so nothing else has to reach for the static `Resolver` directly:

```csharp
protected Logger Logger { get; } = Resolver.Log;
protected ServiceCollection Services { get; } = Resolver.Services;
```

`MeadowApp : MeadowBase` is the only place that wires the app together:

1. **`Initialize()`** — construct and register every controller/service via `Services.Create<T>()`, then do the hardware-specific `Initialize(...)` calls (e.g. `relayController.Initialize(Device.Pins.D05)`) that actually open ports.
2. **`Run()`** — start the diagnostics dump, arm the watchdog (`Enable`/`Pet`), kick off `RelayController.Run()`, and set the onboard LED to a "running" blink state.
3. **`NetworkConnected` handler** — anything needing a live IP address (the Maple HTTP server) starts here, not in `Initialize()`. Calls `NetworkService.NetworkIsConnected(sender)` then `MapleService.Run()`.

WiFi connection is **not** hand-rolled — `meadow.config.yaml`'s `Coprocessor.AutomaticallyStartNetwork`/`AutomaticallyReconnect` (plus the fuller `Network:`/NTP/DNS block also present in this repo's config — copy this file's shape for new projects, it's more complete than `meadow_monsterbox`'s) and a gitignored `wifi.config.yaml` (credentials) handle it.

## Controllers vs. Services

- **`Controllers/`** = thin hardware drivers (`RelayController`, `OnBoardLEDDeviceController`). Each owns one piece of physical hardware.
- **`Services/`** = app-level concerns not tied to one piece of hardware: `MapleService` (HTTP server), `NetworkService` (post-connect diagnostics trigger), `DiagnosticsService` (device/OS/NTP/WiFi info logging), `Watchdog/WatchdogService` (hang recovery), `HeartbeatService` (**see warning below**).

Both layers share the same shape: `Controllers/BaseController.cs` and `Services/BaseService.cs` — constructor takes a `Logger`, exposes it as a property, and a `virtual Task Run()` defaulting to a no-op. Only override `Run()` if the type needs to kick off work on its own; most services here (`DiagnosticsService`, `NetworkService`) are just DI-registered helpers invoked via their own named methods, not through `Run()`.

**Interfaces where they earn their keep**: `IOnOrOffController` (`TurnOn`/`TurnOff`) and `ILEDDeviceController` (extends it, adds `SetColor`/`Stop`/`StartBlink`) let `MeadowApp` depend on an abstraction rather than a concrete controller type, registered via `Services.Create<OnBoardLEDDeviceController, ILEDDeviceController>()`. Only add an interface like this when something genuinely depends on the abstraction (e.g. to swap hardware later) — don't add one reflexively for every controller (`RelayController` here has no interface, because nothing needs one).

## Real constructor DI via `Resolver.Services` (`ServiceCollection`)

Every controller/service constructor declares its dependencies; `Services.Create<T>()` resolves and injects them. No controller or service reaches into `Resolver.Services.Get<T>()` from inside its own business logic.

- `Services.Create<T>()` — construct, resolve constructor params from already-registered services, register.
- `Services.Create<TImpl, TInterface>()` — same, registered under an interface (`Services.Create<WatchdogService, IWatchdogService>()`, `Services.Create<OnBoardLEDDeviceController, ILEDDeviceController>()`).
- `Services.Add(instance)` — register an already-constructed object not built by us (the `IWiFiNetworkAdapter` from `Device.NetworkAdapters.Primary<IWiFiNetworkAdapter>()`).
- `Services.Get<T>()` — retrieve a previously registered instance.

**Ordering matters**: `RelayController` is created before `NetworkService`/`MapleService`; the WiFi adapter is `Add`ed before anything that takes `INetworkAdapter` in its constructor is `Create`d.

**The one sanctioned exception**: `Services/MapleService/ControllerRequestHandler.cs` (`: RequestHandlerBase`) is instantiated by the Maple framework itself, outside this DI container, so it correctly reaches into the global registry directly (`Resolver.Services.Get<RelayController>()`). Don't "fix" this to use constructor injection — it structurally can't receive one.

## The `IMeadowDevice` vs. concrete device-type split

Controllers take `IMeadowDevice device` via constructor injection, which covers the common surface (`CreateDigitalOutputPort`, `PlatformOS`, `Information`, etc.). But **named onboard pins** (`OnboardLedRed`/Green/Blue) only exist on the concrete `F7FeatherV1` type. `OnBoardLEDDeviceController` handles this with a runtime cast in its constructor: `if (this.device is F7FeatherV1 f7DeviceV1) { ... f7DeviceV1.Pins.OnboardLedRed ... }`. `RelayController` avoids needing this by not taking a named pin at all — it takes a generic `IPin devicePin` parameter in `Initialize(IPin devicePin)`, with the concrete pin (`Device.Pins.D05`) resolved by `MeadowApp` (which has the strongly-typed `Device: F7FeatherV1`) and passed down. Prefer that second pattern when practical; reach for the `is F7FeatherV1` cast only when the API you need genuinely isn't on the interface.

## Watchdog

`WatchdogService : BaseService, IWatchdogService` wraps `device.WatchdogEnable(...)`/`WatchdogReset()`. `Enable(seconds)` arms it; `Pet(seconds)` spins up an **independent background `Thread`** that resets the watchdog on its own schedule, decoupled from request handling — so a slow HTTP handler can't accidentally trip it. Current values: 15s timeout / 10s pet interval.

## Maple HTTP server: `Serial` vs `Parallel`

`MapleService.Run()` here uses `RequestProcessMode.Parallel` — safe because `up`/`down` relay commands are simple, non-overlapping toggles with no shared mutable state to race on. `meadow_monsterbox` uses `RequestProcessMode.Serial` instead, because its `shake`/`sound` handlers involve timed sequences that must not overlap. **Choose the mode based on whether your command handlers have state or hardware that can't tolerate concurrent access** — don't copy one blindly.

## ⚠️ `HeartbeatService` is a debug tool, not default wiring

`Services/HeartbeatService/HeartbeatService.cs` exists in this repo but is **not** created/registered in `MeadowApp.Initialize()` — it's intentionally dead code, kept for deliberate restart testing. Its `Run()` loop counts down and `throw`s on purpose to exercise `Lifecycle.ResetOnAppFailure`/`AppFailureRestartDelaySeconds` (see `app.config.yaml`). If you ever need to test crash-recovery behavior, wire it up explicitly and pull it back out afterward — never leave it running in a build meant to actually control hardware for an event.

## Adding a new controller or service

1. Subclass `BaseController` (hardware-shaped) or `BaseService` (app-concern-shaped). Constructor takes `Logger logger` (→ `base(logger)`) plus whatever else it needs.
2. If it owns real hardware and needs idempotent setup, give it a separate `Initialize(...)` method (parameterized with `IPin`/etc. as needed) rather than doing it in the constructor, and call that explicitly from `MeadowApp.Initialize()` after `Services.Create<T>()`.
3. Register it in `MeadowApp.Initialize()` in dependency order — plain `Services.Create<T>()`, or `Create<TImpl, TInterface>()` if other code should depend on an abstraction.
4. Only add an interface when something actually needs to depend on the abstraction rather than the concrete type.
5. Leave `HeartbeatService`-style chaos/testing services unwired unless you're deliberately testing recovery.
