using EchoUI.Core;

namespace EchoUI.Render.Win32;

internal sealed record Win32DrawImage : RenderCommand, IDisposable
{
    public Win32DrawImage(LayoutBox layout, nint bitmap, int sourceWidth, int sourceHeight) : base(layout)
    {
        Bitmap = bitmap;
        SourceWidth = sourceWidth;
        SourceHeight = sourceHeight;
    }

    public nint Bitmap { get; private set; }

    public int SourceWidth { get; }

    public int SourceHeight { get; }

    public void Dispose()
    {
        if (Bitmap != 0)
        {
            NativeInterop.DeleteObject(Bitmap);
            Bitmap = 0;
        }
    }
}
