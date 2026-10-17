using Microsoft.Extensions.DependencyInjection;

using MonsterBoxRemote.Maui.Views;

namespace MonsterBoxRemote.Maui;

public partial class AppShell : Shell
{
    public AppShell(IServiceProvider services)
    {
        InitializeComponent();

        ControllerTab.Content = services.GetRequiredService<MonsterBoxControllerPage>();
        OptionsTab.Content = services.GetRequiredService<MonsterBoxOptionsPage>();
    }
}
