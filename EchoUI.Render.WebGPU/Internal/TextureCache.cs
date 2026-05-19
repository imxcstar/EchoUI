using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using WebGPU;
using static WebGPU.WebGPU;

namespace EchoUI.Render.WebGPU.Internal;

/// <summary>
/// 图片纹理缓存：从文件路径加载 RGBA8 并上传为 sampled texture，缓存到内存中。
/// </summary>
public sealed unsafe class TextureCache : IDisposable
{
    private readonly WGPUDevice _device;
    private readonly WGPUQueue _queue;

    public struct Entry
    {
        public WGPUTexture Texture;
        public WGPUTextureView View;
        public int Width;
        public int Height;
    }

    private readonly Dictionary<string, Entry> _cache = new(StringComparer.OrdinalIgnoreCase);

    public WGPUTexture WhiteTexture;
    public WGPUTextureView WhiteTextureView;

    public TextureCache(WGPUDevice device, WGPUQueue queue)
    {
        _device = device;
        _queue = queue;
        CreateWhiteTexture();
    }

    private void CreateWhiteTexture()
    {
        WGPUTextureDescriptor desc = new()
        {
            usage = WGPUTextureUsage.TextureBinding | WGPUTextureUsage.CopyDst,
            dimension = WGPUTextureDimension._2D,
            size = new WGPUExtent3D { width = 1, height = 1, depthOrArrayLayers = 1 },
            format = WGPUTextureFormat.RGBA8Unorm,
            mipLevelCount = 1,
            sampleCount = 1,
        };
        WhiteTexture = wgpuDeviceCreateTexture(_device, &desc);
        WhiteTextureView = wgpuTextureCreateView(WhiteTexture, null);

        ReadOnlySpan<byte> pixel = stackalloc byte[] { 255, 255, 255, 255 };
        WGPUTexelCopyTextureInfo dst = new()
        {
            texture = WhiteTexture,
            mipLevel = 0,
            origin = default,
            aspect = WGPUTextureAspect.All
        };
        WGPUTexelCopyBufferLayout layout = new()
        {
            offset = 0,
            bytesPerRow = 4,
            rowsPerImage = 1,
        };
        WGPUExtent3D size = new() { width = 1, height = 1, depthOrArrayLayers = 1 };
        fixed (byte* p = pixel)
        {
            wgpuQueueWriteTexture(_queue, &dst, p, 4, &layout, &size);
        }
    }

    public bool TryGet(string path, out Entry entry)
    {
        return _cache.TryGetValue(path, out entry);
    }

    public Entry LoadFromFile(string path)
    {
        if (_cache.TryGetValue(path, out var existing))
            return existing;

        if (!File.Exists(path))
        {
            // Fallback: cache white texture under this path so we don't retry every frame
            var fallback = new Entry { Texture = WhiteTexture, View = WhiteTextureView, Width = 1, Height = 1 };
            _cache[path] = fallback;
            return fallback;
        }

        using var img = Image.Load<Rgba32>(path);
        int w = img.Width;
        int h = img.Height;
        byte[] pixelData = new byte[w * h * 4];
        img.CopyPixelDataTo(pixelData);

        WGPUTextureDescriptor desc = new()
        {
            usage = WGPUTextureUsage.TextureBinding | WGPUTextureUsage.CopyDst,
            dimension = WGPUTextureDimension._2D,
            size = new WGPUExtent3D { width = (uint)w, height = (uint)h, depthOrArrayLayers = 1 },
            format = WGPUTextureFormat.RGBA8Unorm,
            mipLevelCount = 1,
            sampleCount = 1,
        };
        WGPUTexture tex = wgpuDeviceCreateTexture(_device, &desc);
        WGPUTextureView view = wgpuTextureCreateView(tex, null);

        WGPUTexelCopyTextureInfo dst = new()
        {
            texture = tex,
            mipLevel = 0,
            origin = default,
            aspect = WGPUTextureAspect.All
        };
        WGPUTexelCopyBufferLayout layout = new()
        {
            offset = 0,
            bytesPerRow = (uint)(w * 4),
            rowsPerImage = (uint)h,
        };
        WGPUExtent3D size = new() { width = (uint)w, height = (uint)h, depthOrArrayLayers = 1 };

        fixed (byte* p = pixelData)
        {
            wgpuQueueWriteTexture(_queue, &dst, p, (nuint)pixelData.Length, &layout, &size);
        }

        var entry = new Entry { Texture = tex, View = view, Width = w, Height = h };
        _cache[path] = entry;
        return entry;
    }

    public void Dispose()
    {
        foreach (var e in _cache.Values)
        {
            if (e.Texture.Handle == WhiteTexture.Handle) continue;
            if (e.View.IsNotNull) wgpuTextureViewRelease(e.View);
            if (e.Texture.IsNotNull) { wgpuTextureDestroy(e.Texture); wgpuTextureRelease(e.Texture); }
        }
        _cache.Clear();
        if (WhiteTextureView.IsNotNull) wgpuTextureViewRelease(WhiteTextureView);
        if (WhiteTexture.IsNotNull) { wgpuTextureDestroy(WhiteTexture); wgpuTextureRelease(WhiteTexture); }
    }
}
