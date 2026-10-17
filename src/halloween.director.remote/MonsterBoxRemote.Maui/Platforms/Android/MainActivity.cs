using Android.App;
using Android.Content.PM;
using Android.OS;

using AndroidX.Core.View;

namespace MonsterBoxRemote.Maui;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // The app's pages use a bright green background; make sure the status
        // bar icons render dark so they stay legible against it (light theme).
        // Dark theme keeps light icons, matching the deep-green dark palette.
        var isDarkTheme = (Resources?.Configuration?.UiMode & Android.Content.Res.UiMode.NightMask) == Android.Content.Res.UiMode.NightYes;
        if (Window is { } window && WindowCompat.GetInsetsController(window, window.DecorView) is { } insetsController)
        {
            insetsController.AppearanceLightStatusBars = !isDarkTheme;
        }
    }
}
