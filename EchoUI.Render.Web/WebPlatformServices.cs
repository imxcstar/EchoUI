using System.Threading.Tasks;
using EchoUI.Core;

namespace EchoUI.Render.Web;

internal sealed class WebPlatformServices : IPlatformServices
{
    public WebPlatformServices()
    {
        TextMeasurer = new WebTextMeasurer();
        Clipboard = new WebClipboardService();
    }

    public ITextMeasurer TextMeasurer { get; }

    public IClipboardService Clipboard { get; }
}

internal sealed class WebTextMeasurer : ITextMeasurer
{
    public TextMeasurementResult Measure(TextMeasurementRequest request)
    {
        var text = request.Text ?? string.Empty;
        var fontSize = request.FontSize ?? 14f;
        var width = (float)DomInterop.MeasureText(text, request.FontFamily, fontSize, request.FontWeight);
        return new TextMeasurementResult(width, fontSize);
    }
}

internal sealed class WebClipboardService : IClipboardService
{
    public Task<string> ReadTextAsync()
    {
        return DomInterop.ReadClipboardText();
    }

    public Task WriteTextAsync(string text)
    {
        return DomInterop.WriteClipboardText(text ?? string.Empty);
    }
}
