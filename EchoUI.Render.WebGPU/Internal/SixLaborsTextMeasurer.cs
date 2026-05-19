using SixLabors.Fonts;
using EchoUI.Core;
using EchoUI.Render.Win32;

namespace EchoUI.Render.WebGPU.Internal;

/// <summary>
/// 基于 SixLabors.Fonts 的文本测量器。与 <see cref="FontAtlas"/>/<see cref="WebGpuPainter"/>
/// 共享同一组字体文件，逐码点累加 advance 并使用 painter 同款行高，保证布局与绘制完全对齐。
/// 不依赖 GPU 设备，可在首帧绘制前就创建并被布局调用。
/// </summary>
internal sealed class SixLaborsTextMeasurer
{
    private readonly FontCollection _collection = new();
    private FontFamily _primary;
    private readonly List<FontFamily> _fallbacks = new();
    private readonly Dictionary<float, Font> _fontCache = new();
    private FontMetrics? _primaryMetrics;

    // 逐 (pxSize, codepoint) 缓存 advance：与 painter 的 FontAtlas.GetGlyph(...).Advance 同源。
    private readonly Dictionary<(float pxSize, int cp), float> _advanceCache = new();

    public SixLaborsTextMeasurer(string primaryFontPath, IEnumerable<string> fallbackFontPaths)
    {
        AddFont(primaryFontPath, primary: true);
        foreach (var p in fallbackFontPaths)
        {
            if (!string.IsNullOrEmpty(p) && File.Exists(p))
                AddFont(p, primary: false);
        }
        if (_primary.TryGetMetrics(FontStyle.Regular, out var m) && m is not null)
            _primaryMetrics = m;
    }

    private void AddFont(string path, bool primary)
    {
        bool isCollection = path.EndsWith(".ttc", StringComparison.OrdinalIgnoreCase)
                         || path.EndsWith(".otc", StringComparison.OrdinalIgnoreCase);
        if (isCollection)
        {
            var fams = _collection.AddCollection(path).ToList();
            if (fams.Count == 0) return;
            if (primary) _primary = fams[0];
            else foreach (var f in fams) _fallbacks.Add(f);
        }
        else
        {
            var f = _collection.Add(path);
            if (primary) _primary = f;
            else _fallbacks.Add(f);
        }
    }

    private Font GetFont(float pxSize)
    {
        if (!_fontCache.TryGetValue(pxSize, out var font))
        {
            font = _primary.CreateFont(pxSize, FontStyle.Regular);
            _fontCache[pxSize] = font;
        }
        return font;
    }

    private float GetLineHeight(float pxSize)
    {
        if (_primaryMetrics is null) return pxSize * 1.2f;
        float scale = pxSize / _primaryMetrics.UnitsPerEm;
        var hm = _primaryMetrics.HorizontalMetrics;
        return (hm.Ascender - hm.Descender + hm.LineGap) * scale;
    }

    private float GetAdvance(float pxSize, int cp)
    {
        var key = (pxSize, cp);
        if (_advanceCache.TryGetValue(key, out var adv)) return adv;

        var font = GetFont(pxSize);
        var opts = new TextOptions(font)
        {
            FallbackFontFamilies = _fallbacks,
            Dpi = 72f,
        };
        string s = char.ConvertFromUtf32(cp);
        adv = TextMeasurer.MeasureAdvance(s, opts).Width;
        _advanceCache[key] = adv;
        return adv;
    }

    /// <summary>
    /// 与 <see cref="TextMeasurementHook.MeasureDelegate"/> 兼容的测量入口。
    /// 实现完全镜像 <see cref="WebGpuPainter"/>.DrawText 的逐码点累加 + 单行/换行高度推导，
    /// 这样布局尺寸与实际渲染必然一致。
    /// </summary>
    public TextMeasurementResult? Measure(
        string? text,
        string? fontFamily,
        float fontSize,
        string? fontWeight,
        float? widthConstraint,
        bool noWrap)
    {
        float pxSize = fontSize > 0 ? fontSize : 14f;
        float lineHeight = GetLineHeight(pxSize);

        if (string.IsNullOrEmpty(text))
            return new TextMeasurementResult(0, lineHeight);

        // 逐码点累加，与 painter 完全一致。
        float maxLineW = 0f;
        float curLineW = 0f;
        int lines = 1;
        int i = 0;
        while (i < text!.Length)
        {
            int cp;
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                cp = char.ConvertToUtf32(text[i], text[i + 1]);
                i += 2;
            }
            else
            {
                cp = text[i];
                i++;
            }

            if (cp == '\n')
            {
                if (curLineW > maxLineW) maxLineW = curLineW;
                curLineW = 0f;
                lines++;
                continue;
            }
            if (cp == '\r') continue;

            curLineW += GetAdvance(pxSize, cp);
        }
        if (curLineW > maxLineW) maxLineW = curLineW;

        // painter 当前在硬换行（'\n'）之外不会自动换行，因此 widthConstraint 对宽度的限制
        // 与渲染行为不一致；这里也保持单行/硬换行高度算法。
        return new TextMeasurementResult(maxLineW, lineHeight * lines);
    }
}

