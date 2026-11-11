# halloween.director

Props and controllers for the Halloween display. Open `src/halloween.director.sln`.

| Project | What it is |
| --- | --- |
| `halloween.director.monsterbox.meadow` | Meadow F7 firmware for the monsterbox prop. |
| `halloween.director.scarecrow.meadow` | Meadow F7 firmware for the scarecrow prop. |
| `halloween.director.remote` | .NET MAUI mobile remote that controls the props over HTTP (needs the .NET 10 SDK and the MAUI workload). |

The firmware is built on the [FieldKit.Meadow](https://github.com/NelsonLamprecht/fieldkit) NuGet package (app base, status LED, relays, watchdog, heartbeat and Maple web server).

See `agents.md` for the shared architecture and patterns, and each project's `agents.md` for its specifics.

## Building

The firmware uses the FieldKit.Meadow package from GitHub Packages (`NuGet.Config` maps `FieldKit.*` to it; everything else comes from nuget.org). GitHub Packages needs a sign-in even to download, so on each machine, once:

1. Create a classic personal access token at [github.com/settings/tokens](https://github.com/settings/tokens) with only the `read:packages` scope.
2. Add it to your user-level NuGet config under the source name `fieldkit` (the name must match `NuGet.Config`):

   ```powershell
   dotnet nuget add source https://nuget.pkg.github.com/NelsonLamprecht/index.json --name fieldkit --username <your GitHub username> --password <token>
   ```

   On macOS and Linux, add `--store-password-in-clear-text`; Windows encrypts it.

Then build as usual (Visual Studio or `dotnet build src/halloween.director.sln`).

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

The build produces a plain APK (`AndroidPackageFormat` is `apk` for Android in the csproj), so it installs on any tablet. To get the file to copy to a tablet, run `dotnet build -f net10.0-android -c Release -t:SignAndroidPackage`; it is `bin\Release\net10.0-android\com.nlamprecht.halloween.director.remote-Signed.apk`. Install it with `adb install -r <file>.apk`.

Uninstall: `adb uninstall com.nlamprecht.halloween.director.remote`.

The tablet must be on the same WiFi network as the props. On the remote's Options tab, **Scan (broadcast)** listens for the props' UDP advertisement (it also runs when the tab opens); **Scan (subnet)** asks every address on the tablet's network for the prop's `GET /info`, for tablets that drop the broadcast. Typing an address always works.

## Version and build number

`src/Directory.Build.props` gives every project the same version: `VersionPrefix` (edit it by hand for a release) plus a stamp of the minute each build was made.

- **Build stamp:** the UTC date and time as `yyMMdd.HHmm`, for example `261017.1742`. It's what the app shows and the firmware logs, and it's different for every build made in a different minute.
- **Build number:** minutes since 2025-01-01 (UTC). It's Android's `versionCode`, so every build is newer than the last. Pass `-p:BuildNumber=123` to use your own number instead (the stamp then shows it).
- **Git hash:** the commit the build was made from, with `+dirty` added when there are uncommitted changes, so a hash never claims code it doesn't match. It's left out if `git` isn't available.
- **Remote app:** the page headers show `v1.0.0 (build 261017.1742 a3f9c1+dirty)`. The splash screen is a static image, so the version is on the first page.
- **Firmware:** each prop logs `App 1.0.0 (build 261017.1742 a3f9c1)` at the start of boot (`ApplyStartupSettings`).

If the number on the screen or in the log isn't the one you just built, the old build is still running.
