using EchoUI.Core.Text;

namespace EchoUI.Render.Win32;

internal sealed class GdiTextRunMeasurer : ITextRunMeasurer
{
    public TextRunMeasurement Measure(string text, TextStyle style)
    {
        var result = GdiText.MeasureText(text, style.FontFamily, style.EffectiveFontSize, style.FontWeight, noWrap: true);
        var height = style.LineHeight is > 0 ? style.LineHeight.Value : result.Height;
        return new TextRunMeasurement(result.Width, height, height * 0.8f);
    }
}
