using System.Diagnostics;
using EchoUI.Core;
using Vortice.Mathematics;

namespace EchoUI.Render.Win32;

internal sealed class Win32Direct2DRenderBackend : IWin32RenderFrameBackend
{
    private readonly Func<nint> _getHwnd;
    private readonly object _gate = new();
    private readonly Direct2DTargetManager _targetManager = new();
    private readonly Direct2DCommandExecutor _executor = new();
    private RenderFrame? _pendingFrame;
    private long _completedVersion;
    private bool _disposed;

    public RenderBackendKind Kind => RenderBackendKind.Gpu;

    public RenderBackendCapabilities Capabilities { get; } = new(
        RequiresFullFrame: true,
        SupportsPartialInvalidation: false,
        PresentsDirectlyToWindow: true,
        IsHardwareAccelerated: true);

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

    public Win32Direct2DRenderBackend(Func<nint> getHwnd)
    {
        _getHwnd = getHwnd;
    }

    public void Submit(RenderFrame frame)
    {
        TrySubmit(frame);
    }

    public bool TrySubmit(RenderFrame frame)
    {
        RenderFrame? dropped;
        lock (_gate)
        {
            if (_disposed)
                return false;

            dropped = _pendingFrame;
            _pendingFrame = frame;
        }

        DisposeFrameResources(dropped);
        var hwnd = _getHwnd();
        if (hwnd != 0)
            NativeInterop.PostMessage(hwnd, NativeInterop.WM_ECHOUI_RENDER_READY, 0, 0);
        return true;
    }

    public bool Present(nint targetHdc, NativeInterop.RECT? clipRect = null)
    {
        RenderFrame? frame;
        lock (_gate)
        {
            if (_disposed || _pendingFrame == null)
                return false;

            frame = _pendingFrame;
            _pendingFrame = null;
        }

        try
        {
            var hwnd = _getHwnd();
            if (hwnd == 0)
            {
                DisposeFrameResources(frame);
                return false;
            }

            var target = _targetManager.EnsureRenderTarget(hwnd, frame.Width, frame.Height);
            target.BeginDraw();
            target.Clear(ToColor4(EchoUI.Core.Color.White));
            _executor.Execute(target, frame.Commands);
            target.EndDraw(out _, out _);

            lock (_gate)
            {
                _completedVersion = frame.Version;
            }

            return true;
        }
        catch (Exception ex)
        {
            Trace.TraceError($"[EchoUI.Win32] Direct2D render failed: {ex}");
            Debug.WriteLine($"[EchoUI.Win32] Direct2D render failed: {ex}");
            ResetRenderTarget();
            var hwnd = _getHwnd();
            if (hwnd != 0)
                NativeInterop.PostMessage(hwnd, NativeInterop.WM_ECHOUI_RENDER_FAILED, 0, 0);
            return false;
        }
        finally
        {
            DisposeFrameResources(frame);
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
        }

        DisposeFrameResources(pending);
        ResetRenderTarget();
        _executor.Dispose();
        _targetManager.Dispose();
    }

    private void ResetRenderTarget()
    {
        _executor.ResetRenderTargetResources();
        _targetManager.Reset();
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

    private static Vortice.Mathematics.Color4 ToColor4(EchoUI.Core.Color color)
    {
        return new Vortice.Mathematics.Color4(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);
    }
}
