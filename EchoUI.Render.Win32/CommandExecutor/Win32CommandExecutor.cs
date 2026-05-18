using EchoUI.Core;

namespace EchoUI.Render.Win32;

/// <summary>Win32 绘制命令执行器：将 RenderCommand 翻译为 GDI+ 调用</summary>
internal static class Win32CommandExecutor
{
    [ThreadStatic]
    private static Stack<uint>? s_gdiPlusClipStates;

    [ThreadStatic]
    private static Stack<nint>? s_transformMatrices;

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
                    GdiPlus.Flush();
                    var blur = Math.Max(0, s.Blur);
                    var ext = new RectF(s.Layout.X - blur, s.Layout.Y,
                        s.Layout.Width + blur * 2, s.Layout.Height + s.OffsetY + blur);
                    GdiPainter.FillShape(hdc, null, ext, s.Color, s.BorderRadius + blur);
                }
                break;

            case DrawText d:
                {
                    GdiPlus.Flush();
                    var tc = element?.TextColor ?? d.Color;
                    var fs = element != null && element.FontSize > 0 ? element.FontSize : d.FontSize;
                    GdiText.DrawText(hdc, d.Text, d.FontFamily, fs, d.FontWeight, tc,
                        new RectF(d.Layout.X, d.Layout.Y, d.Layout.Width, d.Layout.Height), d.NoWrap);
                }
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
}
