# halloween.director

Props and controllers for the Halloween display. Open `src/halloween.director.sln`.

| Project | What it is |
| --- | --- |
| `halloween.director.shared` | netstandard2.1 class library: `MeadowBase`, base controller/service, watchdog, network and diagnostics services. Referenced by the Meadow firmware projects; its DLL is copied into each app's output (and it is packable as a NuGet package). |
| `halloween.director.monsterbox.meadow` | Meadow F7 firmware for the monsterbox prop. |
| `halloween.director.scarecrow.meadow` | Meadow F7 firmware for the scarecrow prop. |
| `halloween.director.remote` | .NET MAUI mobile remote that controls the props over HTTP (needs the .NET 10 SDK and the MAUI workload). |
