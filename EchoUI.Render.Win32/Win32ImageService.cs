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
            element.NativeImageHandle = bitmap;
            element.NativeImageWidth = width;
            element.NativeImageHeight = height;
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
