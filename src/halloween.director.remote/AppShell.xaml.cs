using Microsoft.Extensions.DependencyInjection;

using Halloween.Director.Remote.Views;

namespace Halloween.Director.Remote;

public partial class AppShell : Shell
{
    public AppShell(IServiceProvider services)
    {
        InitializeComponent();

        ControllerTab.Content = services.GetRequiredService<MonsterBoxControllerPage>();
        OptionsTab.Content = services.GetRequiredService<MonsterBoxOptionsPage>();
    }
}
