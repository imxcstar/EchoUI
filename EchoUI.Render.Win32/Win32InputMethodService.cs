namespace EchoUI.Render.Win32;

internal sealed class Win32InputMethodService
{
    private readonly Func<nint> _getHwnd;

    public Win32InputMethodService(Func<nint> getHwnd)
    {
        _getHwnd = getHwnd;
    }

    public void UpdatePosition(Win32Element? element)
    {
        var hwnd = _getHwnd();
        if (hwnd == 0 || element?.InputMethodAnchorPoint == null)
            return;

        var himc = NativeInterop.ImmGetContext(hwnd);
        if (himc == 0)
            return;

        try
        {
            var anchor = element.InputMethodAnchorPoint.Value;
            var x = Math.Max(0, (int)Math.Round(element.AbsoluteX + anchor.X));
            var y = Math.Max(0, (int)Math.Round(element.AbsoluteY + anchor.Y));
            var point = new NativeInterop.POINT { X = x, Y = y };

            var compositionForm = new NativeInterop.COMPOSITIONFORM
            {
                dwStyle = NativeInterop.CFS_POINT | NativeInterop.CFS_FORCE_POSITION,
                ptCurrentPos = point
            };
            NativeInterop.ImmSetCompositionWindow(himc, ref compositionForm);

            var candidateForm = new NativeInterop.CANDIDATEFORM
            {
                dwIndex = 0,
                dwStyle = NativeInterop.CFS_CANDIDATEPOS,
                ptCurrentPos = point
            };
            NativeInterop.ImmSetCandidateWindow(himc, ref candidateForm);
        }
        finally
        {
            NativeInterop.ImmReleaseContext(hwnd, himc);
        }
    }
}
