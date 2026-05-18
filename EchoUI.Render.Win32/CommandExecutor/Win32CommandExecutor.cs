using EchoUI.Core;
using EchoUI.Core.Text;

namespace EchoUI.Render.Win32;

/// <summary>Win32 绘制命令执行器：将 RenderCommand 翻译为 GDI+ 调用</summary>
internal static class Win32CommandExecutor
{
    [ThreadStatic]
    private static Stack<uint>? s_gdiPlusClipStates;

    [ThreadStatic]
    private static Stack<nint>? s_transformMatrices;

    private static readonly ITextRunMeasurer TextMeasurer = new CachingTextRunMeasurer(new GdiTextRunMeasurer());

    public static void ExecuteSingle(nint hdc, RenderCommand cmd, Win32Element? element = null)
    {
        ExecuteOne(hdc, cmd, element);
    }

    public static void Execute(nint hdc, List<RenderCommand> commands, Win32Element? element = null)
    {
        foreach (var cmd in commands)
            ExecuteOne(hdc, cmd, element);
    }

    private static void ExecuteOne(nint hdc, RenderCommand cmd, Win32Element? element)
    {
        switch (cmd)
        {
            case DrawRect r:
                {
                    // 动画期间优先取 Win32Element 当前值
                    var bg = element?.BackgroundColor ?? r.BackgroundColor;
                    var radius = element?.BorderRadius ?? r.BorderRadius;
                    if (bg is { A: > 0 } validBg)
                    {
                        GdiPlus.Flush();
                        GdiPainter.FillShape(hdc, element,
                            new RectF(r.Layout.X, r.Layout.Y, r.Layout.Width, r.Layout.Height),
                            validBg, radius);
                    }
                }
                break;

            case DrawBorder b:
                {
                    // 动画期间优先取 Win32Element 当前值
                    var bc = element?.BorderColor ?? b.Color;
                    var bw = element?.BorderWidth ?? b.Width;
                    var br = element?.BorderRadius ?? b.Radius;
                    var bs = element?.BorderStyle ?? b.Style;
                    if (bc.A > 0 && bw > 0 && bs != BorderStyle.None)
                    {
                        GdiPlus.Flush();
                        GdiPainter.DrawBorder(hdc, element,
                            new RectF(b.Layout.X, b.Layout.Y, b.Layout.Width, b.Layout.Height),
                            bc, bw, br, bs);
                    }
                }
                break;

            case DrawShadow s:
                if (s.Color.A > 0 && (s.OffsetY != 0 || s.Blur > 0))
                {
                    DrawShadow(hdc, s);
                }
                break;

            case DrawText d:
                DrawTextCommand(hdc, d, element);
                break;

            case DrawTextLayout d:
                DrawTextLayoutCommand(hdc, d.Layout, d.TextLayout, element?.TextColor);
                break;

            case PushClip c:
                NativeInterop.SaveDC(hdc);
                var clipRect = new RectF(c.Layout.X, c.Layout.Y, c.Layout.Width, c.Layout.Height);
                var clip = GdiPainter.ToRect(clipRect);
                NativeInterop.IntersectClipRect(hdc, clip.Left, clip.Top, clip.Right, clip.Bottom);
                s_gdiPlusClipStates ??= [];
                s_gdiPlusClipStates.Push(GdiPlus.SaveGraphics());
                GdiPlus.IntersectClip(clipRect);
                break;

            case PopClip:
                if (s_gdiPlusClipStates is { Count: > 0 })
                {
                    GdiPlus.RestoreGraphics(s_gdiPlusClipStates.Pop());
                }
                NativeInterop.RestoreDC(hdc, -1);
                break;

            case PushTransform t:
                {
                    NativeInterop.SaveDC(hdc);
                    uint gdiState = GdiPlus.SaveGraphics();
                    var rect = new RectF(t.Layout.X, t.Layout.Y, t.Layout.Width, t.Layout.Height);
                    var matrix = GdiPlus.BuildTransformMatrix(rect, t.Transform, t.Origin);
                    GdiPlus.SetWorldTransform(matrix);
                    s_transformMatrices ??= [];
                    s_transformMatrices.Push(matrix);
                    s_gdiPlusClipStates ??= [];
                    s_gdiPlusClipStates.Push(gdiState);
                }
                break;

            case PopTransform:
                {
                    if (s_transformMatrices is { Count: > 0 })
                    {
                        var matrix = s_transformMatrices.Pop();
                        NativeInterop.GdipDeleteMatrix(matrix);
                    }
                    if (s_gdiPlusClipStates is { Count: > 0 })
                    {
                        GdiPlus.RestoreGraphics(s_gdiPlusClipStates.Pop());
                    }
                    NativeInterop.RestoreDC(hdc, -1);
                }
                break;
        }
    }

    private static void DrawTextCommand(nint hdc, DrawText command, Win32Element? element)
    {
        GdiPlus.Flush();

        var color = element?.TextColor ?? command.Color;
        var fontSize = element != null && element.FontSize > 0 ? element.FontSize : command.FontSize;
        var fontFamily = element?.FontFamily ?? command.FontFamily;
        var fontWeight = element?.FontWeight ?? command.FontWeight;
        var style = new TextStyle(fontFamily, fontSize, fontWeight, color, command.LetterSpacing, command.LineHeight);
        var options = new TextLayoutOptions(
            Math.Max(0, command.Layout.Width),
            command.NoWrap,
            command.MaxLines,
            command.Trimming,
            command.LineHeight);
        var layout = TextLayoutEngine.LayoutPlain(command.Text, style, options, TextMeasurer);
        DrawTextLayoutCommand(hdc, command.Layout, layout, null);
    }

    private static void DrawTextLayoutCommand(nint hdc, LayoutBox bounds, TextLayoutResult layout, Color? overrideColor)
    {
        GdiPlus.Flush();

        foreach (var line in layout.Lines)
        {
            foreach (var fragment in line.Fragments)
            {
                if (string.IsNullOrEmpty(fragment.Text) || fragment.Width <= 0)
                    continue;

                var rect = new RectF(
                    bounds.X + line.X + fragment.X,
                    bounds.Y + line.Y,
                    Math.Max(1, fragment.Width + 1),
                    Math.Max(1, line.Height));
                var color = overrideColor ?? fragment.Style.Color;
                GdiText.DrawText(hdc, fragment.Text, fragment.Style.FontFamily, fragment.Style.EffectiveFontSize, fragment.Style.FontWeight, color, rect, noWrap: true);
            }
        }
    }

    private static void DrawShadow(nint hdc, DrawShadow shadow)
    {
        GdiPlus.Flush();

        var blur = Math.Max(0, shadow.Blur);
        if (blur <= 0)
        {
            var hardRect = new RectF(
                shadow.Layout.X,
                shadow.Layout.Y + shadow.OffsetY,
                shadow.Layout.Width,
                shadow.Layout.Height);
            GdiPainter.FillShape(hdc, null, hardRect, shadow.Color, shadow.BorderRadius);
            return;
        }

        var layerCount = Math.Clamp((int)Math.Ceiling(blur), 3, 18);
        var maxAlpha = Math.Min(shadow.Color.A, (byte)120);

        for (var layer = layerCount; layer >= 1; layer--)
        {
            var t = layer / (float)layerCount;
            var expand = blur * t;
            var weight = Math.Pow(1 - t * 0.75f, 2);
            var alpha = Math.Clamp((int)Math.Round(maxAlpha * weight / 3), 0, 255);
            if (alpha <= 0)
                continue;

            var color = shadow.Color.WithAlpha((byte)alpha);
            var rect = new RectF(
                shadow.Layout.X - expand,
                shadow.Layout.Y,
                shadow.Layout.Width + expand * 2,
                shadow.Layout.Height + shadow.OffsetY + expand);

            GdiPainter.FillShape(hdc, null, rect, color, shadow.BorderRadius + expand);
        }
    }
}
