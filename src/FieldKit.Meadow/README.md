# FieldKit.Meadow

A starter kit for networked [Meadow](https://www.wildernesslabs.co/) F7 Feather devices. Derive your app from `MeadowBase` and you get the whole boot sequence: status LED, diagnostics, WiFi, a watchdog, a heartbeat health check, and a Maple web server that advertises itself over UDP and answers `GET /info`. Your app only adds its own hardware and routes.

Targets `net10.0` and Meadow 3.x (`Meadow.F7`, `Meadow.Foundation`, `Meadow.Foundation.Web.Maple.Server`).

## Quick start

```csharp
using FieldKit.Meadow;

public class MeadowApp : MeadowBase
{
    // used when app.config.yaml has no App.DeviceName
    protected override string DefaultDeviceName => "GarageDoor-v1";

    // what GET /info reports as "type"
    protected override string DeviceType => "garage-door";

    // runs during Initialize(), after the LED, diagnostics, WiFi and watchdog are up
    protected override void InitializeDevice()
    {
        var door = Services.Create<DoorController>();
        door.Initialize(Device.Pins.D05);
    }

    // release your own controllers, in the order they must go
    protected override void DisposeServices()
    {
        DisposeIfRegistered<DoorController>();
    }
}
```

Meadow trims dependency assemblies on deploy and would strip constructors that are only reached by reflection (`Services.Create<T>()`), so add this library to your app's `app.build.yaml`:

```yaml
Deploy:
  NoLink: [ FieldKit.Meadow ]
```

WiFi is connected by Meadow OS itself: set `Coprocessor.AutomaticallyStartNetwork` and `AutomaticallyReconnect` in `meadow.config.yaml` and put your network in `wifi.config.yaml`.

## What's in it

| Type | What it does |
| --- | --- |
| `MeadowBase` | The boot sequence and shutdown. Gives your app `Logger`, `Services` and `BootStopwatch`. Reads `App.DeviceName` and `App.StartupDetails` from `app.config.yaml`. |
| `LedController`, `OnboardRgbLed`, `LedStatus` | The onboard RGB LED shows the boot stage: yellow booting, red initializing, blue blink waiting for WiFi, orange blink retrying, cyan starting the server, purple blink server failed, and dark with a green flash on each passing health check once ready. `BeginActivity()`/`EndActivity()` blink it white while a command runs. |
| `RelayOutput`, `IOnOrOffController` | One relay on an active-low relay board, driven by an open-drain pin, off at boot and on dispose. |
| `WatchdogService` | Arms the hardware watchdog (15 s) and pets it from its own thread every 10 s. |
| `HeartbeatService` | Logs uptime and IP every 30 s, and every 3 s checks WiFi, the IP and that Maple is running on it. |
| `MapleService`, `MapleServiceOptions` | Starts Maple on port 5417 once there is an IP (re-binds if DHCP changes it) and advertises every 2 s. |
| `InfoRequestHandler`, `DeviceInfo` | `GET /info` returns `{"name":"…","type":"…","version":"…"}` so clients can find devices without UDP. |
| `NetworkService`, `DiagnosticsService` | Startup diagnostics: OS, device, NTP and network. |
| `BaseController`, `BaseService`, `LoggerExtensions` | Base classes and the startup log sections. |

## Maple request handlers

Maple handlers have rules that fail silently when broken:

- Return `IActionResult` synchronously. An `async Task<IActionResult>` handler returns an empty `200` immediately and swallows exceptions. Block on async work with `.GetAwaiter().GetResult()`.
- `QueryString` is `null` when the URL has no `?`; read it with `QueryString?["x"]`.
- Keep `IsReusable => false`, because Maple stores per-request state on the handler.
- Maple creates handlers outside DI, so resolve controllers from `Resolver.Services` in the constructor.

## License

MIT
