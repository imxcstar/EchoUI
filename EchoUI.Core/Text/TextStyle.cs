using System.Globalization;

namespace EchoUI.Core.Text;

public sealed record TextStyle(
    string? FontFamily,
    float FontSize,
    string? FontWeight,
    Color Color,
    float LetterSpacing = 0,
    float? LineHeight = null)
{
    public static TextStyle Default { get; } = new(null, 14f, null, Color.Black);

    public float EffectiveFontSize => FontSize > 0 ? FontSize : 14f;

    public string LayoutFingerprint => string.Create(CultureInfo.InvariantCulture,
        $"ff={FontFamily ?? string.Empty}|fs={EffectiveFontSize:0.###}|fw={FontWeight ?? string.Empty}|ls={LetterSpacing:0.###}|lh={(LineHeight.HasValue ? LineHeight.Value.ToString("0.###", CultureInfo.InvariantCulture) : string.Empty)}");

    public string RenderFingerprint => string.Create(CultureInfo.InvariantCulture,
        $"{LayoutFingerprint}|c={Color.R},{Color.G},{Color.B},{Color.A}");

    public TextStyle WithColor(Color color) => this with { Color = color };
}

public enum TextTrimming
{
    None,
    CharacterEllipsis,
    WordEllipsis
}
