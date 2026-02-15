using Avalonia;
using EchoUI.Demo.Avalonia;

AppBuilder.Configure<App>()
    .UsePlatformDetect()
    .StartWithClassicDesktopLifetime(args);
