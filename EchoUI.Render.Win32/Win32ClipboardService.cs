using System.Runtime.InteropServices;
using EchoUI.Core;

namespace EchoUI.Render.Win32;

internal sealed class Win32ClipboardService : IClipboardService
{
    public Task<string> ReadTextAsync()
    {
        return Task.FromResult(ReadText());
    }

    public Task WriteTextAsync(string text)
    {
        WriteText(text ?? string.Empty);
        return Task.CompletedTask;
    }

    private static string ReadText()
    {
        if (!NativeInterop.OpenClipboard(0))
            return string.Empty;

        try
        {
            if (!NativeInterop.IsClipboardFormatAvailable(NativeInterop.CF_UNICODETEXT))
                return string.Empty;

            var handle = NativeInterop.GetClipboardData(NativeInterop.CF_UNICODETEXT);
            if (handle == 0)
                return string.Empty;

            var pointer = NativeInterop.GlobalLock(handle);
            if (pointer == 0)
                return string.Empty;

            try
            {
                return Marshal.PtrToStringUni(pointer) ?? string.Empty;
            }
            finally
            {
                NativeInterop.GlobalUnlock(handle);
            }
        }
        finally
        {
            NativeInterop.CloseClipboard();
        }
    }

    private static void WriteText(string text)
    {
        if (!NativeInterop.OpenClipboard(0))
            return;

        nint handle = 0;
        try
        {
            NativeInterop.EmptyClipboard();

            var normalizedText = text ?? string.Empty;
            var bytes = System.Text.Encoding.Unicode.GetBytes(normalizedText + '\0');
            handle = NativeInterop.GlobalAlloc(NativeInterop.GMEM_MOVEABLE, (nuint)bytes.Length);
            if (handle == 0)
                return;

            var pointer = NativeInterop.GlobalLock(handle);
            if (pointer == 0)
                return;

            try
            {
                Marshal.Copy(bytes, 0, pointer, bytes.Length);
            }
            finally
            {
                NativeInterop.GlobalUnlock(handle);
            }

            if (NativeInterop.SetClipboardData(NativeInterop.CF_UNICODETEXT, handle) != 0)
            {
                handle = 0;
            }
        }
        finally
        {
            if (handle != 0)
            {
                NativeInterop.GlobalFree(handle);
            }

            NativeInterop.CloseClipboard();
        }
    }
}
