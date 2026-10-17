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

The Maple server listens on port `5417` and advertises itself over UDP. Requests are `POST`.

| Endpoint | Parameters | Responses |
| --- | --- | --- |
| `/sound` | `filenumber` (0-254) | `200` queued, `400` missing or invalid, `500` failed |
| `/shake` | `bi`/`ei` iterations (default 25/50, `0 <= bi <= ei <= 50`), `bd`/`ed` delay in ms (default 50/75, `1 <= bd <= ed <= 1000`) | `200` shake finished, `400` invalid parameters, `409` already shaking, `500` failed |

`/shake` holds the request open until the shake finishes, so the client's HTTP timeout must allow for it. Example requests are in [`monsterbox.http`](monsterbox.http).

## LED

Red until the server is up, then green while idle. The LED blinks white while a command is being handled and returns to green when the board can take another.

## Setup

1. Copy `wifi.config.yaml.template` to `wifi.config.yaml` and fill in the WiFi credentials. The file is gitignored.
2. Optionally set `App.DeviceName` in `app.config.yaml`; it is the name the mobile app sees.
3. Build and deploy `src/halloween.director.sln` to the Meadow F7 with the Meadow tooling in Visual Studio or VS Code.

## Sounds and the SD card

The MP3 module (YX5300) plays its SD card **by copy order**: the Nth file copied onto the card is track N, and `/sound?filenumber=F` plays track `F + 1`. The module cannot report file names, only counts, so the names live in `app.config.yaml`:

```yaml
Sounds:
    Werewolf: 0
    Raven: 7
    Creature 1: 8
```

Each line is `Name: file number` (plain lines only: Meadow's config parser has no lists or `{ }`). Number the sounds `0, 1, 2, ...` with no gaps, FX first and the creatures last. `GET /sounds` returns the list as JSON (`[{"number":0,"name":"Werewolf"},...]`) so the remote can build its buttons from it (it shows its built-in list until a monsterbox answers, and keeps it if one can't be reached; names starting with `Creature` go in the CREATURES section, everything else under FX). If you change a number, also update the remote's built-in list (`SoundCatalogClient.Defaults`).

**Naming the files on the card:** `NNN_Name.mp3`, where `NNN` is the file number from the config, zero-padded to three digits, and `Name` is the sound name with spaces as underscores: `000_Werewolf.mp3`, `006_Metal_Hit.mp3`, `007_Raven.mp3`. The number in the name is for people (the module ignores names), but it must match the order the files were copied.

**Preparing a card** with `tools\Copy-SoundsToCard.ps1` (Windows PowerShell or PowerShell 7):

1. Put the originals in one folder, named after the sound (`Raven.mp3`, `door_creek.mp3`; case, spaces, `_` and `-` are ignored) or already `007_anything.mp3`.
2. Quick-format the card as **FAT32**. The card must be empty: deleting files does not reliably reset the order.
3. Run it (add `-WhatIf` first to preview):
   ```powershell
   .\tools\Copy-SoundsToCard.ps1 -SourceFolder C:\Halloween\Sounds -Destination E:\
   ```
   If the numbers ever have a gap, pass `-FillerFile` (a short silent mp3) and it is copied into each missing number. To add a sound, add a `Name: number` line using the next free number and re-run it on a freshly formatted card.
4. Eject the card in Windows before pulling it.
