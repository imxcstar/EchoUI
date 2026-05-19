using System.Runtime.InteropServices;
using WebGPU;
using static WebGPU.WebGPU;

namespace EchoUI.Render.Win32.WebGpu;

/// <summary>
/// 使用 GDI（CLEARTYPE_QUALITY + TrueType hinting + Windows 字体链接）栅格化文本到一张
/// R8 atlas 纹理。每个不同的 (text, fontFamily, fontSize, fontWeight) 在 atlas 中占据一个
/// 矩形 region；painter 直接以一个 quad 渲染整条文本。
///
/// 这是为了和 <see cref="GdiPainter"/> 的文本完全一致 —— 因为字形栅格化就是用同一个 GDI 字体、
/// 同样的 DrawText 调用产生的。SixLabors 没有 TrueType hinting 字节码解释器，在 12–14px 这种
/// 小字号下竖笔不到 1px 宽，AA 后看起来又细又灰；GDI 在 CLEARTYPE_QUALITY 下会执行 hinting
/// 把竖笔锁到整数像素宽度，所以更清晰。
/// </summary>
internal sealed unsafe class GdiTextAtlas : IDisposable
{
    private const int InitialAtlasSize = 1024;
    private const int Padding = 1;

    private readonly WGPUDevice _device;
    private readonly WGPUQueue _queue;

    private int _atlasW;
    private int _atlasH;
    private byte[] _cpuAtlas = Array.Empty<byte>();
    private int _shelfX;
    private int _shelfY;
    private int _shelfH;

    public WGPUTexture Texture;
    public WGPUTextureView TextureView;

    /// <summary>
    /// 一条文本运行（text run）在 atlas 上的位置与尺寸（像素已 GDI 测量，绘制时直接当 quad 用）。
    /// </summary>
    public struct Run
    {
        public float U0, V0, U1, V1;
        public int W, H;
    }

    private readonly Dictionary<RunKey, Run> _cache = new();

    // 与 GdiText.GetFontHandle 的 CLEARTYPE_QUALITY HFONT 完全一致。栅格化时三个子像素通道
     // (R/G/B) 取平均 → 256 级灰度 AA，匹配 Win32 GDI 直接画到屏 DC 时的视觉清晰度。
     // 早期试过 ANTIALIASED_QUALITY（只有 17 级 AA），小字号边缘会出现阶梯感 → 退回 ClearType+avg。

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
        public override int GetHashCode()
            => HashCode.Combine(Text, Family, Size, Weight);
    }

    public GdiTextAtlas(WGPUDevice device, WGPUQueue queue)
    {
        _device = device;
        _queue = queue;
        CreateAtlas(InitialAtlasSize, InitialAtlasSize);
    }

    /// <summary>
    /// 获取一条单行文本的 atlas region；多行调用方应先按 '\n' 拆开。
    /// </summary>
    public Run GetRun(string text, string? fontFamily, float fontSize, string? fontWeight)
    {
        var resolved = GdiText.ResolveFontFamily(fontFamily, text);
        var key = new RunKey(text, resolved, fontSize, fontWeight);
        if (_cache.TryGetValue(key, out var cached)) return cached;

        nint screenDC = NativeInterop.GetDC(0);
        nint memDC = NativeInterop.CreateCompatibleDC(screenDC);
        // 使用 ClearType HFONT（与 GdiPainter 完全一致 —— 同样 hinting / 同样 advance）。
        nint hFont = GdiText.GetFontHandle(resolved, fontSize, fontWeight);
        nint oldFont = hFont != 0 ? NativeInterop.SelectObject(memDC, hFont) : 0;

        nint dibBmp = 0;
        nint oldBmp = 0;
        try
        {
            // 用 GetTextExtentPoint32 测量像素宽，与 GdiPainter.MeasureText 完全一致。
            // 高度用 TEXTMETRIC.tmHeight（行盒高度，不含 external leading），与 GDI 绘制一致。
            if (!NativeInterop.GetTextExtentPoint32(memDC, text, text.Length, out var sz))
                sz = new NativeInterop.SIZE { cx = (int)(text.Length * fontSize * 0.6f), cy = (int)Math.Ceiling(fontSize * 1.2f) };
            if (!NativeInterop.GetTextMetrics(memDC, out var tm))
                tm = default;

            int w = Math.Max(1, sz.cx);
            int h = Math.Max(1, tm.tmHeight > 0 ? tm.tmHeight : sz.cy);

            // 创建 32bpp top-down DIB 作为绘制目标。
            var bmi = new NativeInterop.BITMAPINFO
            {
                bmiHeader = new NativeInterop.BITMAPINFOHEADER
                {
                    biSize = (uint)Marshal.SizeOf<NativeInterop.BITMAPINFOHEADER>(),
                    biWidth = w,
                    biHeight = -h, // 负数：top-down
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = 0, // BI_RGB
                }
            };
            dibBmp = NativeInterop.CreateDIBSection(memDC, ref bmi, 0u, out nint bitsPtr, 0, 0);
            if (dibBmp == 0 || bitsPtr == 0)
                throw new InvalidOperationException("CreateDIBSection failed");
            oldBmp = NativeInterop.SelectObject(memDC, dibBmp);

            // 黑底白字 → 像素的 RGB 即 ClearType 三个子像素的覆盖度。直接取 max 会因相位错位
            // 形成硬边/相位锯齿；ANTIALIASED_QUALITY 又只有 17 级 AA 阶梯明显。这里对 R+G+B
            // 取平均（≈ 整像素的真实覆盖率，256 级灰度），与 GDI 直绘屏幕在视觉上一致。
            var rect = new NativeInterop.RECT { Left = 0, Top = 0, Right = w, Bottom = h };
            nint blackBrush = NativeInterop.CreateSolidBrush(0x000000);
            NativeInterop.FillRect(memDC, ref rect, blackBrush);
            NativeInterop.DeleteObject(blackBrush);

            NativeInterop.SetBkMode(memDC, NativeInterop.TRANSPARENT);
            NativeInterop.SetTextColor(memDC, 0xFFFFFF);
            NativeInterop.DrawText(memDC, text, text.Length, ref rect,
                NativeInterop.DT_LEFT | NativeInterop.DT_TOP |
                NativeInterop.DT_NOPREFIX | NativeInterop.DT_SINGLELINE);

            // 分配 atlas region（shelf packing）
            if (_shelfX + w + Padding > _atlasW)
            {
                _shelfX = 0;
                _shelfY += _shelfH + Padding;
                _shelfH = 0;
            }
            while (_shelfY + h + Padding > _atlasH)
                GrowAtlas(_atlasW * 2, _atlasH * 2);

            int gx = _shelfX;
            int gy = _shelfY;

            byte* src = (byte*)bitsPtr;
            int stride = w * 4; // 32bpp 自然 4 字节对齐
            for (int y = 0; y < h; y++)
            {
                int dstRow = (gy + y) * _atlasW + gx;
                int srcRow = y * stride;
                for (int x = 0; x < w; x++)
                {
                    // ClearType 输出：B=右子像素、G=中、R=左 (BGRA in DIB)。取三通道平均得到
                    // 整像素覆盖率（256 级灰度），避免 max(R,G,B) 的过粗硬边。
                    int off = srcRow + x * 4;
                    int avg = (src[off] + src[off + 1] + src[off + 2] + 1) / 3;
                    if (avg > 255) avg = 255;
                    _cpuAtlas[dstRow + x] = (byte)avg;
                }
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
        finally
        {
            if (oldBmp != 0) NativeInterop.SelectObject(memDC, oldBmp);
            if (dibBmp != 0) NativeInterop.DeleteObject(dibBmp);
            if (oldFont != 0) NativeInterop.SelectObject(memDC, oldFont);
            NativeInterop.DeleteDC(memDC);
            NativeInterop.ReleaseDC(0, screenDC);
        }
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
            byte* srcPtr = p + y * _atlasW + x;
            nuint dataSize = (nuint)((h - 1) * _atlasW + w);
            wgpuQueueWriteTexture(_queue, &dst, srcPtr, dataSize, &layout, &size);
        }
    }

    public void Dispose()
    {
        if (TextureView.IsNotNull) wgpuTextureViewRelease(TextureView);
        if (Texture.IsNotNull) { wgpuTextureDestroy(Texture); wgpuTextureRelease(Texture); }
        // HFONT 由 GdiText 持有的 cache 统一管理，这里不需要释放。
        _cache.Clear();
    }
}
