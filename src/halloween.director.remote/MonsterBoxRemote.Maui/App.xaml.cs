using Microsoft.Extensions.DependencyInjection;

namespace MonsterBoxRemote.Maui;

public partial class App : Application
{
	private readonly IServiceProvider _services;

	public App(IServiceProvider services)
	{
		InitializeComponent();
		_services = services;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		// AppShell (and the pages its tabs hold) is resolved here, lazily, rather
		// than constructor-injected into App - constructor-injecting a page forces
		// DI to build it (parsing its XAML, constructing its handler-backed
		// elements) before the native WinUI3 Window/dispatcher exists for it to be
		// threaded into, which reliably fail-fast crashes WinUI3/Windows
		// (0xc000027b in Microsoft.UI.Xaml.dll). Isolated and confirmed with a
		// minimal repro: a stock `dotnet new maui` app crashes identically the
		// moment App(SomePage) constructor injection is added, and stops crashing
		// once it's resolved inside CreateWindow() instead. Don't revert this
		// pattern.
		var shell = _services.GetRequiredService<AppShell>();

		return new Window(shell);
	}
}
