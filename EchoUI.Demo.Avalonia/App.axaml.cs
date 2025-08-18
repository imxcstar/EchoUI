using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using EchoUI.Core;
using EchoUI.Render.Avalonia;

namespace EchoUI.Demo.Avalonia;

public partial class App : Application
{
    private static Reconciler? _reconciler;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var renderer = new AvaloniaRenderer();
        var dockPanel = new DockPanel();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new Window();
            desktop.MainWindow.Title = "EchoUI.Demo";
            desktop.MainWindow.Content = dockPanel;
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = dockPanel;
        }

        _reconciler = new Reconciler(renderer, dockPanel);
        _reconciler.Mount(Demo.Render);

        base.OnFrameworkInitializationCompleted();
    }
}
