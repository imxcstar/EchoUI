using EchoUI.Core;
using EchoUI.Core.Text;

namespace EchoUI.Render.Win32;

internal sealed class Win32TextMeasurer : ITextMeasurer
{
    private static readonly ITextRunMeasurer Measurer = new CachingTextRunMeasurer(new GdiTextRunMeasurer());

    public TextMeasurementResult Measure(TextMeasurementRequest request)
    {
        var style = new TextStyle(request.FontFamily, request.FontSize ?? 14f, request.FontWeight, Color.Black);
        var layout = TextLayoutEngine.LayoutPlain(request.Text, style, new TextLayoutOptions(float.PositiveInfinity, NoWrap: true), Measurer);
        return new TextMeasurementResult(layout.Width, layout.Height > 0 ? layout.Height : Measurer.Measure(request.Text, style).Height);
    }
}
