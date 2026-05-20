using System.Diagnostics;
using System.Runtime.InteropServices;
using EchoUI.Core;

namespace EchoUI.Render.Win32;

internal sealed class Win32CpuRenderBackend : IRenderFrameBackend
{
    private readonly Func<nint> _getHwnd;
    private readonly object _gate = new();
    private readonly Thread _thread;
    private RenderFrame? _pendingFrame;
    private CpuRenderBuffer? _frontBuffer;
    private CpuRenderBuffer? _spareBuffer;
    private LayoutBox _completedDirtyBounds;
    private long _completedVersion;
    private bool _disposed;

    public RenderBackendKind Kind => RenderBackendKind.Cpu;

    public long CompletedVersion
    {
        get
        {
            lock (_gate)
            {
                return _completedVersion;
            }
        }
    }

    public Win32CpuRenderBackend(Func<nint> getHwnd)
    {
        _getHwnd = getHwnd;
        _thread = new Thread(RenderLoop)
        {
            IsBackground = true,
            Name = "EchoUI Win32 CPU Render"
        };
        _thread.Start();
    }

    public void Submit(RenderFrame frame)
    {
        RenderFrame? dropped = null;
        lock (_gate)
        {
            if (_disposed)
                return;

            if (_pendingFrame != null && IsFullFrame(_pendingFrame) && !IsFullFrame(frame))
            {
                dropped = frame;
            }
            else
            {
                dropped = _pendingFrame;
                _pendingFrame = frame;
                Monitor.Pulse(_gate);
            }
        }

        DisposeFrameResources(dropped);
    }

    public bool Present(nint targetHdc, NativeInterop.RECT? clipRect = null)
    {
        lock (_gate)
        {
            if (_frontBuffer == null || targetHdc == 0)
                return false;

            var clip = clipRect ?? ToNativeRect(_completedDirtyBounds.Width > 0 && _completedDirtyBounds.Height > 0
                ? _completedDirtyBounds
                : new LayoutBox(0, 0, _frontBuffer.Width, _frontBuffer.Height));
            var left = Math.Clamp(clip.Left, 0, _frontBuffer.Width);
            var top = Math.Clamp(clip.Top, 0, _frontBuffer.Height);
            var right = Math.Clamp(clip.Right, 0, _frontBuffer.Width);
            var bottom = Math.Clamp(clip.Bottom, 0, _frontBuffer.Height);
            var width = Math.Max(0, right - left);
            var height = Math.Max(0, bottom - top);
            if (width <= 0 || height <= 0)
                return false;

            return NativeInterop.BitBlt(targetHdc, left, top, width, height, _frontBuffer.Hdc, left, top, NativeInterop.SRCCOPY);
        }
    }

    public void Dispose()
    {
        RenderFrame? pending;
        lock (_gate)
        {
            if (_disposed)
                return;

            _disposed = true;
            pending = _pendingFrame;
            _pendingFrame = null;
            Monitor.PulseAll(_gate);
        }

        DisposeFrameResources(pending);
        if (Thread.CurrentThread != _thread)
            _thread.Join();

        lock (_gate)
        {
            _frontBuffer?.Dispose();
            _spareBuffer?.Dispose();
            _frontBuffer = null;
            _spareBuffer = null;
        }
    }

    private void RenderLoop()
    {
        while (true)
        {
            RenderFrame? frame;
            lock (_gate)
            {
                while (!_disposed && _pendingFrame == null)
                    Monitor.Wait(_gate);

                if (_disposed)
                    return;

                frame = _pendingFrame;
                _pendingFrame = null;
            }

            if (frame == null)
                continue;

            CpuRenderBuffer? buffer = null;
            try
            {
                buffer = RentRenderBuffer(frame.Width, frame.Height);
                CopyFrontBuffer(buffer);
                GdiPainter.PaintFrame(buffer.Hdc, frame, buffer.Surface);

                lock (_gate)
                {
                    if (_disposed)
                    {
                        buffer.Dispose();
                        buffer = null;
                        return;
                    }

                    var oldFront = _frontBuffer;
                    _frontBuffer = buffer;
                    buffer = null;
                    _spareBuffer = oldFront;
                    _completedDirtyBounds = frame.DirtyRects.Aggregate(LayoutBox.Zero, TileGrid.Union);
                    _completedVersion = frame.Version;
                }

                var hwnd = _getHwnd();
                if (hwnd != 0)
                    NativeInterop.PostMessage(hwnd, NativeInterop.WM_ECHOUI_RENDER_READY, 0, 0);
            }
            catch (Exception ex)
            {
                buffer?.Dispose();
                Trace.TraceError($"[EchoUI.Win32] CPU render failed: {ex}");
                Debug.WriteLine($"[EchoUI.Win32] CPU render failed: {ex}");

                var hwnd = _getHwnd();
                if (hwnd != 0)
                    NativeInterop.PostMessage(hwnd, NativeInterop.WM_ECHOUI_RENDER_FAILED, 0, 0);
            }
            finally
            {
                DisposeFrameResources(frame);
            }
        }
    }

    private CpuRenderBuffer RentRenderBuffer(int width, int height)
    {
        lock (_gate)
        {
            if (_spareBuffer != null && _spareBuffer.Width == width && _spareBuffer.Height == height)
            {
                var buffer = _spareBuffer;
                _spareBuffer = null;
                return buffer;
            }

            _spareBuffer?.Dispose();
            _spareBuffer = null;
        }

        return CpuRenderBuffer.Create(width, height);
    }

    private void CopyFrontBuffer(CpuRenderBuffer target)
    {
        lock (_gate)
        {
            if (_frontBuffer == null || _frontBuffer.Width != target.Width || _frontBuffer.Height != target.Height)
                return;

            NativeInterop.BitBlt(target.Hdc, 0, 0, target.Width, target.Height, _frontBuffer.Hdc, 0, 0, NativeInterop.SRCCOPY);
        }
    }

    private static void DisposeFrameResources(RenderFrame? frame)
    {
        if (frame == null)
            return;

        foreach (var command in frame.Commands)
        {
            if (command is IDisposable disposable)
                disposable.Dispose();
        }
    }

    private static bool IsFullFrame(RenderFrame frame)
    {
        if (frame.DirtyRects.Count != 1)
            return false;

        var dirty = frame.DirtyRects[0];
        return dirty.X <= 0
            && dirty.Y <= 0
            && dirty.Width >= frame.Width
            && dirty.Height >= frame.Height;
    }

    private static NativeInterop.RECT ToNativeRect(LayoutBox rect)
    {
        return new NativeInterop.RECT
        {
            Left = (int)Math.Floor(rect.X),
            Top = (int)Math.Floor(rect.Y),
            Right = (int)Math.Ceiling(rect.X + rect.Width),
            Bottom = (int)Math.Ceiling(rect.Y + rect.Height)
        };
    }

    private sealed class CpuRenderBuffer : IDisposable
    {
        private nint _bitmap;
        private nint _oldBitmap;

        public nint Hdc { get; private set; }
        public nint Bits { get; private set; }
        public int Width { get; }
        public int Height { get; }
        public int Stride { get; }
        public CpuBitmapSurface Surface => new(Bits, Width, Height, Stride);

        private CpuRenderBuffer(nint hdc, nint bitmap, nint oldBitmap, nint bits, int width, int height, int stride)
        {
            Hdc = hdc;
            _bitmap = bitmap;
            _oldBitmap = oldBitmap;
            Bits = bits;
            Width = width;
            Height = height;
            Stride = stride;
        }

        public static CpuRenderBuffer Create(int width, int height)
        {
            var screenDc = NativeInterop.GetDC(0);
            if (screenDc == 0)
                throw new InvalidOperationException("Cannot acquire screen DC.");

            try
            {
                var memoryDc = NativeInterop.CreateCompatibleDC(screenDc);
                if (memoryDc == 0)
                    throw new InvalidOperationException("Cannot create memory DC.");

                var stride = checked(width * sizeof(uint));
                var bitmapInfo = new NativeInterop.BITMAPINFO
                {
                    bmiHeader = new NativeInterop.BITMAPINFOHEADER
                    {
                        biSize = (uint)Marshal.SizeOf<NativeInterop.BITMAPINFOHEADER>(),
                        biWidth = width,
                        biHeight = -height,
                        biPlanes = 1,
                        biBitCount = 32,
                        biCompression = NativeInterop.BI_RGB,
                        biSizeImage = (uint)checked(stride * height)
                    }
                };

                var bitmap = NativeInterop.CreateDIBSection(screenDc, ref bitmapInfo, NativeInterop.DIB_RGB_COLORS, out var bits, 0, 0);
                if (bitmap == 0 || bits == 0)
                {
                    if (bitmap != 0)
                        NativeInterop.DeleteObject(bitmap);
                    NativeInterop.DeleteDC(memoryDc);
                    throw new InvalidOperationException("Cannot create DIB section.");
                }

                var oldBitmap = NativeInterop.SelectObject(memoryDc, bitmap);
                return new CpuRenderBuffer(memoryDc, bitmap, oldBitmap, bits, width, height, stride);
            }
            finally
            {
                NativeInterop.ReleaseDC(0, screenDc);
            }
        }

        public void Dispose()
        {
            if (Hdc != 0)
            {
                if (_oldBitmap != 0)
                    NativeInterop.SelectObject(Hdc, _oldBitmap);

                if (_bitmap != 0)
                    NativeInterop.DeleteObject(_bitmap);

                NativeInterop.DeleteDC(Hdc);
            }

            Hdc = 0;
            _bitmap = 0;
            _oldBitmap = 0;
            Bits = 0;
        }
    }
}
