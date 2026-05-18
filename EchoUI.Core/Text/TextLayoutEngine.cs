namespace EchoUI.Core.Text;

public static class TextLayoutEngine
{
    public static TextLayoutResult Layout(TextContent content, TextLayoutOptions options, ITextRunMeasurer measurer, TextLayoutCache? cache = null)
    {
        cache ??= TextLayoutCache.Shared;
        if (cache.TryGet(content, options, out var cached))
            return cached;

        var measured = LayoutCore(content, options, measurer);
        cache.Set(content, options, measured);
        return measured;
    }

    public static TextLayoutResult LayoutPlain(string? text, TextStyle style, TextLayoutOptions options, ITextRunMeasurer measurer, TextLayoutCache? cache = null)
    {
        var builder = new TextContentBuilder(style);
        builder.AppendText(text ?? string.Empty, style);
        return Layout(builder.Build(), options, measurer, cache);
    }

    private static TextLayoutResult LayoutCore(TextContent content, TextLayoutOptions options, ITextRunMeasurer measurer)
    {
        var lines = new List<TextLayoutLine>();
        var maxWidth = ResolveMaxWidth(options.MaxWidth);
        var y = 0f;

        foreach (var paragraph in content.Paragraphs)
        {
            if (paragraph.Runs.Count == 0)
            {
                var emptyHeight = ResolveParagraphLineHeight(paragraph, options, measurer);
                lines.Add(new TextLayoutLine([], 0, y, 0, emptyHeight, emptyHeight * 0.8f, 0, 0));
                y += emptyHeight;
                if (ReachedRawLineLimit(lines, options))
                    break;
                continue;
            }

            LayoutParagraph(paragraph, options, measurer, maxWidth, lines, ref y);
            if (ReachedRawLineLimit(lines, options))
                break;
        }

        if (lines.Count == 0)
        {
            var height = Math.Max(0, options.LineHeight ?? content.MaxFontSize * 1.25f);
            var result = new TextLayoutResult([], 0, 0);
            return result;
        }

        var visibleLimit = ResolveVisibleLineLimit(options);
        var needsTrim = options.Trimming != TextTrimming.None && lines.Count > visibleLimit;
        if (lines.Count > visibleLimit)
            lines.RemoveRange(visibleLimit, lines.Count - visibleLimit);

        if (needsTrim && lines.Count > 0)
            lines[^1] = TrimLine(lines[^1], maxWidth, options.Trimming, measurer);
        else if (options.NoWrap && options.Trimming != TextTrimming.None && lines.Count == 1 && lines[0].Width > maxWidth)
            lines[0] = TrimLine(lines[0], maxWidth, options.Trimming, measurer);

        var width = lines.Count == 0 ? 0 : lines.Max(static l => l.Width);
        var heightTotal = lines.Count == 0 ? 0 : lines[^1].Y + lines[^1].Height;
        return new TextLayoutResult(lines.ToArray(), width, heightTotal);
    }

    private static void LayoutParagraph(TextParagraph paragraph, TextLayoutOptions options, ITextRunMeasurer measurer, float maxWidth, List<TextLayoutLine> lines, ref float y)
    {
        var current = new List<TextLayoutFragment>();
        var currentWidth = 0f;
        var currentHeight = 0f;
        var currentBaseline = 0f;
        var lineStart = int.MaxValue;
        var lineEnd = 0;

        foreach (var run in paragraph.Runs)
        {
            var graphemes = TextGraphemeEnumerator.Enumerate(run.Text);
            var segmentStart = 0;
            var segmentWidth = 0f;
            var lastBreakGrapheme = -1;
            var lastBreakWidth = 0f;

            for (var i = 0; i < graphemes.Count; i++)
            {
                var candidateText = run.Text.Substring(segmentStart, graphemes[i].End - segmentStart);
                var candidateWidth = MeasureWidth(candidateText, run.Style, measurer);
                var wouldOverflow = !options.NoWrap && currentWidth + candidateWidth > maxWidth && (current.Count > 0 || segmentStart < graphemes[i].Start);

                if (wouldOverflow)
                {
                    if (lastBreakGrapheme >= segmentStart)
                    {
                        var end = graphemes[lastBreakGrapheme].End;
                        AppendFragment(current, run, segmentStart, end - segmentStart, currentWidth, y, lastBreakWidth, measurer, ref currentHeight, ref currentBaseline, ref lineStart, ref lineEnd);
                        CommitLine(lines, current, currentWidth + lastBreakWidth, currentHeight, currentBaseline, lineStart, lineEnd, y);
                        y += currentHeight;
                        ResetLine(current, ref currentWidth, ref currentHeight, ref currentBaseline, ref lineStart, ref lineEnd);
                        segmentStart = end;
                        i = Math.Max(lastBreakGrapheme, 0);
                    }
                    else if (segmentStart < graphemes[i].Start)
                    {
                        AppendFragment(current, run, segmentStart, graphemes[i].Start - segmentStart, currentWidth, y, segmentWidth, measurer, ref currentHeight, ref currentBaseline, ref lineStart, ref lineEnd);
                        CommitLine(lines, current, currentWidth + segmentWidth, currentHeight, currentBaseline, lineStart, lineEnd, y);
                        y += currentHeight;
                        ResetLine(current, ref currentWidth, ref currentHeight, ref currentBaseline, ref lineStart, ref lineEnd);
                        segmentStart = graphemes[i].Start;
                        i--;
                    }
                    else
                    {
                        var single = graphemes[i].Value;
                        var singleWidth = MeasureWidth(single, run.Style, measurer);
                        AppendFragment(current, run, graphemes[i].Start, graphemes[i].Length, currentWidth, y, singleWidth, measurer, ref currentHeight, ref currentBaseline, ref lineStart, ref lineEnd);
                        CommitLine(lines, current, currentWidth + singleWidth, currentHeight, currentBaseline, lineStart, lineEnd, y);
                        y += currentHeight;
                        ResetLine(current, ref currentWidth, ref currentHeight, ref currentBaseline, ref lineStart, ref lineEnd);
                        segmentStart = graphemes[i].End;
                    }

                    segmentWidth = 0;
                    lastBreakGrapheme = -1;
                    lastBreakWidth = 0;

                    if (ReachedRawLineLimit(lines, options))
                        return;
                    continue;
                }

                segmentWidth = candidateWidth;
                if (TextBreakEngine.IsPreferredBreakAfter(run.Text, graphemes[i].End - 1))
                {
                    lastBreakGrapheme = i;
                    lastBreakWidth = segmentWidth;
                }
            }

            if (segmentStart < run.Text.Length)
            {
                AppendFragment(current, run, segmentStart, run.Text.Length - segmentStart, currentWidth, y, segmentWidth, measurer, ref currentHeight, ref currentBaseline, ref lineStart, ref lineEnd);
                currentWidth += segmentWidth;
            }
        }

        if (current.Count > 0)
        {
            CommitLine(lines, current, currentWidth, currentHeight, currentBaseline, lineStart, lineEnd, y);
            y += currentHeight;
        }
    }

    private static void AppendFragment(List<TextLayoutFragment> fragments, TextRun run, int runOffset, int length, float x, float y, float width, ITextRunMeasurer measurer, ref float lineHeight, ref float baseline, ref int lineStart, ref int lineEnd)
    {
        if (length <= 0)
            return;

        var text = run.Text.Substring(runOffset, length);
        var measurement = measurer.Measure(text, run.Style);
        var fragmentHeight = ResolveLineHeight(run.Style, measurement);
        var fragmentBaseline = measurement.Baseline > 0 ? measurement.Baseline : fragmentHeight * 0.8f;
        var textStart = run.TextStart + runOffset;
        fragments.Add(new TextLayoutFragment(text, run.Style, x, y, width, fragmentHeight, fragmentBaseline, textStart, length));
        lineHeight = Math.Max(lineHeight, fragmentHeight);
        baseline = Math.Max(baseline, fragmentBaseline);
        lineStart = Math.Min(lineStart, textStart);
        lineEnd = Math.Max(lineEnd, textStart + length);
    }

    private static void CommitLine(List<TextLayoutLine> lines, List<TextLayoutFragment> fragments, float width, float height, float baseline, int start, int end, float y)
    {
        height = height > 0 ? height : 1;
        baseline = baseline > 0 ? baseline : height * 0.8f;
        var normalized = new TextLayoutFragment[fragments.Count];
        for (var i = 0; i < fragments.Count; i++)
            normalized[i] = fragments[i] with { Y = y, Height = height, Baseline = baseline };
        lines.Add(new TextLayoutLine(normalized, 0, y, width, height, baseline, start == int.MaxValue ? end : start, end));
    }

    private static TextLayoutLine TrimLine(TextLayoutLine line, float maxWidth, TextTrimming trimming, ITextRunMeasurer measurer)
    {
        if (line.Fragments.Count == 0 || maxWidth <= 0)
            return line;

        var fragments = line.Fragments.Select(static f => f with { }).ToList();
        var style = fragments.Last(static f => f.Text.Length > 0).Style;
        var ellipsisWidth = MeasureWidth("…", style, measurer);

        if (maxWidth <= ellipsisWidth)
        {
            var m = measurer.Measure("…", style);
            var h = ResolveLineHeight(style, m);
            return new TextLayoutLine([new TextLayoutFragment("…", style, 0, line.Y, ellipsisWidth, h, m.Baseline, line.StartTextIndex, 0)], 0, line.Y, ellipsisWidth, h, m.Baseline, line.StartTextIndex, line.StartTextIndex);
        }

        while (fragments.Count > 0 && fragments.Sum(static f => f.Width) + ellipsisWidth > maxWidth)
        {
            var last = fragments[^1];
            var text = TextGraphemeEnumerator.RemoveLastGrapheme(last.Text);
            if (trimming == TextTrimming.WordEllipsis)
                text = TrimToWordBoundary(text);

            if (text.Length == 0)
            {
                fragments.RemoveAt(fragments.Count - 1);
            }
            else
            {
                fragments[^1] = last with { Text = text, Width = MeasureWidth(text, last.Style, measurer), TextLength = text.Length };
            }
        }

        var endIndex = fragments.Count == 0 ? line.StartTextIndex : fragments[^1].TextEnd;
        fragments.Add(new TextLayoutFragment("…", style, 0, line.Y, ellipsisWidth, line.Height, line.Baseline, endIndex, 0));
        return Normalize(line, fragments);
    }

    private static TextLayoutLine Normalize(TextLayoutLine source, List<TextLayoutFragment> fragments)
    {
        var x = 0f;
        var normalized = new TextLayoutFragment[fragments.Count];
        for (var i = 0; i < fragments.Count; i++)
        {
            normalized[i] = fragments[i] with { X = x, Y = source.Y };
            x += fragments[i].Width;
        }
        var start = normalized.Length == 0 ? source.StartTextIndex : normalized.Min(static f => f.TextStart);
        var end = normalized.Length == 0 ? source.EndTextIndex : normalized.Max(static f => f.TextEnd);
        return source with { Fragments = normalized, Width = x, StartTextIndex = start, EndTextIndex = end };
    }

    private static string TrimToWordBoundary(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;
        for (var i = text.Length - 1; i > 0; i--)
        {
            if (TextBreakEngine.IsWordBoundaryBefore(text, i))
                return text[..i].TrimEnd();
        }
        return text;
    }

    private static void ResetLine(List<TextLayoutFragment> current, ref float width, ref float height, ref float baseline, ref int start, ref int end)
    {
        current.Clear();
        width = 0;
        height = 0;
        baseline = 0;
        start = int.MaxValue;
        end = 0;
    }

    private static float MeasureWidth(string text, TextStyle style, ITextRunMeasurer measurer)
    {
        if (string.IsNullOrEmpty(text))
            return 0;
        var measurement = measurer.Measure(text, style);
        return measurement.Width + Math.Max(0, TextGraphemeEnumerator.Count(text) - 1) * Math.Max(0, style.LetterSpacing);
    }

    private static float ResolveLineHeight(TextStyle style, TextRunMeasurement measurement)
    {
        return style.LineHeight is > 0 ? style.LineHeight.Value : Math.Max(measurement.Height, style.EffectiveFontSize * 1.2f);
    }

    private static float ResolveParagraphLineHeight(TextParagraph paragraph, TextLayoutOptions options, ITextRunMeasurer measurer)
    {
        if (options.LineHeight is > 0)
            return options.LineHeight.Value;

        var style = paragraph.Runs.FirstOrDefault()?.Style ?? TextStyle.Default;
        return ResolveLineHeight(style, measurer.Measure(string.Empty, style));
    }

    private static int ResolveVisibleLineLimit(TextLayoutOptions options)
    {
        if (options.MaxLines > 0)
            return options.MaxLines;
        if (options.NoWrap && options.Trimming != TextTrimming.None)
            return 1;
        return int.MaxValue;
    }

    private static bool ReachedRawLineLimit(List<TextLayoutLine> lines, TextLayoutOptions options)
    {
        var limit = ResolveVisibleLineLimit(options);
        return options.Trimming != TextTrimming.None && lines.Count > limit;
    }

    private static float ResolveMaxWidth(float maxWidth)
    {
        return float.IsPositiveInfinity(maxWidth) || maxWidth <= 0 ? float.PositiveInfinity : Math.Max(1, maxWidth);
    }
}
