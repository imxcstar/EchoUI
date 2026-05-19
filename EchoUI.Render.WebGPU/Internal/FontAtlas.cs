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
/// 基于 SixLabors.Fonts + SixLabors.ImageSharp.Drawing 的字体字形栅格化与 R8 atlas。
/// 单 atlas、单主字体 + 多个回退字体，多 pixel size + codepoint 共享一张纹理。
/// 简单 shelf packing；空间不够则扩容（重建一张更大的）。
/// </summary>
internal sealed unsafe class FontAtlas : IDisposable
{
    private const int InitialAtlasSize = 1024;
    private const int Padding = 1;

    private readonly WGPUDevice _device;
    private readonly WGPUQueue _queue;

    private readonly FontCollection _collection = new();
    private FontFamily _primaryFamily;
    private readonly List<FontFamily> _fallbackFamilies = new();
    // 主字体在不同 pxSize 上的 Font 实例缓存（避免每个码点重建）
    private readonly Dictionary<float, Font> _primaryFontCache = new();

    private int _atlasW;
    private int _atlasH;
    private byte[] _cpuAtlas = Array.Empty<byte>();
    private int _shelfX;
    private int _shelfY;
    private int _shelfH;

    public WGPUTexture Texture;
    public WGPUTextureView TextureView;

    private readonly Dictionary<(float pxSize, int codepoint), Glyph> _cache = new();

    public struct Glyph
    {
        public float U0, V0, U1, V1;     // atlas uv
        public int W, H;                 // glyph bitmap size in px
        public int XOff, YOff;           // offset from (penX, baselineY) to bitmap top-left
        public float Advance;            // horizontal advance in px
        public bool Empty;               // whitespace glyph (no bitmap)
    }

    public FontAtlas(WGPUDevice device, WGPUQueue queue)
    {
        _device = device;
        _queue = queue;
    }

    public void LoadFont(string path)
    {
        AddFont(path, asPrimary: true);
        CreateAtlas(InitialAtlasSize, InitialAtlasSize);
    }

    /// <summary>
    /// 添加一个回退字体。当主字体没有某个码点的字形时，会依次尝试后续字体。
    /// 必须在 LoadFont 之后调用。
    /// </summary>
    public void AddFallback(string path)
    {
        if (!File.Exists(path)) return;
        try { AddFont(path, asPrimary: false); } catch { /* ignore broken fallback */ }
    }

    private void AddFont(string path, bool asPrimary)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Font not found: " + path);

        // SixLabors.Fonts 内部正确处理 TTC（TrueType Collection）的偏移；
        // 对 .ttc/.otc 使用 AddCollection，其余按单一字体处理。
        bool isCollection = path.EndsWith(".ttc", StringComparison.OrdinalIgnoreCase)
                         || path.EndsWith(".otc", StringComparison.OrdinalIgnoreCase);

        if (isCollection)
        {
            var fams = _collection.AddCollection(path).ToList();
            if (fams.Count == 0)
                throw new InvalidOperationException("Empty font collection: " + path);
            if (asPrimary) _primaryFamily = fams[0];
            else
                foreach (var f in fams) _fallbackFamilies.Add(f);
        }
        else
        {
            var fam = _collection.Add(path);
            if (asPrimary) _primaryFamily = fam;
            else _fallbackFamilies.Add(fam);
        }
    }

    private Font GetPrimaryFont(float pxSize)
    {
        if (!_primaryFontCache.TryGetValue(pxSize, out var font))
        {
            font = _primaryFamily.CreateFont(pxSize, FontStyle.Regular);
            _primaryFontCache[pxSize] = font;
        }
        return font;
    }

    public float GetScaleForPixelHeight(float pxSize)
    {
        // 与 CSS/GDI 一致：font-size 表示 em 高度（每 em 多少像素）。
        if (!_primaryFamily.TryGetMetrics(FontStyle.Regular, out var metrics) || metrics is null)
            return 1f;
        return pxSize / metrics.UnitsPerEm;
    }

    public void GetVMetrics(float pxSize, out float ascent, out float descent, out float lineGap)
    {
        if (!_primaryFamily.TryGetMetrics(FontStyle.Regular, out var metrics) || metrics is null)
        {
            ascent = pxSize; descent = 0; lineGap = 0; return;
        }
        float scale = pxSize / metrics.UnitsPerEm;
        ascent = metrics.HorizontalMetrics.Ascender * scale;
        descent = metrics.HorizontalMetrics.Descender * scale;
        lineGap = metrics.HorizontalMetrics.LineGap * scale;
    }

    /// <summary>
    /// 测量整段文本的像素宽高，逻辑与 <see cref="WebGpuPainter"/> 的 DrawText 完全一致：
    /// 用 painter 同款 ascender/descender/lineGap 推导行高；逐行累加 codepoint 的 Glyph.Advance。
    /// 这样布局与绘制一定对齐，不会出现裁剪/错位。
    /// </summary>
    public (float Width, float Height) MeasureText(string? text, float pxSize)
    {
        GetVMetrics(pxSize, out float ascent, out float descent, out float lineGap);
        float lineHeight = ascent - descent + lineGap;
        if (string.IsNullOrEmpty(text))
            return (0f, lineHeight);

        float maxLineW = 0f;
        float curLineW = 0f;
        int lineCount = 1;
        int i = 0;
        while (i < text.Length)
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
                lineCount++;
                continue;
            }
            if (cp == '\r') continue;

            var g = GetGlyph(pxSize, cp);
            curLineW += g.Advance;
        }
        if (curLineW > maxLineW) maxLineW = curLineW;
        return (maxLineW, lineHeight * lineCount);
    }

    public Glyph GetGlyph(float pxSize, int codepoint)
    {
        var key = (pxSize, codepoint);
        if (_cache.TryGetValue(key, out var g))
            return g;

        string text = char.ConvertFromUtf32(codepoint);
        var font = GetPrimaryFont(pxSize);

        // 主字体 + 回退链：SixLabors 会自动为缺失字形使用 FallbackFontFamilies。
        // VerticalAlignment 默认为 Top → 度量空间 y=0 对应行盒顶部，基线位于 ascent 处；
        // 这与我们的 GetVMetrics 输出保持一致（painter 使用 baselineY = AbsoluteY + ascent）。
        var measureOpts = new TextOptions(font)
        {
            FallbackFontFamilies = _fallbackFamilies,
            Dpi = 72f, // 1pt = 1px，使 font.Size 直接表示像素 em 高度
        };

        var advance = TextMeasurer.MeasureAdvance(text, measureOpts).Width;
        var bounds = TextMeasurer.MeasureBounds(text, measureOpts);

        // 一些字符（空格、控制字符等）没有可见 ink → 直接缓存为空字形
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            g = new Glyph { Empty = true, Advance = advance };
            _cache[key] = g;
            return g;
        }

        // 紧凑包围盒像素尺寸（向外取整 1px 防止边缘被裁）
        int boxLeft = (int)Math.Floor(bounds.X);
        int boxTop = (int)Math.Floor(bounds.Y);
        int boxRight = (int)Math.Ceiling(bounds.X + bounds.Width);
        int boxBottom = (int)Math.Ceiling(bounds.Y + bounds.Height);
        int w = boxRight - boxLeft;
        int h = boxBottom - boxTop;
        if (w <= 0 || h <= 0)
        {
            g = new Glyph { Empty = true, Advance = advance };
            _cache[key] = g;
            return g;
        }

        // Pack into shelf
        if (_shelfX + w + Padding > _atlasW)
        {
            _shelfX = 0;
            _shelfY += _shelfH + Padding;
            _shelfH = 0;
        }
        if (_shelfY + h + Padding > _atlasH)
        {
            GrowAtlas(_atlasW * 2, _atlasH * 2);
        }

        int gx = _shelfX;
        int gy = _shelfY;

        // 渲染到 L8（单通道 8-bit luminance），可直接当作 alpha 拷贝到 R8 atlas。
        using (var img = new Image<L8>(w, h))
        {
            var drawOpts = new RichTextOptions(font)
            {
                FallbackFontFamilies = _fallbackFamilies,
                Dpi = 72f,
                // 让 ink 的左上角对齐到 image (0,0)
                Origin = new Vector2(-boxLeft, -boxTop),
            };
            img.Mutate(ctx => ctx.DrawText(drawOpts, text, Color.White));

            img.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < h; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    int dstOffset = (gy + y) * _atlasW + gx;
                    for (int x = 0; x < w; x++)
                        _cpuAtlas[dstOffset + x] = row[x].PackedValue;
                }
            });
        }

        UploadAtlasRegion(gx, gy, w, h);

        _shelfX += w + Padding;
        if (h > _shelfH) _shelfH = h;

        // painter 使用：bitmap.top_left_screen = (penX + XOff, baselineY + YOff)
        // baselineY = AbsoluteY + ascent，与 SixLabors 度量空间一致：行盒顶部在 y=0，基线在 y=ascent。
        // 因此 bitmap 顶部在度量空间 = boxTop；相对基线 = boxTop - ascent。
        float ascent = 0f;
        if (_primaryFamily.TryGetMetrics(FontStyle.Regular, out var fm) && fm is not null)
            ascent = fm.HorizontalMetrics.Ascender * (pxSize / fm.UnitsPerEm);

        g = new Glyph
        {
            U0 = gx / (float)_atlasW,
            V0 = gy / (float)_atlasH,
            U1 = (gx + w) / (float)_atlasW,
            V1 = (gy + h) / (float)_atlasH,
            W = w,
            H = h,
            XOff = boxLeft,
            YOff = (int)Math.Round(boxTop - ascent),
            Advance = advance,
            Empty = false
        };
        _cache[key] = g;
        return g;
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
        var keys = new List<(float, int)>(_cache.Keys);
        foreach (var k in keys)
        {
            var g = _cache[k];
            g.U0 *= sx; g.U1 *= sx;
            g.V0 *= sy; g.V1 *= sy;
            _cache[k] = g;
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
        _primaryFontCache.Clear();
        _cache.Clear();
    }
}
