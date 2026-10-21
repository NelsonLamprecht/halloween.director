# Agent notes: scarecrow firmware

Meadow F7 firmware for the scarecrow prop: one relay driving a scissor lift (pneumatic cylinder), controlled over HTTP by the remote app. **Read the repo-root `agents.md` first**: it covers the shared architecture (`MeadowBase`, DI, LED, relays, watchdog, heartbeat, Maple, logging, config files). This file only covers what is specific to the scarecrow.

## What `MeadowApp` creates

In order: `DiagnosticsService`, WiFi (external antenna, saved), `LedController`, `WatchdogService`, `RelayController` (`Device.Pins.D05`), `LiftController`, then `HeartbeatService`, `NetworkService` and `MapleService` (default options). `DisposeServices()` releases the heartbeat, then `LiftController` (cancels a pending auto-lower), then `RelayController` (closes the valve before its pin is released), then the LED.

## The lift

- **Relay wiring**: relay **off = lift down** (the rest state: where it starts at boot, where `Down()` puts it and where the auto-lower puts it); relay **on = lift up**. `RelayController` is built on the shared `RelayOutput`, so `TurnOn()` raises and `TurnOff()` lowers.
- **`LiftController`** wraps the relay with `Up()` / `Down()`. A repeated command is a no-op (returns `false`). After `Up()`, the lift lowers itself after `LiftOptions.MaxUp`: the default is the constant `LiftOptions.DefaultMaxUpSeconds` (30), overridden by `App.LiftMaxUpSeconds` in `app.config.yaml` (0 = no limit). A second `Up()` does not extend the timer; `Down()` cancels it.

## HTTP endpoints

`Controllers/ControllerRequestHandler.cs` (resolves `LiftController` and `LedController` in its constructor; `IsReusable => false`):

- `POST /up` and `POST /down`: `200 OK` when the lift moved or was already there, `500` if the relay failed. Accepted commands blink the LED. Examples are in `scarecrow.http`.
- Maple runs in `Parallel` mode, which is safe here because the lift state is guarded by a lock in `LiftController`.
