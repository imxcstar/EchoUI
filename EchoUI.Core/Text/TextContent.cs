using System.Text;

namespace EchoUI.Core.Text;

public sealed record TextRun(string Text, TextStyle Style, int TextStart)
{
    public int TextEnd => TextStart + Text.Length;
}

public sealed class TextParagraph
{
    public TextParagraph(IReadOnlyList<TextRun> runs)
    {
        Runs = runs;
        LayoutFingerprint = CreateFingerprint(runs, render: false);
        RenderFingerprint = CreateFingerprint(runs, render: true);
    }

    public IReadOnlyList<TextRun> Runs { get; }
    public string LayoutFingerprint { get; }
    public string RenderFingerprint { get; }

    private static string CreateFingerprint(IReadOnlyList<TextRun> runs, bool render)
    {
        var builder = new StringBuilder();
        foreach (var run in runs)
        {
            var style = render ? run.Style.RenderFingerprint : run.Style.LayoutFingerprint;
            builder.Append(style.Length).Append(':').Append(style).Append('/');
            builder.Append(run.Text.Length).Append(':').Append(run.Text).Append(';');
        }
        return builder.ToString();
    }
}

public sealed class TextContent
{
    public TextContent(IReadOnlyList<TextParagraph> paragraphs, string text, float maxFontSize)
    {
        Paragraphs = paragraphs;
        Text = text;
        MaxFontSize = maxFontSize;
        LayoutFingerprint = string.Join("|p|", paragraphs.Select(static p => p.LayoutFingerprint));
        RenderFingerprint = string.Join("|p|", paragraphs.Select(static p => p.RenderFingerprint));
    }

    public IReadOnlyList<TextParagraph> Paragraphs { get; }
    public string Text { get; }
    public float MaxFontSize { get; }
    public string LayoutFingerprint { get; }
    public string RenderFingerprint { get; }
}

public sealed class TextContentBuilder
{
    private readonly List<TextParagraph> _paragraphs = [];
    private readonly List<TextRun> _runs = [];
    private readonly StringBuilder _text = new();
    private float _maxFontSize;

    public TextContentBuilder(TextStyle baseStyle)
    {
        BaseStyle = baseStyle;
        _maxFontSize = baseStyle.EffectiveFontSize;
    }

    public TextStyle BaseStyle { get; }

    public void AppendText(string? text, TextStyle? style = null)
    {
        if (string.IsNullOrEmpty(text))
            return;

        style ??= BaseStyle;
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '\r' && text[i] != '\n')
                continue;

            if (i > start)
                AppendRun(text[start..i], style);

            if (text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                i++;

            AppendLineBreak();
            start = i + 1;
        }

        if (start < text.Length)
            AppendRun(text[start..], style);
    }

    public void AppendLineBreak()
    {
        FlushParagraph(allowEmpty: true);
        _text.Append('\n');
    }

    public TextContent Build()
    {
        FlushParagraph(allowEmpty: false);
        return new TextContent(_paragraphs.ToArray(), _text.ToString(), _maxFontSize);
    }

    private void AppendRun(string text, TextStyle style)
    {
        if (text.Length == 0)
            return;

        _runs.Add(new TextRun(text, style, _text.Length));
        _text.Append(text);
        _maxFontSize = Math.Max(_maxFontSize, style.EffectiveFontSize);
    }

    private void FlushParagraph(bool allowEmpty)
    {
        if (!allowEmpty && _runs.Count == 0)
            return;

        _paragraphs.Add(new TextParagraph(_runs.ToArray()));
        _runs.Clear();
    }
}
