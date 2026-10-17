# halloween.director.scarecrow.meadow

Wilderness Labs Meadow F7 firmware for the scarecrow prop: a relay (pin D05) raises and lowers it on `POST /up` and `POST /down`.

## Setup

1. Copy `wifi.config.yaml.template` to `wifi.config.yaml` and fill in the WiFi credentials. The file is gitignored.
2. Build and deploy `src/halloween.director.sln` to the Meadow F7 with the Meadow tooling in Visual Studio or VS Code.

The Maple server listens on port `5417` and advertises itself over UDP. Example log:

```
Listening on http://192.168.1.92:5417/
INFO: POST : /up
INFO: POST : /down
```
