using EchoUI.Core;

namespace EchoUI.Render.Win32;

internal sealed class Win32RenderPipeline : IDisposable
{
    private readonly IWin32RenderFrameBackend _backend;
    private readonly Win32NativeOverlayManager _overlayManager;
    private readonly Func<ComponentInstance?> _getRootInstance;
    private readonly Func<Win32Element?> _getRootElement;
    private readonly Func<IReadOnlyList<Win32Element>> _getFloatingElements;
    private long _frameVersion;
    private bool _forceFullFrameRender = true;

    public Win32RenderPipeline(
        IWin32RenderFrameBackend backend,
        Win32NativeOverlayManager overlayManager,
        Func<ComponentInstance?> getRootInstance,
        Func<Win32Element?> getRootElement,
        Func<IReadOnlyList<Win32Element>> getFloatingElements)
    {
        _backend = backend;
        _overlayManager = overlayManager;
        _getRootInstance = getRootInstance;
        _getRootElement = getRootElement;
        _getFloatingElements = getFloatingElements;
    }

    public RenderBackendCapabilities Capabilities => _backend.Capabilities;

    public void ForceFullFrame()
    {
        _forceFullFrameRender = true;
    }

    public void RequestNativeOverlaySync()
    {
        _overlayManager.RequestPositionSyncAfterNextAcceptedFrame();
    }

    public void SubmitFrame(int width, int height, IReadOnlyList<LayoutBox> dirtyRects, int tileSize)
    {
        var rootInstance = _getRootInstance();
        var rootElement = _getRootElement();
        if (rootInstance == null || rootElement == null || width <= 0 || height <= 0)
            return;

        var viewport = new LayoutBox(0, 0, width, height);
        var needsFullFrame = _forceFullFrameRender || _backend.Capabilities.RequiresFullFrame;
        var frameDirtyRects = needsFullFrame ? [viewport] : dirtyRects;
        var version = Interlocked.Increment(ref _frameVersion);
        var frame = Win32FrameRecorder.Record(rootInstance, rootElement, _getFloatingElements(), width, height, frameDirtyRects, version, tileSize);
        if (needsFullFrame)
            _forceFullFrameRender = false;

        if (_backend.TrySubmit(frame))
            _overlayManager.MarkAcceptedFrame(version);
    }

    public bool Present(nint hdc, NativeInterop.RECT? clipRect, float viewportWidth, float viewportHeight)
    {
        var presented = _backend.Present(hdc, clipRect);
        if (presented)
            _overlayManager.TrySyncAfterPresent(_backend.CompletedVersion, _getRootElement(), viewportWidth, viewportHeight, _getFloatingElements());
        return presented;
    }

    public void Dispose()
    {
        _backend.Dispose();
    }
}
