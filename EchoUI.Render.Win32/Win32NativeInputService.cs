using EchoUI.Core;

namespace EchoUI.Render.Win32;

internal sealed class Win32NativeInputService
{
    private readonly Func<nint> _getParentHwnd;
    private readonly Action<Win32Element> _requestRepaint;
    private readonly Dictionary<nint, Win32Element> _editElements = [];
    private bool _suppressEditNotification;

    public Win32NativeInputService(Func<nint> getParentHwnd, Action<Win32Element> requestRepaint)
    {
        _getParentHwnd = getParentHwnd;
        _requestRepaint = requestRepaint;
    }

    public void Create(Win32Element element)
    {
        var parentHwnd = _getParentHwnd();
        if (parentHwnd == 0) return;

        var hwnd = NativeInterop.CreateWindowEx(
            0,
            "EDIT",
            "",
            NativeInterop.WS_CHILD | NativeInterop.WS_VISIBLE | NativeInterop.ES_AUTOHSCROLL | NativeInterop.ES_LEFT,
            0, 0, 100, 24,
            parentHwnd,
            0,
            NativeInterop.GetModuleHandle(null),
            0);

        if (hwnd == 0)
            return;

        element.EditHwnd = hwnd;
        _editElements[hwnd] = element;
        Sync(element);
    }

    public void Sync(Win32Element element)
    {
        if (element.EditHwnd == 0) return;

        if (element.InputValue != null)
        {
            var currentText = GetWindowText(element.EditHwnd);
            if (currentText != element.InputValue)
            {
                _suppressEditNotification = true;
                NativeInterop.SetWindowText(element.EditHwnd, element.InputValue);
                _suppressEditNotification = false;
            }
        }

        var fontHandle = GdiText.GetFontHandle(element.FontFamily, element.FontSize > 0 ? element.FontSize : 14, element.FontWeight);
        if (fontHandle != 0 && element.NativeFontHandle != fontHandle)
        {
            element.NativeFontHandle = fontHandle;
            NativeInterop.SendMessage(element.EditHwnd, NativeInterop.WM_SETFONT, fontHandle, 1);
        }

        NativeInterop.InvalidateRect(element.EditHwnd, 0, true);
    }

    public void HandleChange(nint editHwnd)
    {
        if (_suppressEditNotification) return;

        if (!_editElements.TryGetValue(editHwnd, out var element))
            return;

        var text = GetWindowText(editHwnd);
        element.OnValueChanged?.Invoke(text);

        var syncContext = SynchronizationContext.Current;
        if (syncContext != null)
            syncContext.Post(_ => RestoreControlledValue(element), null);
        else
            RestoreControlledValue(element);
    }

    public void HandleFocusChange(nint editHwnd, bool isFocused)
    {
        if (!_editElements.TryGetValue(editHwnd, out var element))
            return;

        element.IsFocused = isFocused;
        _requestRepaint(element);
    }

    public void UpdatePositions(Win32Element root, float viewportWidth, float viewportHeight, IReadOnlyList<Win32Element>? floatingElements = null)
    {
        UpdatePositionsRecursive(root, viewportWidth, viewportHeight, floatingElements ?? []);
    }

    public void Release(Win32Element element)
    {
        if (element.EditHwnd == 0)
            return;

        _editElements.Remove(element.EditHwnd);
        element.NativeFontHandle = 0;
        element.NativeBrushHandle = 0;

        if (NativeInterop.IsWindow(element.EditHwnd))
            NativeInterop.DestroyWindow(element.EditHwnd);

        element.EditHwnd = 0;
    }

    public Win32Element? GetElement(nint hwnd)
    {
        _editElements.TryGetValue(hwnd, out var element);
        return element;
    }

    private void RestoreControlledValue(Win32Element element)
    {
        if (element.EditHwnd == 0 || !NativeInterop.IsWindow(element.EditHwnd))
            return;

        var controlledValue = element.InputValue ?? string.Empty;
        var currentValue = GetWindowText(element.EditHwnd);
        if (currentValue == controlledValue)
            return;

        _suppressEditNotification = true;
        NativeInterop.SetWindowText(element.EditHwnd, controlledValue);
        _suppressEditNotification = false;
    }

    private static string GetWindowText(nint editHwnd)
    {
        int len = NativeInterop.GetWindowTextLength(editHwnd);
        var buffer = new char[len + 1];
        NativeInterop.GetWindowText(editHwnd, buffer, buffer.Length);
        return new string(buffer, 0, len);
    }

    private void UpdatePositionsRecursive(Win32Element element, float viewportWidth, float viewportHeight, IReadOnlyList<Win32Element> floatingElements)
    {
        if (element.EditHwnd != 0)
            UpdateElementPosition(element, viewportWidth, viewportHeight, floatingElements);

        foreach (var child in element.Children)
            UpdatePositionsRecursive(child, viewportWidth, viewportHeight, floatingElements);
    }

    private static void UpdateElementPosition(Win32Element element, float viewportWidth, float viewportHeight, IReadOnlyList<Win32Element> floatingElements)
    {
        var padding = ResolvePadding(element.Padding, element.LayoutWidth, viewportWidth, viewportHeight);
        var border = GetBorderInset(element);

        var contentX = element.AbsoluteX + padding.Left + border;
        var contentY = element.AbsoluteY + padding.Top + border;
        var contentW = Math.Max(0, element.LayoutWidth - padding.Left - padding.Right - border * 2);
        var contentH = Math.Max(0, element.LayoutHeight - padding.Top - padding.Bottom - border * 2);
        var editH = Math.Min(contentH, GetEditPreferredHeight(element));
        var editY = contentY + Math.Max(0, (contentH - editH) / 2f);

        var x = (int)Math.Floor(contentX);
        var y = (int)Math.Round(editY, MidpointRounding.AwayFromZero);
        var w = (int)Math.Ceiling(contentW);
        var h = Math.Max(1, (int)Math.Round(editH, MidpointRounding.AwayFromZero));

        var editRect = new RectF(x, y, w, h);
        var clipRect = GetEditClipRect(element, viewportWidth, viewportHeight);
        var visibleRect = RectF.Intersect(editRect, clipRect);

        if (visibleRect.Width <= 0 || visibleRect.Height <= 0 || w <= 0 || h <= 0)
        {
            NativeInterop.ShowWindow(element.EditHwnd, NativeInterop.SW_HIDE);
            return;
        }

        var region = CreateEditVisibleRegion(element, editRect, visibleRect, floatingElements);
        if (region == 0)
        {
            NativeInterop.ShowWindow(element.EditHwnd, NativeInterop.SW_HIDE);
            return;
        }

        NativeInterop.ShowWindow(element.EditHwnd, NativeInterop.SW_SHOW);
        NativeInterop.MoveWindow(element.EditHwnd, x, y, w, h, true);
        ApplyEditClipRegion(element.EditHwnd, region);
    }

    private static RectF GetEditClipRect(Win32Element element, float viewportWidth, float viewportHeight)
    {
        var clipRect = new RectF(0, 0, viewportWidth, viewportHeight);
        var current = element.Parent;

        while (current != null)
        {
            if (current.Overflow != Overflow.Visible)
                clipRect = RectF.Intersect(clipRect, current.GetAbsoluteBounds());

            if (current.Float)
                break;

            current = current.Parent;
        }

        return clipRect;
    }

    private static nint CreateEditVisibleRegion(Win32Element element, RectF editRect, RectF visibleRect, IReadOnlyList<Win32Element> floatingElements)
    {
        var left = Math.Max(0, (int)Math.Floor(visibleRect.Left - editRect.Left));
        var top = Math.Max(0, (int)Math.Floor(visibleRect.Top - editRect.Top));
        var right = Math.Max(left, (int)Math.Ceiling(visibleRect.Right - editRect.Left));
        var bottom = Math.Max(top, (int)Math.Ceiling(visibleRect.Bottom - editRect.Top));
        var region = NativeInterop.CreateRectRgn(left, top, right, bottom);
        if (region == 0)
            return 0;

        foreach (var floating in GetOccludingFloatingElements(element, floatingElements))
        {
            var floatingBounds = Win32VisualBounds.GetVisualBounds(floating, floating.AbsoluteBounds);
            var occlusion = RectF.Intersect(visibleRect, ToRectF(floatingBounds));
            if (occlusion.Width <= 0 || occlusion.Height <= 0)
                continue;

            var occlusionRegion = NativeInterop.CreateRectRgn(
                Math.Max(0, (int)Math.Floor(occlusion.Left - editRect.Left)),
                Math.Max(0, (int)Math.Floor(occlusion.Top - editRect.Top)),
                Math.Max(0, (int)Math.Ceiling(occlusion.Right - editRect.Left)),
                Math.Max(0, (int)Math.Ceiling(occlusion.Bottom - editRect.Top)));
            if (occlusionRegion == 0)
                continue;

            var combineResult = NativeInterop.CombineRgn(region, region, occlusionRegion, NativeInterop.RGN_DIFF);
            NativeInterop.DeleteObject(occlusionRegion);
            if (combineResult == NativeInterop.NULLREGION)
            {
                NativeInterop.DeleteObject(region);
                return 0;
            }
        }

        return region;
    }

    private static void ApplyEditClipRegion(nint hwnd, nint region)
    {
        if (NativeInterop.SetWindowRgn(hwnd, region, true) == 0)
            NativeInterop.DeleteObject(region);
    }

    private static RectF ToRectF(LayoutBox bounds)
    {
        return new RectF(bounds.X, bounds.Y, bounds.Width, bounds.Height);
    }

    private static IEnumerable<Win32Element> GetOccludingFloatingElements(Win32Element element, IReadOnlyList<Win32Element> floatingElements)
    {
        var ownFloatIndex = -1;
        for (var i = 0; i < floatingElements.Count; i++)
        {
            if (IsAncestorOf(floatingElements[i], element))
                ownFloatIndex = i;
        }

        for (var i = ownFloatIndex + 1; i < floatingElements.Count; i++)
        {
            var floating = floatingElements[i];
            if (ReferenceEquals(floating, element) || IsAncestorOf(floating, element))
                continue;

            yield return floating;
        }
    }

    private static bool IsAncestorOf(Win32Element candidateAncestor, Win32Element element)
    {
        var current = element.Parent;
        while (current != null)
        {
            if (ReferenceEquals(current, candidateAncestor))
                return true;

            current = current.Parent;
        }

        return false;
    }

    private static float GetEditPreferredHeight(Win32Element element)
    {
        var fontSize = element.FontSize > 0 ? element.FontSize : 14f;
        return GdiText.GetPreferredLineHeight(element.FontFamily, fontSize, element.FontWeight) + 1f;
    }

    private static float GetBorderInset(Win32Element element)
    {
        return element.BorderStyle == BorderStyle.None ? 0 : Math.Max(0, element.BorderWidth);
    }

    private static (float Left, float Top, float Right, float Bottom) ResolvePadding(Spacing? padding, float width, float viewportWidth, float viewportHeight)
    {
        if (padding == null) return (0, 0, 0, 0);
        return (
            ResolveDimension(padding.Value.Left, width, viewportWidth, viewportHeight),
            ResolveDimension(padding.Value.Top, width, viewportWidth, viewportHeight),
            ResolveDimension(padding.Value.Right, width, viewportWidth, viewportHeight),
            ResolveDimension(padding.Value.Bottom, width, viewportWidth, viewportHeight));
    }

    private static float ResolveDimension(Dimension? dimension, float parentSize, float viewportWidth, float viewportHeight)
    {
        if (dimension == null) return 0;
        return dimension.Value.Unit switch
        {
            DimensionUnit.Pixels => dimension.Value.Value,
            DimensionUnit.Percent => parentSize * dimension.Value.Value / 100f,
            DimensionUnit.ViewportHeight => viewportHeight * dimension.Value.Value / 100f,
            _ => 0
        };
    }
}
