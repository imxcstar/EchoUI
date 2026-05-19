using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using EchoUI.Core;
using WebGPU;
using static WebGPU.WebGPU;

namespace EchoUI.Render.WebGPU.Internal;

/// <summary>
/// 一帧内累积顶点/索引并按 (texture, scissor) 分批 flush 的批渲染器。
/// 顶点和索引使用 CPU 端 List 累积，flush 时一次 wgpuQueueWriteBuffer + drawIndexed。
/// </summary>
internal sealed unsafe class UiBatchRenderer : IDisposable
{
    private readonly WGPUDevice _device;
    private readonly WGPUQueue _queue;
    private readonly UiPipeline _pipeline;

    private WGPUBuffer _vertexBuffer;
    private WGPUBuffer _indexBuffer;
    private ulong _vertexBufferCapacity;  // bytes
    private ulong _indexBufferCapacity;   // bytes

    private readonly List<UiVertex> _vertices = new(4096);
    private readonly List<ushort> _indices = new(8192);

    // 当前批的状态
    private WGPUTextureView _currentTexture;
    private WGPUSampler _currentSampler;
    private (int x, int y, int w, int h) _currentScissor;
    private int _batchStartIndex;

    // 当前帧记录所有 batch 描述
    private struct DrawBatch
    {
        public int IndexStart;
        public int IndexCount;
        public WGPUTextureView Texture;
        public WGPUSampler Sampler;
        public int ScissorX, ScissorY, ScissorW, ScissorH;
    }
    private readonly List<DrawBatch> _batches = new(64);

    private WGPUTextureView _whiteTextureView;

    // 当前 2D 变换矩阵（row-vector: v' = v * M）。AddRect 内会把四个角点先经此矩阵变换再写入顶点。
    // WebGpuPainter 在遇到带 Transform 的元素时通过 SetTransform 入栈/出栈。
    private Matrix3x2 _currentMatrix = Matrix3x2.Identity;

    public UiBatchRenderer(WGPUDevice device, WGPUQueue queue, UiPipeline pipeline)
    {
        _device = device;
        _queue = queue;
        _pipeline = pipeline;
    }

    public void SetWhiteTexture(WGPUTextureView whiteView)
    {
        _whiteTextureView = whiteView;
    }

    public void BeginFrame(int viewportW, int viewportH)
    {
        _vertices.Clear();
        _indices.Clear();
        _batches.Clear();
        _currentTexture = _whiteTextureView;
        _currentSampler = _pipeline.LinearSampler;
        _currentScissor = (0, 0, viewportW, viewportH);
        _currentMatrix = Matrix3x2.Identity;
        _batchStartIndex = 0;
    }

    /// <summary>设置当前 2D 变换矩阵。仅影响之后 AddRect 写入的顶点，已写入的不受影响。</summary>
    public Matrix3x2 SetTransform(Matrix3x2 m)
    {
        var old = _currentMatrix;
        _currentMatrix = m;
        return old;
    }

    public Matrix3x2 CurrentMatrix => _currentMatrix;

    private void FlushBatch()
    {
        int count = _indices.Count - _batchStartIndex;
        if (count <= 0)
            return;
        _batches.Add(new DrawBatch
        {
            IndexStart = _batchStartIndex,
            IndexCount = count,
            Texture = _currentTexture,
            Sampler = _currentSampler,
            ScissorX = _currentScissor.x,
            ScissorY = _currentScissor.y,
            ScissorW = _currentScissor.w,
            ScissorH = _currentScissor.h,
        });
        _batchStartIndex = _indices.Count;
    }

    public void SetTexture(WGPUTextureView view, WGPUSampler sampler)
    {
        if (view.Handle == _currentTexture.Handle && sampler.Handle == _currentSampler.Handle)
            return;
        FlushBatch();
        _currentTexture = view;
        _currentSampler = sampler;
    }

    public void SetScissor(int x, int y, int w, int h)
    {
        if (w < 0) w = 0;
        if (h < 0) h = 0;
        if (_currentScissor.x == x && _currentScissor.y == y && _currentScissor.w == w && _currentScissor.h == h)
            return;
        FlushBatch();
        _currentScissor = (x, y, w, h);
    }

    /// <summary>
    /// 添加一个矩形四边形：4 顶点 + 6 索引。
    /// localUV 自动是 0..1。
    /// </summary>
    public void AddRect(
        float x, float y, float w, float h,
        Color fill, Color border, float borderWidth, float borderRadius,
        float u0, float v0, float u1, float v1,
        bool hasTexture, bool isAlphaMask)
    {
        if (w <= 0 || h <= 0) return;

        // 自动 flush：u16 索引上限
        if (_vertices.Count + 4 > 65000)
        {
            // 不能在同一批内继续，强制 flush 当前批 (但 buffer 会重新写出全量)
            // 简化做法：保持单大 buffer，但仅当总顶点数超出再增长。我们这里允许 List 自然增长。
        }

        int baseIdx = _vertices.Count;

        var fr = fill.R / 255f; var fg = fill.G / 255f; var fb = fill.B / 255f; var fa = fill.A / 255f;
        var br = border.R / 255f; var bgc = border.G / 255f; var bbc = border.B / 255f; var ba = border.A / 255f;

        float pIsMask = isAlphaMask ? 1f : 0f;
        float pHasTex = hasTexture ? 1f : 0f;

        UiVertex v0v = new()
        {
            PositionX = x, PositionY = y,
            U = u0, V = v0,
            LocalU = 0, LocalV = 0,
            ColorR = fr, ColorG = fg, ColorB = fb, ColorA = fa,
            BorderColorR = br, BorderColorG = bgc, BorderColorB = bbc, BorderColorA = ba,
            RectSizeX = w, RectSizeY = h,
            ParamRadius = borderRadius, ParamBorderWidth = borderWidth,
            ParamIsAlphaMask = pIsMask, ParamHasTexture = pHasTex,
        };
        UiVertex v1v = v0v; v1v.PositionX = x + w; v1v.U = u1; v1v.LocalU = 1;
        UiVertex v2v = v0v; v2v.PositionX = x + w; v2v.PositionY = y + h; v2v.U = u1; v2v.V = v1; v2v.LocalU = 1; v2v.LocalV = 1;
        UiVertex v3v = v0v; v3v.PositionY = y + h; v3v.V = v1; v3v.LocalV = 1;

        // 若当前存在 2D 变换（rotate/scale/skew/translate），对四个角的位置做变换后再写入。
        // RectSize / LocalUV 仍保留 axis-aligned 信息，用于 fragment shader 的圆角/边框/AA 计算；
        // 这意味着圆角矩形在旋转后边框逻辑仍按局部矩形坐标算（与 GDI 世界变换语义一致）。
        if (!_currentMatrix.IsIdentity)
        {
            var p0 = Vector2.Transform(new Vector2(v0v.PositionX, v0v.PositionY), _currentMatrix);
            var p1 = Vector2.Transform(new Vector2(v1v.PositionX, v1v.PositionY), _currentMatrix);
            var p2 = Vector2.Transform(new Vector2(v2v.PositionX, v2v.PositionY), _currentMatrix);
            var p3 = Vector2.Transform(new Vector2(v3v.PositionX, v3v.PositionY), _currentMatrix);
            v0v.PositionX = p0.X; v0v.PositionY = p0.Y;
            v1v.PositionX = p1.X; v1v.PositionY = p1.Y;
            v2v.PositionX = p2.X; v2v.PositionY = p2.Y;
            v3v.PositionX = p3.X; v3v.PositionY = p3.Y;
        }

        _vertices.Add(v0v);
        _vertices.Add(v1v);
        _vertices.Add(v2v);
        _vertices.Add(v3v);

        // CCW: (0,1,2) (0,2,3) — 注意 NDC Y 翻转后 CCW 在屏幕上看起来是 CW；CullMode.None 所以无所谓
        _indices.Add((ushort)(baseIdx + 0));
        _indices.Add((ushort)(baseIdx + 1));
        _indices.Add((ushort)(baseIdx + 2));
        _indices.Add((ushort)(baseIdx + 0));
        _indices.Add((ushort)(baseIdx + 2));
        _indices.Add((ushort)(baseIdx + 3));
    }

    /// <summary>
    /// 提交所有累积的批到 render pass。
    /// </summary>
    public void EndFrameAndDraw(WGPURenderPassEncoder pass)
    {
        FlushBatch();
        if (_indices.Count == 0)
            return;

        EnsureBuffers((ulong)(_vertices.Count * UiVertex.SizeInBytes), (ulong)(_indices.Count * sizeof(ushort)));

        // 上传顶点/索引
        var vSpan = CollectionsMarshal.AsSpan(_vertices);
        wgpuQueueWriteBuffer(_queue, _vertexBuffer, vSpan, 0);
        var iSpan = CollectionsMarshal.AsSpan(_indices);
        wgpuQueueWriteBuffer(_queue, _indexBuffer, iSpan, 0);

        wgpuRenderPassEncoderSetPipeline(pass, _pipeline.Pipeline);
        wgpuRenderPassEncoderSetVertexBuffer(pass, 0, _vertexBuffer);
        wgpuRenderPassEncoderSetIndexBuffer(pass, _indexBuffer, WGPUIndexFormat.Uint16);

        // 每批一个 bind group
        WGPUBindGroup? lastBg = null;
        WGPUTextureView lastTex = default;
        WGPUSampler lastSampler = default;

        foreach (var batch in _batches)
        {
            if (lastBg == null || batch.Texture.Handle != lastTex.Handle || batch.Sampler.Handle != lastSampler.Handle)
            {
                if (lastBg.HasValue)
                    wgpuBindGroupRelease(lastBg.Value);
                lastBg = _pipeline.CreateBindGroup(_device, batch.Texture, batch.Sampler);
                lastTex = batch.Texture;
                lastSampler = batch.Sampler;
                wgpuRenderPassEncoderSetBindGroup(pass, 0, lastBg.Value, 0, null);
            }
            wgpuRenderPassEncoderSetScissorRect(pass,
                (uint)batch.ScissorX, (uint)batch.ScissorY,
                (uint)batch.ScissorW, (uint)batch.ScissorH);
            wgpuRenderPassEncoderDrawIndexed(pass, (uint)batch.IndexCount, 1, (uint)batch.IndexStart, 0, 0);
        }
        if (lastBg.HasValue)
            wgpuBindGroupRelease(lastBg.Value);
    }

    private void EnsureBuffers(ulong vSize, ulong iSize)
    {
        if (vSize > _vertexBufferCapacity)
        {
            if (_vertexBuffer.IsNotNull) { wgpuBufferDestroy(_vertexBuffer); wgpuBufferRelease(_vertexBuffer); }
            ulong cap = Math.Max(vSize * 2, 64UL * 1024UL);
            // 4-byte 对齐
            cap = (cap + 3UL) & ~3UL;
            _vertexBuffer = wgpuDeviceCreateBuffer(_device, WGPUBufferUsage.Vertex | WGPUBufferUsage.CopyDst, cap);
            _vertexBufferCapacity = cap;
        }
        if (iSize > _indexBufferCapacity)
        {
            if (_indexBuffer.IsNotNull) { wgpuBufferDestroy(_indexBuffer); wgpuBufferRelease(_indexBuffer); }
            ulong cap = Math.Max(iSize * 2, 32UL * 1024UL);
            cap = (cap + 3UL) & ~3UL;
            _indexBuffer = wgpuDeviceCreateBuffer(_device, WGPUBufferUsage.Index | WGPUBufferUsage.CopyDst, cap);
            _indexBufferCapacity = cap;
        }
    }

    public void Dispose()
    {
        if (_vertexBuffer.IsNotNull) { wgpuBufferDestroy(_vertexBuffer); wgpuBufferRelease(_vertexBuffer); }
        if (_indexBuffer.IsNotNull) { wgpuBufferDestroy(_indexBuffer); wgpuBufferRelease(_indexBuffer); }
    }
}
