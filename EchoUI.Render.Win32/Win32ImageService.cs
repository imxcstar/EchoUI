using System.Runtime.InteropServices;
using EchoUI.Core;

namespace EchoUI.Render.Win32;

internal sealed class Win32ImageService
{
    public void Load(Win32Element element, string source)
    {
        try
        {
            var path = ResolvePath(source);
            if (path == null)
                return;

            if (!WicImageLoader.TryLoadBitmap(path, out var bitmap, out var width, out var height))
                return;

            Clear(element);
            try
            {
                element.NativeImageWidth = width;
                element.NativeImageHeight = height;
                element.ImageResource = CreateImageResource(bitmap, width, height);
            }
            finally
            {
                NativeInterop.DeleteObject(bitmap);
            }
        }
        catch
        {
            // 图片加载失败不影响 UI 主流程。
        }
    }

    public void Clear(Win32Element element)
    {
        if (element.NativeImageHandle != 0)
        {
            NativeInterop.DeleteObject(element.NativeImageHandle);
            element.NativeImageHandle = 0;
        }

        element.NativeImageWidth = 0;
        element.NativeImageHeight = 0;
        element.ImageResource = null;
    }

    private static ImageResource? CreateImageResource(nint bitmapHandle, int width, int height)
    {
        if (bitmapHandle == 0 || width <= 0 || height <= 0)
            return null;

        if (!NativeInterop.GetObject(bitmapHandle, Marshal.SizeOf<NativeInterop.BITMAP>(), out var bitmap) || bitmap.bmBits == 0 || bitmap.bmWidthBytes <= 0)
            return null;

        var stride = bitmap.bmWidthBytes;
        var byteCount = checked(stride * height);
        var pixels = new byte[byteCount];
        Marshal.Copy(bitmap.bmBits, pixels, 0, byteCount);
        return new ImageResource(pixels, width, height, stride, ImagePixelFormat.Bgra8888Premultiplied);
    }

    private static string? ResolvePath(string source)
    {
        if (Path.IsPathRooted(source) && File.Exists(source))
            return source;

        var currentDir = AppContext.BaseDirectory;
        var candidate = Path.Combine(currentDir, source.TrimStart('/', '\\'));
        return File.Exists(candidate) ? candidate : null;
    }
}
