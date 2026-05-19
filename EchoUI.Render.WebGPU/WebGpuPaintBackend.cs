using EchoUI.Render.Win32;
using EchoUI.Render.WebGPU.Internal;
using WebGPU;
using static WebGPU.WebGPU;

namespace EchoUI.Render.WebGPU;

/// <summary>
/// IWin32PaintBackend 的 WebGPU 实现：在 Win32Window 的 WM_PAINT 中接管绘制。
/// </summary>
internal sealed unsafe class WebGpuPaintBackend : IWin32PaintBackend
{
    private readonly WebGpuContext _context = new();
    private UiPipeline? _pipeline;
    private UiBatchRenderer? _batch;
    private GdiTextAtlas? _textAtlas;
    private TextureCache? _textures;
    private WebGpuPainter? _painter;
    private Win32Renderer? _renderer;
    private nint _hwnd;
    private bool _initialized;

    public WebGpuPaintBackend()
    {
    }

    public void Attach(nint hwnd, Win32Renderer renderer)
    {
        _hwnd = hwnd;
        _renderer = renderer;
    }

    private void EnsureInitialized(int width, int height)
    {
        if (_initialized) return;
        if (width <= 0 || height <= 0) return;

        nint hinstance = NativeInterop.GetModuleHandle(null);
        _context.Initialize(_hwnd, hinstance, (uint)width, (uint)height);

        string wgsl = LoadShader("Ui.wgsl");
        _pipeline = new UiPipeline();
        _pipeline.Initialize(_context.Device, _context.SwapChainFormat, wgsl);

        _textures = new TextureCache(_context.Device, _context.Queue);
        _textAtlas = new GdiTextAtlas(_context.Device, _context.Queue);

        _batch = new UiBatchRenderer(_context.Device, _context.Queue, _pipeline);
        _batch.SetWhiteTexture(_textures.WhiteTextureView);

        _painter = new WebGpuPainter(_batch, _textAtlas, _textures, _pipeline);
        _initialized = true;
    }

    private static string LoadShader(string name)
    {
        string baseDir = AppContext.BaseDirectory;
        string path = Path.Combine(baseDir, "Shaders", name);
        if (File.Exists(path))
            return File.ReadAllText(path);

        // Fallback: walk up from base dir to find Shaders/Ui.wgsl
        var dir = new DirectoryInfo(baseDir);
        while (dir != null)
        {
            var probe = Path.Combine(dir.FullName, "Shaders", name);
            if (File.Exists(probe)) return File.ReadAllText(probe);
            dir = dir.Parent;
        }
        throw new FileNotFoundException("Could not locate shader: " + name);
    }

    public void Resize(int width, int height)
    {
        if (width <= 0 || height <= 0) return;
        if (!_initialized)
        {
            EnsureInitialized(width, height);
            return;
        }
        _context.Resize((uint)width, (uint)height);
    }

    public void Paint(int width, int height)
    {
        if (!_initialized)
        {
            EnsureInitialized(width, height);
            if (!_initialized) return;
        }
        if (_renderer == null) return;
        // 自定义后端负责触发布局（GDI 路径在 Win32Window.OnPaint 中已 EnsureLayout，但 PaintBackend 分支没有）
        _renderer.EnsureLayout(width, height);
        var root = _renderer.RootElement;
        if (root == null) return;

        _context.RenderFrame((encoder, view) =>
        {
            WGPURenderPassColorAttachment colorAttachment = new()
            {
                view = view,
                resolveTarget = WGPUTextureView.Null,
                loadOp = WGPULoadOp.Clear,
                storeOp = WGPUStoreOp.Store,
                depthSlice = WGPU_DEPTH_SLICE_UNDEFINED,
                clearValue = new WGPUColor(1.0, 1.0, 1.0, 1.0)
            };
            WGPURenderPassDescriptor renderPassDesc = new()
            {
                colorAttachmentCount = 1,
                colorAttachments = &colorAttachment,
                depthStencilAttachment = null,
                timestampWrites = null
            };
            var pass = wgpuCommandEncoderBeginRenderPass(encoder, &renderPassDesc);
            try
            {
                _pipeline!.WriteGlobals(_context.Queue, (float)width, (float)height);
                _batch!.BeginFrame(width, height);
                _painter!.Paint(root, _renderer.FloatingElements, width, height);
                _batch.EndFrameAndDraw(pass);
            }
            finally
            {
                wgpuRenderPassEncoderEnd(pass);
                wgpuRenderPassEncoderRelease(pass);
            }
        });
    }

    public void Dispose()
    {
        _batch?.Dispose();
        _textAtlas?.Dispose();
        _textures?.Dispose();
        _pipeline?.Dispose();
        _context.Dispose();
    }
}
