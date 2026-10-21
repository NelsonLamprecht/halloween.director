# halloween.director.scarecrow.meadow

Wilderness Labs Meadow F7 firmware for the scarecrow prop: a relay (pin D05) raises and lowers it on `POST /up` and `POST /down`.

## LED

| LED | Meaning |
| --- | --- |
| Off | Meadow OS is booting; the app hasn't started yet |
| Solid yellow | Meadow OS has booted and the app has started (reading OS and device info) |
| Solid red | Initializing hardware |
| Slow blue blink | Waiting for WiFi |
| Fast orange blink | WiFi failed to connect or dropped, and is retrying (check the signal and `wifi.config.yaml`) |
| Solid cyan | WiFi connected, server starting |
| Fast purple blink | The server failed to start (see the log); it retries on the next WiFi connect |
| Short green flash every 3s, dark in between | Ready and healthy. Each flash is a watchdog pet, so if the flashes stop the app has hung (the watchdog resets the board 15s later) |
| White blink | Handling a command; back to the green flashes when the board can take another |

## Setup

1. Copy `wifi.config.yaml.template` to `wifi.config.yaml` and fill in the WiFi credentials. The file is gitignored.
2. Build and deploy `src/halloween.director.sln` to the Meadow F7 with the Meadow tooling in Visual Studio or VS Code.

The Maple server listens on port `5417` and advertises itself over UDP. Example requests are in [`scarecrow.http`](scarecrow.http). Example log:

```
Listening on http://192.168.1.92:5417/
INFO: POST : /up
INFO: POST : /down
```
