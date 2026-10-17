using Microsoft.Extensions.DependencyInjection;

using halloween.director.remote.Views;

namespace halloween.director.remote;

public partial class AppShell : Shell
{
    public AppShell(IServiceProvider services)
    {
        InitializeComponent();

        ControllerTab.Content = services.GetRequiredService<MonsterBoxControllerPage>();
        OptionsTab.Content = services.GetRequiredService<MonsterBoxOptionsPage>();
    }
}
