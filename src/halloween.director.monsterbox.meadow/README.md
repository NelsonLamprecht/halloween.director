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

| LED | Meaning |
| --- | --- |
| Off | Meadow OS is booting; the app hasn't started yet |
| Solid red | Initializing hardware |
| Slow blue blink | Waiting for WiFi |
| Fast orange blink | WiFi failed to connect or dropped, and is retrying (check the signal and `wifi.config.yaml`) |
| Solid cyan | WiFi connected, server starting |
| Fast purple blink | The server failed to start (see the log); it retries on the next WiFi connect |
| Green | Ready |
| White blink | Handling a command; back to green when the board can take another |

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

Each line is `Name: file number` (plain lines only: Meadow's config parser has no lists or `{ }`). The sounds are numbered `0, 1, 2, ...` with no gaps, FX first and the creatures (names starting with `Creature`) last. `GET /sounds` returns the list as JSON (`[{"number":0,"name":"Werewolf"},...]`) so the remote can build its buttons from it. The remote shows its built-in list until a monsterbox answers, and keeps it if one can't be reached; names starting with `Creature` go in the CREATURES section, everything else under FX.

**You don't edit that list by hand.** Keep all the sound files in one folder and run `tools\Copy-SoundsToCard.ps1` (Windows PowerShell or PowerShell 7). It reads the folder, numbers the sounds, rewrites the `Sounds:` section of `app.config.yaml` (nothing else in the file changes) and copies the files to the card in that order:

1. Put the `.mp3` / `.wav` files in one folder. The file name becomes the sound name: `Raven.mp3` is "Raven", `evil_laugh.mp3` is "Evil laugh" (`_` and `-` become spaces). A sound already in the config keeps its spelling and its place in the order, so the remote's buttons don't move; a new file is added after the existing non-creature sounds and before the creatures. Use `-Order Alphabetical` to sort by name instead. A file that was removed from the folder is dropped from the list (the script warns), and the numbers after it shift.
2. Quick-format the card as **FAT32**. The card must be empty: deleting files does not reliably reset the order.
3. Preview, then run it (omit `-Destination` to only update the config):
   ```powershell
   .\tools\Copy-SoundsToCard.ps1 -SourceFolder C:\Halloween\Sounds -Destination E:\ -WhatIf
   .\tools\Copy-SoundsToCard.ps1 -SourceFolder C:\Halloween\Sounds -Destination E:\
   ```
   It prints the new list with `new` / `was N` markers.
4. Eject the card in Windows, then **redeploy the monsterbox** (the config is part of the app). The remote reads the list from `/sounds`, so it needs no change.

**Names on the card:** `NNN_Name.mp3`, where `NNN` is the file number zero-padded to three digits and `Name` is the sound name with spaces as underscores: `000_Werewolf.mp3`, `007_Raven.mp3`, `011_Creature_1.mp3`. The number in the name is for people (the module ignores names), but it must match the order the files were copied. A file that already starts with `NNN_` in the source folder has that prefix ignored.
