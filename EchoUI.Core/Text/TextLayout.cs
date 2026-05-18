using System.Globalization;

namespace EchoUI.Core.Text;

public sealed record TextLayoutOptions(
    float MaxWidth,
    bool NoWrap = false,
    int MaxLines = 0,
    TextTrimming Trimming = TextTrimming.None,
    float? LineHeight = null)
{
    public string Fingerprint => string.Create(CultureInfo.InvariantCulture,
        $"mw={MaxWidth:0.###}|nw={NoWrap}|ml={MaxLines}|tr={Trimming}|lh={(LineHeight.HasValue ? LineHeight.Value.ToString("0.###", CultureInfo.InvariantCulture) : string.Empty)}");
}

public sealed class TextLayoutResult
{
    public TextLayoutResult(IReadOnlyList<TextLayoutLine> lines, float width, float height)
    {
        Lines = lines;
        Width = width;
        Height = height;
    }

    public IReadOnlyList<TextLayoutLine> Lines { get; }
    public float Width { get; }
    public float Height { get; }
    public int LineCount => Lines.Count;
}

public sealed record TextLayoutLine(
    IReadOnlyList<TextLayoutFragment> Fragments,
    float X,
    float Y,
    float Width,
    float Height,
    float Baseline,
    int StartTextIndex,
    int EndTextIndex);

public sealed record TextLayoutFragment(
    string Text,
    TextStyle Style,
    float X,
    float Y,
    float Width,
    float Height,
    float Baseline,
    int TextStart,
    int TextLength)
{
    public int TextEnd => TextStart + TextLength;
}
