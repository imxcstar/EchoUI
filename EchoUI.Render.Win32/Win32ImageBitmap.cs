using System.Runtime.InteropServices;
using EchoUI.Core;

namespace EchoUI.Render.Win32;

internal sealed class Win32ImageBitmap : IDisposable
{
    private nint _handle;
    private nint _bits;

    public nint Handle => _handle;

    private Win32ImageBitmap(nint handle, nint bits)
    {
        _handle = handle;
        _bits = bits;
    }

    public static Win32ImageBitmap FromResource(ImageResource resource)
    {
        if (resource.Format != ImagePixelFormat.Bgra8888Premultiplied)
            throw new NotSupportedException($"Unsupported image pixel format: {resource.Format}.");

        if (resource.Width <= 0 || resource.Height <= 0 || resource.Pixels.IsEmpty)
            throw new ArgumentException("Image resource is empty.", nameof(resource));

        var destinationStride = checked(resource.Width * 4);
        var bitmapInfo = new NativeInterop.BITMAPINFO
        {
            bmiHeader = new NativeInterop.BITMAPINFOHEADER
            {
                biSize = (uint)Marshal.SizeOf<NativeInterop.BITMAPINFOHEADER>(),
                biWidth = resource.Width,
                biHeight = -resource.Height,
                biPlanes = 1,
                biBitCount = 32,
                biCompression = NativeInterop.BI_RGB,
                biSizeImage = (uint)checked(destinationStride * resource.Height)
            }
        };

        var screenDc = NativeInterop.GetDC(0);
        if (screenDc == 0)
            throw new InvalidOperationException("Cannot acquire screen DC.");

        try
        {
            var bitmap = NativeInterop.CreateDIBSection(screenDc, ref bitmapInfo, NativeInterop.DIB_RGB_COLORS, out var bits, 0, 0);
            if (bitmap == 0 || bits == 0)
                throw new InvalidOperationException("Cannot create image DIB section.");

            CopyPixels(resource, bits, destinationStride);
            return new Win32ImageBitmap(bitmap, bits);
        }
        finally
        {
            NativeInterop.ReleaseDC(0, screenDc);
        }
    }

    private static void CopyPixels(ImageResource resource, nint destinationBits, int destinationStride)
    {
        var source = resource.Pixels.ToArray();
        var rowBytes = Math.Min(destinationStride, resource.Stride);
        for (var y = 0; y < resource.Height; y++)
        {
            var sourceOffset = checked(y * resource.Stride);
            var destinationOffset = checked(y * destinationStride);
            Marshal.Copy(source, sourceOffset, destinationBits + destinationOffset, rowBytes);
        }
    }

    public void Dispose()
    {
        if (_handle != 0)
        {
            NativeInterop.DeleteObject(_handle);
            _handle = 0;
            _bits = 0;
        }
    }
}
