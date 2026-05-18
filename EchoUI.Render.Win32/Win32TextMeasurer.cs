using EchoUI.Core;

namespace EchoUI.Render.Win32;

internal sealed class Win32TextMeasurer : ITextMeasurer
{
    public TextMeasurementResult Measure(TextMeasurementRequest request)
    {
        return GdiText.MeasureText(request.Text, request.FontFamily, request.FontSize ?? 14f, request.FontWeight);
    }
}
