using System.Numerics;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using WebGPU;
using static WebGPU.WebGPU;

namespace EchoUI.Render.WebGPU.Internal;

/// <summary>
/// 基于 SixLabors.Fonts + SixLabors.ImageSharp.Drawing 的文本 run atlas，public 表面与
/// <see cref="GdiTextAtlas"/> 完全一致：一条文本运行（同一字体/字号/字重 + 文本内容）作为
/// atlas 上的一个矩形 region，painter 用单个 quad 渲染整条文本。
///
/// 用途：跨平台后端（非 Windows / Wayland / 无 GDI 环境）可用本类替代 GdiTextAtlas，
/// painter 调用方式不变。Windows 桌面仍建议走 GdiTextAtlas —— SixLabors.Fonts 没有
/// TrueType hinting bytecode 解释器，12–14px 小字号竖笔会发灰。
/// </summary>
public sealed unsafe class FontAtlas : IDisposable
{
    private const int InitialAtlasSize = 1024;
    private const int Padding = 1;

    private readonly WGPUDevice _device;
    private readonly WGPUQueue _queue;

    private readonly FontCollection _collection = new();
    private FontFamily _primaryFamily;
    private readonly List<FontFamily> _fallbackFamilies = new();
    // 按 family-name 查找已注册字体；不命中则退回 _primaryFamily。
    private readonly Dictionary<string, FontFamily> _familyByName =
        new(StringComparer.OrdinalIgnoreCase);
    // (family-name, pxSize, weight) → Font 缓存。
    private readonly Dictionary<(string Family, float Size, string? Weight), Font> _fontCache = new();

    private int _atlasW;
    private int _atlasH;
    private byte[] _cpuAtlas = Array.Empty<byte>();
    private int _shelfX;
    private int _shelfY;
    private int _shelfH;

    // Stem darkening 查找表：cov_out = pow(cov_in/255, StemDarkenExp) * 255。
    // 0.55 接近 FreeType / Chrome / macOS 在小字号下的笔画加粗强度；
    // 没有 TrueType hinting 时，这是让 SimSun / 雅黑 等 CJK 字体在 12–14px 下
    // 笔画看起来不发灰、不发幼的关键步骤。
    private const double StemDarkenExp = 0.55;
    private static readonly byte[] s_stemLut = BuildStemLut();

    private static byte[] BuildStemLut()
    {
        var lut = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            double c = i / 255.0;
            double o = Math.Pow(c, StemDarkenExp);
            int v = (int)Math.Round(o * 255.0);
            if (v < 0) v = 0; else if (v > 255) v = 255;
            lut[i] = (byte)v;
        }
        return lut;
    }

    public WGPUTexture Texture;
    public WGPUTextureView TextureView;

    /// <summary>
    /// 一条文本运行在 atlas 上的位置与尺寸（与 <see cref="GdiTextAtlas.Run"/> 字段一致）。
    /// </summary>
    public struct Run
    {
        public float U0, V0, U1, V1;
        public int W, H;
    }

    private readonly struct RunKey : IEquatable<RunKey>
    {
        public readonly string Text;
        public readonly string Family;
        public readonly float Size;
        public readonly string? Weight;
        public RunKey(string text, string family, float size, string? weight)
        { Text = text; Family = family; Size = size; Weight = weight; }
        public bool Equals(RunKey o)
            => Size == o.Size
            && string.Equals(Text, o.Text, StringComparison.Ordinal)
            && string.Equals(Family, o.Family, StringComparison.Ordinal)
            && string.Equals(Weight, o.Weight, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is RunKey k && Equals(k);
        public override int GetHashCode() => HashCode.Combine(Text, Family, Size, Weight);
    }

    private readonly Dictionary<RunKey, Run> _cache = new();

    /// <summary>
    /// 测量整段文本的像素宽高（与 <see cref="GetRun"/> 用同一字体/同一 TextOptions，
    /// 保证布局测量与栅格化绝对对齐）。支持多行（按 '\n' 拆开取最宽行）。
    /// </summary>
    public (float Width, float Height) MeasureText(string? text, string? fontFamily, float fontSize, string? fontWeight)
    {
        // 行盒高度同 GetRun。
        float ascent = fontSize, descent = 0, lineGap = 0;
        if (_primaryFamily.TryGetMetrics(FontStyle.Regular, out var pm) && pm is not null)
        {
            float scale = fontSize / pm.UnitsPerEm;
            ascent = pm.HorizontalMetrics.Ascender * scale;
            descent = pm.HorizontalMetrics.Descender * scale;
            lineGap = pm.HorizontalMetrics.LineGap * scale;
        }
        float lineHeight = ascent - descent + lineGap;
        if (string.IsNullOrEmpty(text))
            return (0f, lineHeight);

        var font = GetFont(fontFamily, fontSize, fontWeight);
        var opts = new TextOptions(font)
        {
            FallbackFontFamilies = _fallbackFamilies,
            Dpi = 72f,
        };

        float maxLineW = 0;
        int lines = 0;
        int start = 0;
        while (start <= text.Length)
        {
            int end = text.IndexOf('\n', start);
            if (end < 0) end = text.Length;
            int lineEnd = end;
            if (lineEnd > start && text[lineEnd - 1] == '\r') lineEnd--;
            string line = text.Substring(start, lineEnd - start);
            float w = string.IsNullOrEmpty(line) ? 0 : TextMeasurer.MeasureAdvance(line, opts).Width;
            if (w > maxLineW) maxLineW = w;
            lines++;
            if (end >= text.Length) break;
            start = end + 1;
        }
        if (lines == 0) lines = 1;
        return (maxLineW, lineHeight * lines);
    }

    public FontAtlas(WGPUDevice device, WGPUQueue queue)
    {
        _device = device;
        _queue = queue;
        CreateAtlas(InitialAtlasSize, InitialAtlasSize);
    }

    /// <summary>设置主字体（也作为未知 family 名的回退）。</summary>
    public void LoadFont(string path) => AddFont(path, asPrimary: true);

    /// <summary>追加一个 fallback 字体（缺字时按注册顺序回退）。</summary>
    public void AddFallback(string path)
    {
        if (!File.Exists(path)) return;
        try { AddFont(path, asPrimary: false); } catch { /* ignore broken fallback */ }
    }

    private void AddFont(string path, bool asPrimary)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Font not found: " + path);

        // .ttc / .otc 走 collection；其余按单文件。
        bool isCollection = path.EndsWith(".ttc", StringComparison.OrdinalIgnoreCase)
                         || path.EndsWith(".otc", StringComparison.OrdinalIgnoreCase);

        IEnumerable<FontFamily> added;
        if (isCollection)
        {
            added = _collection.AddCollection(path).ToList();
            if (!added.Any())
                throw new InvalidOperationException("Empty font collection: " + path);
        }
        else
        {
            added = new[] { _collection.Add(path) };
        }

        foreach (var fam in added)
        {
            _familyByName[fam.Name] = fam;
            if (asPrimary)
            {
                _primaryFamily = fam;
                asPrimary = false; // 集合里只把第一项作主字体
            }
            else
            {
                _fallbackFamilies.Add(fam);
            }
        }
    }

    private Font GetFont(string? fontFamily, float pxSize, string? fontWeight)
    {
        string family = !string.IsNullOrEmpty(fontFamily) && _familyByName.ContainsKey(fontFamily)
            ? fontFamily
            : _primaryFamily.Name;
        var key = (family, pxSize, fontWeight);
        if (_fontCache.TryGetValue(key, out var font)) return font;
        var fam = _familyByName.TryGetValue(family, out var f) ? f : _primaryFamily;
        var style = string.Equals(fontWeight, "bold", StringComparison.OrdinalIgnoreCase)
            ? FontStyle.Bold
            : FontStyle.Regular;
        font = fam.CreateFont(pxSize, style);
        _fontCache[key] = font;
        return font;
    }

    /// <summary>
    /// 获取一条单行文本的 atlas region；多行调用方应先按 '\n' 拆开。与
    /// <see cref="GdiTextAtlas.GetRun"/> 行为对齐：返回的 W/H 即可作为绘制矩形大小。
    /// </summary>
    public Run GetRun(string text, string? fontFamily, float fontSize, string? fontWeight)
    {
        string resolvedFamily = !string.IsNullOrEmpty(fontFamily) && _familyByName.ContainsKey(fontFamily)
            ? fontFamily
            : (_primaryFamily.Name ?? string.Empty);
        var key = new RunKey(text, resolvedFamily, fontSize, fontWeight);
        if (_cache.TryGetValue(key, out var cached)) return cached;

        var font = GetFont(resolvedFamily, fontSize, fontWeight);

        // 行盒高度：与 GDI 的 TEXTMETRIC.tmHeight 语义一致 —— ascent - descent + lineGap。
        // descent 在 SixLabors 是负数（基线下方），故直接 (ascent - descent) 已包含下行。
        float ascent = fontSize, descent = 0, lineGap = 0;
        if (_primaryFamily.TryGetMetrics(FontStyle.Regular, out var pm) && pm is not null)
        {
            float scale = fontSize / pm.UnitsPerEm;
            ascent = pm.HorizontalMetrics.Ascender * scale;
            descent = pm.HorizontalMetrics.Descender * scale;
            lineGap = pm.HorizontalMetrics.LineGap * scale;
        }
        int h = Math.Max(1, (int)Math.Ceiling(ascent - descent + lineGap));

        // 宽度：用 SixLabors 整段 advance。包含 fallback 链与 kerning（与栅格化同条 TextOptions）。
        var measureOpts = new TextOptions(font)
        {
            FallbackFontFamilies = _fallbackFamilies,
            Dpi = 72f,
        };
        float advance = string.IsNullOrEmpty(text) ? 0f : TextMeasurer.MeasureAdvance(text, measureOpts).Width;
        int w = Math.Max(1, (int)Math.Ceiling(advance));

        AllocShelf(w, h, out int gx, out int gy);

        if (!string.IsNullOrEmpty(text))
        {
            // 渲染整条文本到 L8 —— 单通道 luminance 直接当作覆盖率拷贝到 R8 atlas。
            using var img = new Image<L8>(w, h);
            var drawOpts = new RichTextOptions(font)
            {
                FallbackFontFamilies = _fallbackFamilies,
                Dpi = 72f,
                // 行盒顶部在 y=0，基线在 y=ascent —— 与 GDI DrawText(DT_LEFT|DT_TOP) 一致。
                Origin = new Vector2(0, 0),
            };
            img.Mutate(ctx => ctx.DrawText(drawOpts, text, SixLabors.ImageSharp.Color.White));

            img.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < h; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    int dstOffset = (gy + y) * _atlasW + gx;
                    int n = Math.Min(row.Length, w);
                    for (int x = 0; x < n; x++)
                        _cpuAtlas[dstOffset + x] = s_stemLut[row[x].PackedValue];
                }
            });
        }

        UploadAtlasRegion(gx, gy, w, h);
        _shelfX += w + Padding;
        if (h > _shelfH) _shelfH = h;

        var run = new Run
        {
            U0 = gx / (float)_atlasW,
            V0 = gy / (float)_atlasH,
            U1 = (gx + w) / (float)_atlasW,
            V1 = (gy + h) / (float)_atlasH,
            W = w,
            H = h,
        };
        _cache[key] = run;
        return run;
    }

    private void AllocShelf(int w, int h, out int gx, out int gy)
    {
        if (_shelfX + w + Padding > _atlasW)
        {
            _shelfX = 0;
            _shelfY += _shelfH + Padding;
            _shelfH = 0;
        }
        while (_shelfY + h + Padding > _atlasH)
            GrowAtlas(_atlasW * 2, _atlasH * 2);

        gx = _shelfX;
        gy = _shelfY;
    }

    private void CreateAtlas(int width, int height)
    {
        _atlasW = width;
        _atlasH = height;
        _cpuAtlas = new byte[width * height];
        _shelfX = 0;
        _shelfY = 0;
        _shelfH = 0;

        WGPUTextureDescriptor desc = new()
        {
            usage = WGPUTextureUsage.TextureBinding | WGPUTextureUsage.CopyDst,
            dimension = WGPUTextureDimension._2D,
            size = new WGPUExtent3D { width = (uint)width, height = (uint)height, depthOrArrayLayers = 1 },
            format = WGPUTextureFormat.R8Unorm,
            mipLevelCount = 1,
            sampleCount = 1,
            viewFormatCount = 0,
            viewFormats = null,
        };
        Texture = wgpuDeviceCreateTexture(_device, &desc);
        TextureView = wgpuTextureCreateView(Texture, null);
    }

    private void GrowAtlas(int newW, int newH)
    {
        var oldCpu = _cpuAtlas;
        int oldW = _atlasW;
        int oldH = _atlasH;
        if (TextureView.IsNotNull) wgpuTextureViewRelease(TextureView);
        if (Texture.IsNotNull) { wgpuTextureDestroy(Texture); wgpuTextureRelease(Texture); }
        CreateAtlas(newW, newH);
        for (int y = 0; y < oldH; y++)
            Buffer.BlockCopy(oldCpu, y * oldW, _cpuAtlas, y * _atlasW, oldW);
        UploadAtlasRegion(0, 0, oldW, oldH);
        float sx = (float)oldW / _atlasW;
        float sy = (float)oldH / _atlasH;
        var keys = new List<RunKey>(_cache.Keys);
        foreach (var k in keys)
        {
            var r = _cache[k];
            r.U0 *= sx; r.U1 *= sx;
            r.V0 *= sy; r.V1 *= sy;
            _cache[k] = r;
        }
    }

    private void UploadAtlasRegion(int x, int y, int w, int h)
    {
        if (w <= 0 || h <= 0) return;
        WGPUTexelCopyTextureInfo dst = new()
        {
            texture = Texture,
            mipLevel = 0,
            origin = new WGPUOrigin3D { x = (uint)x, y = (uint)y, z = 0 },
            aspect = WGPUTextureAspect.All
        };
        WGPUTexelCopyBufferLayout layout = new()
        {
            offset = 0,
            bytesPerRow = (uint)_atlasW,
            rowsPerImage = (uint)_atlasH,
        };
        WGPUExtent3D size = new() { width = (uint)w, height = (uint)h, depthOrArrayLayers = 1 };

        fixed (byte* p = _cpuAtlas)
        {
            byte* src = p + y * _atlasW + x;
            nuint dataSize = (nuint)((h - 1) * _atlasW + w);
            wgpuQueueWriteTexture(_queue, &dst, src, dataSize, &layout, &size);
        }
    }

    public void Dispose()
    {
        if (TextureView.IsNotNull) wgpuTextureViewRelease(TextureView);
        if (Texture.IsNotNull) { wgpuTextureDestroy(Texture); wgpuTextureRelease(Texture); }
        _fallbackFamilies.Clear();
        _familyByName.Clear();
        _fontCache.Clear();
        _cache.Clear();
    }
}
