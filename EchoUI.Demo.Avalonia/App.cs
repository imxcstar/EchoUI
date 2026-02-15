using Avalonia;
using Avalonia.Themes.Fluent;

namespace EchoUI.Demo.Avalonia;

public class App : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
