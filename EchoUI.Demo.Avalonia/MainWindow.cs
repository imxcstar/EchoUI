using Avalonia.Controls;
using EchoUI.Core;
using EchoUI.Render.Avalonia;

namespace EchoUI.Demo.Avalonia;

public class MainWindow : Window
{
    public MainWindow()
    {
        Title = "EchoUI Avalonia Demo";
        Width = 1200;
        Height = 800;

        var rootPanel = new EchoUIPanel();
        Content = rootPanel;

        Opened += async (_, _) =>
        {
            var renderer = new AvaloniaRenderer(rootPanel);
            var reconciler = new Reconciler(renderer, rootPanel);
            await reconciler.Mount(Demo.Render);
        };
    }
}
