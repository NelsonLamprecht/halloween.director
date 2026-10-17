# Agent notes: monsterbox firmware

Meadow F7 firmware for the monsterbox prop: two relays driving pneumatic cylinders (the shake) and a YX5300 MP3 player, controlled over HTTP by the remote app. **Read the repo-root `agents.md` first**: it covers the shared architecture (`MeadowBase`, DI, LED, relays, watchdog, heartbeat, Maple, logging, config files). This file only covers what is specific to the monsterbox.

## What `MeadowApp` creates

In order: `DiagnosticsService`, WiFi (external antenna, saved), `LedController`, `RelayController` (`Device.Pins.D05` left, `D06` right), `MP3Controller`, then `WatchdogService`, `NetworkService`, `HeartbeatService`, `CylindersController` and `MapleService` (default options). `DisposeServices()` releases the heartbeat, LED, MP3, then `CylindersController` **before** `RelayController`, so an in-flight shake is cancelled and switches the relays off before they are released.

## Controllers

- **`RelayController`** owns two `RelayOutput`s, exposed as `Left` and `Right` (`IOnOrOffController`). For these pneumatics the relay is **closed = on**. `Dispose` switches both off before releasing the pins.
- **`CylindersController`** sits above `RelayController` (constructor-injected). `TryShakeAsync` runs one shake at a time; a second call returns `false` immediately.
- **`MP3Controller`** wraps the YX5300 on the UART. `QueueFile()` only touches lock-protected state; a single background worker is the only code that talks to the UART after `Initialize()`. Don't call `_mp3Player` from request handlers.

## Relay wiring

The relay ports are created with `OutputType.OpenDrain` (initial state `true`, so the valves stay closed at boot). Open-drain is required when the F7 is powered from the relay board's 5V and ground; keep it when touching `RelayController.Initialize`. `RelayController.Dispose` switches both relays off before releasing the pins.

## Maple HTTP server: `Serial` vs `Parallel`

`MapleService.Run()` builds the `MapleServer` with `RequestProcessMode.Parallel` here, because `sound` and `shake` are often triggered close together and must be able to overlap:

- `/shake` **holds its request open until the shake finishes** (`await CylindersController.TryShakeAsync()`), so the mobile app knows when it's done: `200 OK` = finished, `409 Conflict` (returned immediately) = another shake was already running, `500` = the shake threw (relays are switched off first), `400 Bad Request` = the query parameters failed `ShakeConfiguration.TryValidate()` (checked before the busy flag is taken). An `Interlocked` busy flag in `CylindersController` is what prevents two shakes overlapping — Parallel mode means two `/shake` requests really can race.
  - Query parameters: `bi`/`ei` (begin/end iterations, default 25/50, must satisfy `0 <= bi <= ei <= 50`) and `bd`/`ed` (begin/end delay in ms, default 50/75, must satisfy `1 <= bd <= ed <= 1000`). The limits are `ShakeConfiguration.MaxIterations`/`MaxDelayMs`; they exist so a bad value can't hang a shake (`Task.Delay(-1)` waits forever) or hold the relays for minutes.
  - A shake picks a random iteration count in `[bi, ei)` and runs exactly that many actions; each action randomly switches one side on or off, then waits a random `[bd, ed)` ms. Defaults give ~1.25–3.6s and the worst case allowed by the limits is ~50s, so the app's HTTP timeout must allow for that.
- `/sound` requires a `filenumber` query parameter, `0` to `MP3Controller.MaxFileNumber` (254; the module is sent `filenumber + 1` in a single byte): `200 OK` = queued, `400 Bad Request` = missing, not a byte, or above the maximum, `500` = queueing threw.
- `MP3Controller.QueueFile()` puts the file in a **one-slot queue** and returns. A single background playback worker (started in `MP3Controller.Initialize()`) plays it once the current track finishes; a newer request replaces an older pending one, so rapid taps don't stack up. The Yx5300 driver has no "track finished" event, so the worker polls `GetStatus()` — defensively, because the driver's response reader is fragile: a track only counts as finished after several consecutive non-`Playing` reads, a few consecutive errors (or a 1s per-call timeout) end the wait early, and `MaxPlayDuration` (measured with a `Stopwatch`) is a final backstop. The per-call timeout is a cancellable `Task.Delay` so no timer is left running after each poll. `MP3Controller` is `IDisposable`: `Dispose()` cancels the worker's token, which interrupts its waits and ends the loop.

**The playback worker is the only code that touches the Yx5300 UART** after `Initialize()` — that's what keeps `Play()` and `GetStatus()` from colliding. Don't call `_mp3Player` from request handlers or anywhere else; go through the queue. `QueueFile()` itself only touches lock-protected state, so it's safe to call from parallel requests.

## Sounds

`SoundCatalog` reads the `Sounds:` section of `app.config.yaml` (`Name: file number`) into the DI container; `GET /sounds` returns it as JSON (hand-built, because Maple's `JsonResult` serializes by reflection). The module can't report file names, so the config is the only source of names. Meadow's config parser is line-based: **only plain `key: value` lines**, no lists and no inline `{ }`; a section flattens to `Sounds.<Name>` keys. The card must be copied in file-number order (`tools\Copy-SoundsToCard.ps1`, see the README): track = file number + 1.
