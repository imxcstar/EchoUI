using EchoUI.Core;

namespace EchoUI.Render.Win32;

public enum Win32RenderBackendKind
{
    Cpu,
    Skia,
    Direct2D
}

internal interface IWin32RenderFrameBackend : IRenderFrameBackend
{
    long CompletedVersion { get; }

    bool TrySubmit(RenderFrame frame);

    bool Present(nint targetHdc, NativeInterop.RECT? clipRect = null);
}
