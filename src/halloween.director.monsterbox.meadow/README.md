# halloween.director.monsterbox.meadow

Wilderness Labs Meadow F7 firmware for the monsterbox prop: two relays drive pneumatic cylinders (the shake) and a YX5300 MP3 module plays sounds. A companion mobile app triggers both over HTTP.

Architecture, patterns and the reasons behind them are in [AGENTS.md](agents.md); read it before changing code.

## Hardware

| Part | Connection |
| --- | --- |
| Left / right relay | Feather `D05` / `D06` (open-drain; required when the F7 is powered from the relay board's 5V and ground) |
| YX5300 MP3 module | Feather `COM4` UART |
| Status LED | Onboard RGB LED |

## HTTP API

The Maple server listens on port `5417` and advertises itself over UDP (the scarecrow uses `5418`, so both can run on one network). Requests are `POST`.

| Endpoint | Parameters | Responses |
| --- | --- | --- |
| `/sound` | `filenumber` (0-254) | `200` queued, `400` missing or invalid, `500` failed |
| `/shake` | `bi`/`ei` iterations (default 25/50, `0 <= bi <= ei <= 50`), `bd`/`ed` delay in ms (default 50/75, `1 <= bd <= ed <= 1000`) | `200` shake finished, `400` invalid parameters, `409` already shaking, `500` failed |

`/shake` holds the request open until the shake finishes, so the client's HTTP timeout must allow for it. Example requests are in [`monsterbox.http`](monsterbox.http).

## LED

Red until the server is up, then green while idle. The LED pulses while a command is being handled and returns to green when the board can take another.

## Setup

1. Copy `wifi.config.yaml.template` to `wifi.config.yaml` and fill in the WiFi credentials. The file is gitignored.
2. Optionally set `App.DeviceName` in `app.config.yaml`; it is the name the mobile app sees.
3. Build and deploy `src/halloween.director.sln` to the Meadow F7 with the Meadow tooling in Visual Studio or VS Code.
