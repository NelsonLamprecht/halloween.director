using Microsoft.Extensions.Logging;

using MonsterBoxRemote.Maui.ViewModel;
using MonsterBoxRemote.Maui.Views;

namespace MonsterBoxRemote.Maui;

public static partial class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		// Implemented once per Platforms/<X>/ folder (see MauiProgram.Android.cs);
		// a no-op on platforms that don't need a handler customization.
		RegisterPlatformHandlers(builder);

		// Shared view model instance across Controller/Options pages so selected
		// devices and stepper values survive navigation between the two pages.
		builder.Services.AddSingleton<MonsterBoxControllerViewModel>();

		// Registered as singletons, matching how they're actually used: AppShell
		// is resolved exactly once in App.CreateWindow, and it resolves each page
		// exactly once and holds it as permanent ShellContent.Content (see
		// AppShell.xaml.cs) - there's no scenario where a second instance of any
		// of these should exist alongside the first.
		builder.Services.AddSingleton<AppShell>();
		builder.Services.AddSingleton<MonsterBoxControllerPage>();
		builder.Services.AddSingleton<MonsterBoxOptionsPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}

	static partial void RegisterPlatformHandlers(MauiAppBuilder builder);
}
