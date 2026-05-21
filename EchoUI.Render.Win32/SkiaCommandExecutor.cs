using EchoUI.Core;
using EchoUI.Core.Text;
using SkiaSharp;

namespace EchoUI.Render.Win32;

internal static class SkiaCommandExecutor
{
    [ThreadStatic]
    private static Stack<int>? s_canvasSaveCounts;

    private static readonly ITextRunMeasurer TextMeasurer = new CachingTextRunMeasurer(new SkiaTextRunMeasurer());

    public static void Execute(SKCanvas canvas, IReadOnlyList<RenderCommand> commands)
    {
        canvas.Save();
        try
        {
            foreach (var command in commands)
                ExecuteOne(canvas, command);
        }
        finally
        {
            canvas.Restore();
            s_canvasSaveCounts?.Clear();
        }
    }

    private static void ExecuteOne(SKCanvas canvas, RenderCommand command)
    {
        switch (command)
        {
            case DrawRect rect:
                DrawRectCommand(canvas, rect);
                break;
            case DrawBorder border:
                DrawBorderCommand(canvas, border);
                break;
            case DrawShadow shadow:
                DrawShadowCommand(canvas, shadow);
                break;
            case DrawText text:
                DrawTextCommand(canvas, text);
                break;
            case DrawTextLayout textLayout:
                DrawTextLayoutCommand(canvas, textLayout.Layout, textLayout.TextLayout);
                break;
            case DrawImage image:
                DrawImageCommand(canvas, image);
                break;
            case PushClip clip:
                PushClipCommand(canvas, clip);
                break;
            case PopClip:
                PopCanvasState(canvas);
                break;
            case PushTransform transform:
                PushTransformCommand(canvas, transform);
                break;
            case PopTransform:
                PopCanvasState(canvas);
                break;
        }
    }

    private static void DrawRectCommand(SKCanvas canvas, DrawRect command)
    {
        if (command.BackgroundColor is not { A: > 0 } color || command.Layout.Width <= 0 || command.Layout.Height <= 0)
            return;

        using var paint = new SKPaint
        {
            IsAntialias = true,
            Color = ToSkColor(color),
            Style = SKPaintStyle.Fill
        };
        canvas.DrawRoundRect(ToSkRect(command.Layout), command.BorderRadius, command.BorderRadius, paint);
    }

    private static void DrawBorderCommand(SKCanvas canvas, DrawBorder command)
    {
        if (command.Color.A == 0 || command.Width <= 0 || command.Style == BorderStyle.None || command.Layout.Width <= 0 || command.Layout.Height <= 0)
            return;

        using var paint = new SKPaint
        {
            IsAntialias = true,
            Color = ToSkColor(command.Color),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = command.Width
        };

        if (command.Style == BorderStyle.Dashed)
            paint.PathEffect = SKPathEffect.CreateDash([Math.Max(1, command.Width * 3), Math.Max(1, command.Width * 2)], 0);
        else if (command.Style == BorderStyle.Dotted)
            paint.PathEffect = SKPathEffect.CreateDash([Math.Max(1, command.Width), Math.Max(1, command.Width * 2)], 0);

        var inset = command.Width / 2f;
        var rect = ToSkRect(command.Layout);
        rect.Inflate(-inset, -inset);
        var radius = Math.Max(0, command.Radius - inset);
        canvas.DrawRoundRect(rect, radius, radius, paint);
    }

    private static void DrawShadowCommand(SKCanvas canvas, DrawShadow command)
    {
        if (command.Color.A == 0 || (command.OffsetY == 0 && command.Blur <= 0) || command.Layout.Width <= 0 || command.Layout.Height <= 0)
            return;

        using var paint = new SKPaint
        {
            IsAntialias = true,
            Color = ToSkColor(command.Color),
            Style = SKPaintStyle.Fill,
            ImageFilter = command.Blur > 0 ? SKImageFilter.CreateBlur(command.Blur / 2f, command.Blur / 2f) : null
        };

        var rect = ToSkRect(command.Layout);
        rect.Offset(0, command.OffsetY);
        canvas.DrawRoundRect(rect, command.BorderRadius, command.BorderRadius, paint);
    }

    private static void DrawTextCommand(SKCanvas canvas, DrawText command)
    {
        if (string.IsNullOrEmpty(command.Text) || command.Layout.Width <= 0)
            return;

        var layout = TextLayoutEngine.LayoutPlain(command.Text, command.CreateStyle(), command.CreateLayoutOptions(), TextMeasurer);
        DrawTextLayoutCommand(canvas, command.Layout, layout);
    }

    private static void DrawTextLayoutCommand(SKCanvas canvas, LayoutBox bounds, TextLayoutResult layout)
    {
        foreach (var line in layout.Lines)
        {
            foreach (var fragment in line.Fragments)
            {
                if (string.IsNullOrEmpty(fragment.Text))
                    continue;

                using var typeface = CreateTypeface(fragment.Style);
                using var font = new SKFont(typeface, fragment.Style.EffectiveFontSize);
                font.Subpixel = true;
                font.Edging = SKFontEdging.Antialias;
                using var paint = new SKPaint
                {
                    IsAntialias = true,
                    Color = ToSkColor(fragment.Style.Color)
                };

                canvas.DrawText(fragment.Text, bounds.X + line.X + fragment.X, bounds.Y + line.Y + fragment.Baseline, font, paint);
            }
        }
    }

    private static void DrawImageCommand(SKCanvas canvas, DrawImage command)
    {
        if (command.Image.Width <= 0 || command.Image.Height <= 0 || command.Image.Pixels.IsEmpty || command.Layout.Width <= 0 || command.Layout.Height <= 0)
            return;

        if (command.Image.Format != ImagePixelFormat.Bgra8888Premultiplied)
            return;

        using var data = SKData.CreateCopy(command.Image.Pixels.Span);
        using var image = SKImage.FromPixelCopy(new SKImageInfo(command.Image.Width, command.Image.Height, SKColorType.Bgra8888, SKAlphaType.Premul), data.Data, command.Image.Stride);
        if (image == null)
            return;

        using var paint = new SKPaint { IsAntialias = true, FilterQuality = SKFilterQuality.High };
        canvas.DrawImage(image, ToSkRect(command.Layout), paint);
    }

    private static void PushClipCommand(SKCanvas canvas, PushClip command)
    {
        PushCanvasState(canvas);
        canvas.ClipRect(ToSkRect(command.Layout), SKClipOperation.Intersect, antialias: true);
    }

    private static void PushTransformCommand(SKCanvas canvas, PushTransform command)
    {
        PushCanvasState(canvas);
        var matrix = BuildTransformMatrix(command.Layout, command.Transform, command.Origin);
        canvas.Concat(ref matrix);
    }

    private static void PushCanvasState(SKCanvas canvas)
    {
        s_canvasSaveCounts ??= [];
        s_canvasSaveCounts.Push(canvas.Save());
    }

    private static void PopCanvasState(SKCanvas canvas)
    {
        if (s_canvasSaveCounts is not { Count: > 0 })
            return;

        canvas.RestoreToCount(s_canvasSaveCounts.Pop());
    }

    private static SKMatrix BuildTransformMatrix(LayoutBox layout, Transform transform, TransformOrigin origin)
    {
        var ox = layout.X + layout.Width * origin.X;
        var oy = layout.Y + layout.Height * origin.Y;
        var matrix = SKMatrix.CreateIdentity();
        matrix = matrix.PostConcat(SKMatrix.CreateTranslation(-ox, -oy));

        foreach (var fn in transform.Functions)
        {
            var next = fn switch
            {
                TranslateTransform t => SKMatrix.CreateTranslation(t.X, t.Y),
                ScaleTransform s => SKMatrix.CreateScale(s.X, s.Y),
                RotateTransform r => SKMatrix.CreateRotationDegrees(r.AngleDeg),
                SkewTransform s => SKMatrix.CreateSkew((float)Math.Tan(s.XDeg * Math.PI / 180.0), (float)Math.Tan(s.YDeg * Math.PI / 180.0)),
                _ => SKMatrix.CreateIdentity()
            };
            matrix = matrix.PostConcat(next);
        }

        return matrix.PostConcat(SKMatrix.CreateTranslation(ox, oy));
    }

    private static SKRect ToSkRect(LayoutBox layout)
    {
        return new SKRect(layout.X, layout.Y, layout.X + layout.Width, layout.Y + layout.Height);
    }

    private static SKColor ToSkColor(Color color)
    {
        return new SKColor(color.R, color.G, color.B, color.A);
    }

    private static SKTypeface CreateTypeface(TextStyle style)
    {
        var slant = SKFontStyleSlant.Upright;
        var weight = string.Equals(style.FontWeight, "bold", StringComparison.OrdinalIgnoreCase) ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal;
        return SKTypeface.FromFamilyName(style.FontFamily, weight, SKFontStyleWidth.Normal, slant);
    }

    private sealed class SkiaTextRunMeasurer : ITextRunMeasurer
    {
        public TextRunMeasurement Measure(string text, TextStyle style)
        {
            using var typeface = CreateTypeface(style);
            using var font = new SKFont(typeface, style.EffectiveFontSize);
            font.Subpixel = true;
            font.Edging = SKFontEdging.Antialias;
            using var paint = new SKPaint(font);
            var width = string.IsNullOrEmpty(text) ? 0 : paint.MeasureText(text);
            font.GetFontMetrics(out var metrics);
            var height = Math.Max(0, metrics.Descent - metrics.Ascent + metrics.Leading);
            var baseline = -metrics.Ascent;
            return new TextRunMeasurement(width, height, baseline);
        }
    }
}
