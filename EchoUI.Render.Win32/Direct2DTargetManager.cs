using Vortice.DCommon;
using Vortice.Direct2D1;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace EchoUI.Render.Win32;

internal sealed class Direct2DTargetManager : IDisposable
{
    private readonly ID2D1Factory _factory;
    private ID2D1HwndRenderTarget? _renderTarget;
    private int _renderTargetWidth;
    private int _renderTargetHeight;
    private bool _disposed;

    public Direct2DTargetManager()
    {
        _factory = D2D1.D2D1CreateFactory<ID2D1Factory>(FactoryType.MultiThreaded);
    }

    public ID2D1HwndRenderTarget EnsureRenderTarget(nint hwnd, int width, int height)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_renderTarget != null)
        {
            if (_renderTargetWidth != width || _renderTargetHeight != height)
            {
                _renderTarget.Resize(new SizeI(width, height));
                _renderTargetWidth = width;
                _renderTargetHeight = height;
            }

            return _renderTarget;
        }

        var renderTargetProperties = new RenderTargetProperties(
            RenderTargetType.Hardware,
            new PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Ignore),
            96,
            96,
            RenderTargetUsage.None,
            FeatureLevel.Default);
        var hwndProperties = new HwndRenderTargetProperties
        {
            Hwnd = hwnd,
            PixelSize = new SizeI(width, height),
            PresentOptions = PresentOptions.None
        };
        _renderTarget = _factory.CreateHwndRenderTarget(renderTargetProperties, hwndProperties);
        _renderTargetWidth = width;
        _renderTargetHeight = height;
        return _renderTarget;
    }

    public void Reset()
    {
        _renderTarget?.Dispose();
        _renderTarget = null;
        _renderTargetWidth = 0;
        _renderTargetHeight = 0;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Reset();
        _factory.Dispose();
    }
}
