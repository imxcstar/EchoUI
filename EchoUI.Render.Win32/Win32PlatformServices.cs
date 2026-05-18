using EchoUI.Core;

namespace EchoUI.Render.Win32;

internal sealed class Win32PlatformServices : IPlatformServices
{
    public Win32PlatformServices()
    {
        TextMeasurer = new Win32TextMeasurer();
        Clipboard = new Win32ClipboardService();
    }

    public ITextMeasurer TextMeasurer { get; }

    public IClipboardService Clipboard { get; }
}
