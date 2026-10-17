# halloween.director

Props and controllers for the Halloween display. Open `src/halloween.director.sln`.

| Project | What it is |
| --- | --- |
| `halloween.director.shared` | netstandard2.1 class library: `MeadowBase`, base controller/service, watchdog, network and diagnostics services. Referenced by the Meadow firmware projects; its DLL is copied into each app's output (and it is packable as a NuGet package). |
| `halloween.director.monsterbox.meadow` | Meadow F7 firmware for the monsterbox prop. |
| `halloween.director.scarecrow.meadow` | Meadow F7 firmware for the scarecrow prop. |
| `halloween.director.remote` | .NET MAUI mobile remote that controls the props over HTTP (needs the .NET 10 SDK and the MAUI workload). |

See `agents.md` for the shared architecture and patterns, and each project's `agents.md` for its specifics.

## Installing the remote on an Android tablet

The remote's Android application id is `com.nlamprecht.halloween.director.remote`; installing again updates the app in place.

### Enable side loading (once per tablet)

1. **Settings → About tablet**, tap **Build number** 7 times to turn on Developer options.
2. **Settings → System → Developer options**, turn on **USB debugging**. Wireless instead: turn on **Wireless debugging**, then **Pair device with pairing code**.
3. Connect the tablet by USB and accept **Allow USB debugging?** on it (tick *Always allow*).
4. If you install an APK file by hand rather than with the commands below, allow **Install unknown apps** for the app that opens it (Settings → Apps → Special app access, or the prompt Android shows). Installing with `adb` / `dotnet` doesn't need this.

You also need the .NET 10 SDK, the MAUI Android workload (`dotnet workload install maui-android`) and `adb` (Android SDK platform-tools) on your PATH.

### Deploy from the command line

```
cd src\halloween.director.remote

adb devices                                  # the tablet should be listed as "device", not "unauthorized"
dotnet build -f net10.0-android -c Release -t:Install
adb shell monkey -p com.nlamprecht.halloween.director.remote 1     # launch it
```

Wireless debugging: `adb pair <ip>:<pairing-port>` (enter the code), then `adb connect <ip>:<port>`, then run the same commands.

To make an APK file instead (to copy to the tablet), run `dotnet publish -f net10.0-android -c Release -p:AndroidPackageFormat=apk`; the `.apk` is in `bin\Release\net10.0-android\publish\`. Install it with `adb install -r <file>.apk`.

Uninstall: `adb uninstall com.nlamprecht.halloween.director.remote`.

The tablet must be on the same WiFi network as the props.
