using EchoUI.Core;

namespace EchoUI.Render.Win32;

internal static class Win32FrameRecorder
{
    public static RenderFrame Record(ComponentInstance? rootInstance, Win32Element? root, IReadOnlyCollection<Win32Element> floatingElements, int width, int height, IReadOnlyList<LayoutBox> dirtyRects, long version, int tileSize)
    {
        var viewport = new LayoutBox(0, 0, width, height);
        var effectiveDirtyRects = dirtyRects.Count == 0 ? [viewport] : dirtyRects.Select(r => TileGrid.Intersect(viewport, r)).Where(r => r.Width > 0 && r.Height > 0).ToArray();
        var tiles = TileGrid.FromDirtyRects(width, height, effectiveDirtyRects, tileSize);
        var paintCoverage = tiles.Count > 0
            ? tiles.Select(t => t.Bounds).Aggregate(LayoutBox.Zero, TileGrid.Union)
            : effectiveDirtyRects.Aggregate(LayoutBox.Zero, TileGrid.Union);
        var commands = new List<RenderCommand>(512)
        {
            new DrawRect(viewport, Color.White, 0)
        };

        if (rootInstance != null)
            AddNativeBackedInstanceCommands(rootInstance, commands, paintCoverage);
        else if (root != null)
            AddElementCommands(root, commands, paintCoverage, floatingElements);

        var renderedFloatingElements = new HashSet<Win32Element>(ReferenceEqualityComparer.Instance);
        foreach (var floating in floatingElements)
            AddFloatCommands(floating, commands, paintCoverage, renderedFloatingElements);

        return new RenderFrame(width, height, commands, effectiveDirtyRects, tiles, tileSize, version);
    }

    private static void AddNativeBackedInstanceCommands(ComponentInstance instance, List<RenderCommand> commands, LayoutBox paintRect, bool forcePaintSubtree = false)
    {
        var native = instance.NativeElement as Win32Element;
        var layout = instance.Layout;
        var props = instance.Element.Props;

        if (instance.Element.Type.IsNative && props is ContainerProps { Float: true })
            return;

        var nativeBounds = native != null && layout.HasValue ? Win32VisualBounds.GetVisualBounds(native, layout.Value) : (LayoutBox?)null;
        var intersectsPaint = forcePaintSubtree || nativeBounds == null || Intersects(nativeBounds.Value, paintRect);
        if (!intersectsPaint && native != null && (native.Overflow != Overflow.Visible || native.Children.Count == 0))
            return;

        var hasTransform = native != null && layout.HasValue && !native.Transform.IsEmpty;
        if (hasTransform)
            commands.Add(new PushTransform(layout!.Value, native!.Transform, native.TransformOrigin));

        if (native != null && layout.HasValue && intersectsPaint)
        {
            switch (native.ElementType)
            {
                case ElementCoreName.Container:
                case ElementCoreName.Text:
                case ElementCoreName.Input:
                    PaintEngine.AppendCommands(native, layout.Value, commands);
                    break;
                case "img":
                    AddImageCommand(native, layout.Value, commands);
                    break;
            }
        }

        var shouldClip = native != null && layout.HasValue && native.Overflow != Overflow.Visible;
        if (shouldClip)
            commands.Add(new PushClip(layout!.Value));

        foreach (var child in instance.Children)
            AddNativeBackedInstanceCommands(child, commands, paintRect, forcePaintSubtree || hasTransform);

        if (shouldClip)
            commands.Add(new PopClip());

        if (native != null && layout.HasValue && intersectsPaint && native.Overflow is Overflow.Auto or Overflow.Scroll)
            AddScrollbarCommands(native, layout.Value, commands);

        if (hasTransform)
            commands.Add(new PopTransform());
    }

    private static void AddElementCommands(Win32Element element, List<RenderCommand> commands, LayoutBox paintRect, IReadOnlyCollection<Win32Element>? skippedElements)
    {
        if (skippedElements != null && skippedElements.Contains(element))
            return;

        var bounds = element.AbsoluteBounds;
        var visualBounds = Win32VisualBounds.GetVisualBounds(element, bounds);
        if (!Intersects(visualBounds, paintRect))
        {
            if (element.Overflow != Overflow.Visible || element.Children.Count == 0)
                return;
        }

        if (element.ElementType is ElementCoreName.Container or ElementCoreName.Text or ElementCoreName.Input)
            PaintEngine.AppendCommands(element, bounds, commands);
        else if (element.ElementType == "img")
            AddImageCommand(element, bounds, commands);

        var shouldClip = element.Overflow != Overflow.Visible;
        if (shouldClip)
            commands.Add(new PushClip(bounds));

        foreach (var child in element.Children)
            AddElementCommands(child, commands, paintRect, skippedElements);

        if (shouldClip)
            commands.Add(new PopClip());

        if (element.Overflow is Overflow.Auto or Overflow.Scroll)
            AddScrollbarCommands(element, bounds, commands);
    }

    private static void AddFloatCommands(Win32Element element, List<RenderCommand> commands, LayoutBox paintRect, HashSet<Win32Element> renderedFloatingElements)
    {
        if (!renderedFloatingElements.Add(element))
            return;

        var bounds = element.AbsoluteBounds;
        var visualBounds = Win32VisualBounds.GetVisualBounds(element, bounds);
        if (!Intersects(visualBounds, paintRect))
        {
            if (element.Overflow != Overflow.Visible || element.Children.Count == 0)
                return;
        }

        if (element.ElementType == "img")
            AddImageCommand(element, bounds, commands);
        else
            PaintEngine.AppendCommands(element, bounds, commands);

        var shouldClip = element.Overflow != Overflow.Visible;
        if (shouldClip)
            commands.Add(new PushClip(bounds));

        foreach (var child in element.Children)
        {
            if (child.Float)
                continue;

            AddFloatCommands(child, commands, paintRect, renderedFloatingElements);
        }

        if (shouldClip)
            commands.Add(new PopClip());

        if (element.Overflow is Overflow.Auto or Overflow.Scroll)
            AddScrollbarCommands(element, bounds, commands);
    }

    private static void AddImageCommand(Win32Element element, LayoutBox bounds, List<RenderCommand> commands)
    {
        if (element.NativeImageHandle == 0 || element.NativeImageWidth <= 0 || element.NativeImageHeight <= 0 || bounds.Width <= 0 || bounds.Height <= 0)
            return;

        var copy = NativeInterop.CopyImage(element.NativeImageHandle, NativeInterop.IMAGE_BITMAP, 0, 0, NativeInterop.LR_CREATEDIBSECTION);
        if (copy != 0)
            commands.Add(new Win32DrawImage(bounds, copy, element.NativeImageWidth, element.NativeImageHeight));
    }

    private static void AddScrollbarCommands(Win32Element element, LayoutBox bounds, List<RenderCommand> commands)
    {
        var contentWidth = LayoutEngine.MeasureContentWidth(element);
        var contentHeight = LayoutEngine.MeasureContentHeight(element);
        var alwaysShow = element.Overflow == Overflow.Scroll;
        var showVertical = alwaysShow || contentHeight > element.LayoutHeight;
        var showHorizontal = alwaysShow || contentWidth > element.LayoutWidth;

        if (!showVertical && !showHorizontal)
            return;

        const float scrollbarSize = 6;
        var trackColor = new Color(237, 237, 237);
        var thumbColor = new Color(128, 128, 128);

        if (showVertical)
        {
            var trackHeight = Math.Max(0, bounds.Height - (showHorizontal ? scrollbarSize + 2 : 0));
            var trackRect = new LayoutBox(bounds.X + bounds.Width - scrollbarSize - 2, bounds.Y, scrollbarSize, trackHeight);
            if (alwaysShow && trackRect.Width > 0 && trackRect.Height > 0)
                commands.Add(new DrawRect(trackRect, trackColor, scrollbarSize / 2));

            var maxScroll = Math.Max(0, contentHeight - element.LayoutHeight);
            var thumbHeight = maxScroll > 0 && contentHeight > 0 ? Math.Max(20, trackHeight * (element.LayoutHeight / contentHeight)) : trackHeight;
            var thumbY = maxScroll > 0 ? bounds.Y + (element.ScrollOffsetY / maxScroll) * Math.Max(0, trackHeight - thumbHeight) : bounds.Y;
            var thumbRect = new LayoutBox(bounds.X + bounds.Width - scrollbarSize - 2, thumbY, scrollbarSize, Math.Max(0, thumbHeight));
            if (thumbRect.Width > 0 && thumbRect.Height > 0)
                commands.Add(new DrawRect(thumbRect, thumbColor, scrollbarSize / 2));
        }

        if (showHorizontal)
        {
            var trackWidth = Math.Max(0, bounds.Width - (showVertical ? scrollbarSize + 2 : 0));
            var trackRect = new LayoutBox(bounds.X, bounds.Y + bounds.Height - scrollbarSize - 2, trackWidth, scrollbarSize);
            if (alwaysShow && trackRect.Width > 0 && trackRect.Height > 0)
                commands.Add(new DrawRect(trackRect, trackColor, scrollbarSize / 2));

            var maxScroll = Math.Max(0, contentWidth - element.LayoutWidth);
            var thumbWidth = maxScroll > 0 && contentWidth > 0 ? Math.Max(20, trackWidth * (element.LayoutWidth / contentWidth)) : trackWidth;
            var thumbX = maxScroll > 0 ? bounds.X + (element.ScrollOffsetX / maxScroll) * Math.Max(0, trackWidth - thumbWidth) : bounds.X;
            var thumbRect = new LayoutBox(thumbX, bounds.Y + bounds.Height - scrollbarSize - 2, Math.Max(0, thumbWidth), scrollbarSize);
            if (thumbRect.Width > 0 && thumbRect.Height > 0)
                commands.Add(new DrawRect(thumbRect, thumbColor, scrollbarSize / 2));
        }
    }

    private static bool Intersects(LayoutBox a, LayoutBox b)
    {
        return a.X + a.Width >= b.X && a.X <= b.X + b.Width && a.Y + a.Height >= b.Y && a.Y <= b.Y + b.Height;
    }
}

internal static class Win32VisualBounds
{
    public static LayoutBox GetVisualBounds(Win32Element element, LayoutBox layout)
    {
        var bounds = ExpandForShadow(layout, element.Shadow);
        if (!element.Transform.IsEmpty)
            bounds = Union(bounds, GetTransformedBounds(layout, element.Transform, element.TransformOrigin));
        return bounds;
    }

    private static LayoutBox ExpandForShadow(LayoutBox layout, BoxShadow shadow)
    {
        if (!shadow.IsVisible)
            return layout;

        var blur = Math.Max(0, shadow.Blur);
        var left = layout.X - blur;
        var top = layout.Y + Math.Min(0, shadow.OffsetY) - blur;
        var right = layout.X + layout.Width + blur;
        var bottom = layout.Y + layout.Height + Math.Max(0, shadow.OffsetY) + blur;
        return new LayoutBox(left, top, right - left, bottom - top);
    }

    private static LayoutBox GetTransformedBounds(LayoutBox layout, Transform transform, TransformOrigin origin)
    {
        var p1 = TransformPoint(layout.X, layout.Y, layout, transform, origin);
        var p2 = TransformPoint(layout.X + layout.Width, layout.Y, layout, transform, origin);
        var p3 = TransformPoint(layout.X, layout.Y + layout.Height, layout, transform, origin);
        var p4 = TransformPoint(layout.X + layout.Width, layout.Y + layout.Height, layout, transform, origin);

        var left = Math.Min(Math.Min(p1.X, p2.X), Math.Min(p3.X, p4.X));
        var top = Math.Min(Math.Min(p1.Y, p2.Y), Math.Min(p3.Y, p4.Y));
        var right = Math.Max(Math.Max(p1.X, p2.X), Math.Max(p3.X, p4.X));
        var bottom = Math.Max(Math.Max(p1.Y, p2.Y), Math.Max(p3.Y, p4.Y));
        return new LayoutBox(left, top, right - left, bottom - top);
    }

    private static (float X, float Y) TransformPoint(float x, float y, LayoutBox layout, Transform transform, TransformOrigin origin)
    {
        var ox = layout.X + layout.Width * origin.X;
        var oy = layout.Y + layout.Height * origin.Y;
        x -= ox;
        y -= oy;

        foreach (var fn in transform.Functions)
        {
            switch (fn)
            {
                case TranslateTransform t:
                    x += t.X;
                    y += t.Y;
                    break;
                case ScaleTransform s:
                    x *= s.X;
                    y *= s.Y;
                    break;
                case RotateTransform r:
                    var rad = r.AngleDeg * Math.PI / 180.0;
                    var cos = (float)Math.Cos(rad);
                    var sin = (float)Math.Sin(rad);
                    var rx = x * cos - y * sin;
                    var ry = x * sin + y * cos;
                    x = rx;
                    y = ry;
                    break;
                case SkewTransform k:
                    var sx = (float)Math.Tan(k.XDeg * Math.PI / 180.0);
                    var sy = (float)Math.Tan(k.YDeg * Math.PI / 180.0);
                    var originalX = x;
                    var originalY = y;
                    x = originalX + sx * originalY;
                    y = originalY + sy * originalX;
                    break;
            }
        }

        return (x + ox, y + oy);
    }

    private static LayoutBox Union(LayoutBox a, LayoutBox b)
    {
        var left = Math.Min(a.X, b.X);
        var top = Math.Min(a.Y, b.Y);
        var right = Math.Max(a.X + a.Width, b.X + b.Width);
        var bottom = Math.Max(a.Y + a.Height, b.Y + b.Height);
        return new LayoutBox(left, top, right - left, bottom - top);
    }
}
