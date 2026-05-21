using System.Numerics;
using System.Runtime.InteropServices;
using EchoUI.Core;
using EchoUI.Core.Text;
using Vortice;
using Vortice.DCommon;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.DXGI;
using Vortice.Mathematics;
using EchoColor = EchoUI.Core.Color;

namespace EchoUI.Render.Win32;

internal sealed class Direct2DCommandExecutor : IDisposable
{
    private readonly Direct2DResourceCache _resources;
    private readonly Stack<Matrix3x2> _transformStack = [];
    private readonly Stack<bool> _clipStack = [];
    private ID2D1RenderTarget? _target;

    private static readonly ITextRunMeasurer TextMeasurer = new CachingTextRunMeasurer(new GdiTextRunMeasurer());

    public Direct2DCommandExecutor()
    {
        _resources = new Direct2DResourceCache();
    }

    public void Execute(ID2D1RenderTarget target, IReadOnlyList<RenderCommand> commands)
    {
        _target = target;
        _resources.SetRenderTarget(target);
        _transformStack.Clear();
        _clipStack.Clear();
        try
        {
            foreach (var command in commands)
                ExecuteOne(command);
        }
        finally
        {
            while (_clipStack.Count > 0)
            {
                _target.PopAxisAlignedClip();
                _clipStack.Pop();
            }

            while (_transformStack.Count > 0)
            {
                _target.Transform = _transformStack.Pop();
            }

            _target = null;
        }
    }

    public void Dispose()
    {
        _resources.Dispose();
    }

    public void ResetRenderTargetResources()
    {
        _resources.ResetRenderTarget();
    }

    private void ExecuteOne(RenderCommand command)
    {
        switch (command)
        {
            case DrawRect rect:
                DrawRectCommand(rect);
                break;
            case DrawBorder border:
                DrawBorderCommand(border);
                break;
            case DrawShadow shadow:
                DrawShadowCommand(shadow);
                break;
            case DrawText text:
                DrawTextCommand(text);
                break;
            case DrawTextLayout textLayout:
                DrawTextLayoutCommand(textLayout.Layout, textLayout.TextLayout);
                break;
            case DrawImage image:
                DrawImageCommand(image);
                break;
            case PushClip clip:
                PushClipCommand(clip);
                break;
            case PopClip:
                PopClipCommand();
                break;
            case PushTransform transform:
                PushTransformCommand(transform);
                break;
            case PopTransform:
                PopTransformCommand();
                break;
        }
    }

    private void DrawRectCommand(DrawRect command)
    {
        if (_target == null || command.BackgroundColor is not { A: > 0 } color || command.Layout.Width <= 0 || command.Layout.Height <= 0)
            return;

        var brush = CreateBrush(color);
        var radius = ClampRadius(command.Layout, command.BorderRadius);
        if (radius > 0)
        {
            var rounded = new RoundedRectangle(ToRectangleF(command.Layout), radius, radius);
            _target.FillRoundedRectangle(ref rounded, brush);
        }
        else
        {
            _target.FillRectangle(ToRawRect(command.Layout), brush);
        }
    }

    private void DrawBorderCommand(DrawBorder command)
    {
        if (_target == null || command.Color.A == 0 || command.Width <= 0 || command.Style == BorderStyle.None || command.Layout.Width <= 0 || command.Layout.Height <= 0)
            return;

        var brush = CreateBrush(command.Color);
        var strokeStyle = CreateStrokeStyle(command.Style);
        var inset = command.Width / 2f;
        var rect = new LayoutBox(command.Layout.X + inset, command.Layout.Y + inset, Math.Max(0, command.Layout.Width - command.Width), Math.Max(0, command.Layout.Height - command.Width));
        var radius = ClampRadius(rect, Math.Max(0, command.Radius - inset));
        if (radius > 0)
        {
            var rounded = new RoundedRectangle(ToRectangleF(rect), radius, radius);
            _target.DrawRoundedRectangle(ref rounded, brush, command.Width, strokeStyle);
        }
        else
        {
            var raw = ToRawRect(rect);
            _target.DrawRectangle(raw, brush, command.Width, strokeStyle);
        }
    }

    private void DrawShadowCommand(DrawShadow command)
    {
        if (_target == null || command.Color.A == 0 || (command.OffsetY == 0 && command.Blur <= 0) || command.Layout.Width <= 0 || command.Layout.Height <= 0)
            return;

        var blur = Math.Max(0, command.Blur);
        var layerCount = blur <= 0 ? 1 : Math.Clamp((int)Math.Ceiling(blur), 3, 18);
        var maxAlpha = Math.Min(command.Color.A, (byte)120);
        for (var layer = layerCount; layer >= 1; layer--)
        {
            var t = layer / (float)layerCount;
            var expand = blur * t;
            var weight = blur <= 0 ? 1 : Math.Pow(1 - t * 0.75f, 2);
            var alpha = Math.Clamp((int)Math.Round(maxAlpha * weight / (blur <= 0 ? 1 : 3)), 0, 255);
            if (alpha <= 0)
                continue;

            var color = command.Color.WithAlpha((byte)alpha);
            var rect = new LayoutBox(command.Layout.X - expand, command.Layout.Y, command.Layout.Width + expand * 2, command.Layout.Height + command.OffsetY + expand);
            var brush = CreateBrush(color);
            var radius = ClampRadius(rect, command.BorderRadius + expand);
            var rounded = new RoundedRectangle(ToRectangleF(rect), radius, radius);
            _target.FillRoundedRectangle(ref rounded, brush);
        }
    }

    private void DrawTextCommand(DrawText command)
    {
        if (string.IsNullOrEmpty(command.Text) || command.Layout.Width <= 0)
            return;

        var layout = TextLayoutEngine.LayoutPlain(command.Text, command.CreateStyle(), command.CreateLayoutOptions(), TextMeasurer);
        DrawTextLayoutCommand(command.Layout, layout);
    }

    private void DrawTextLayoutCommand(LayoutBox bounds, TextLayoutResult layout)
    {
        if (_target == null)
            return;

        foreach (var line in layout.Lines)
        {
            foreach (var fragment in line.Fragments)
            {
                if (string.IsNullOrEmpty(fragment.Text))
                    continue;

                var brush = CreateBrush(fragment.Style.Color);
                var format = CreateTextFormat(fragment.Style);
                var rect = new Rect(
                    bounds.X + line.X + fragment.X,
                    bounds.Y + line.Y,
                    Math.Max(1, fragment.Width + 1),
                    Math.Max(1, line.Height));
                _target.DrawText(fragment.Text, format, rect, brush, DrawTextOptions.Clip, MeasuringMode.Natural);
            }
        }
    }

    private void DrawImageCommand(DrawImage command)
    {
        if (_target == null || command.Image.Width <= 0 || command.Image.Height <= 0 || command.Image.Pixels.IsEmpty || command.Layout.Width <= 0 || command.Layout.Height <= 0)
            return;

        if (command.Image.Format != ImagePixelFormat.Bgra8888Premultiplied)
            return;

        var properties = new BitmapProperties(new PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied));
        var pixels = command.Image.Pixels.ToArray();
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            using var d2dBitmap = _target.CreateBitmap(new SizeI(command.Image.Width, command.Image.Height), handle.AddrOfPinnedObject(), (uint)command.Image.Stride, properties);
            var dest = ToRawRect(command.Layout);
            _target.DrawBitmap(d2dBitmap, dest, 1f, BitmapInterpolationMode.Linear, null);
        }
        finally
        {
            handle.Free();
        }
    }

    private void PushClipCommand(PushClip command)
    {
        if (_target == null)
            return;

        _target.PushAxisAlignedClip(ToRawRect(command.Layout), AntialiasMode.PerPrimitive);
        _clipStack.Push(true);
    }

    private void PopClipCommand()
    {
        if (_target == null || _clipStack.Count == 0)
            return;

        _target.PopAxisAlignedClip();
        _clipStack.Pop();
    }

    private void PushTransformCommand(PushTransform command)
    {
        if (_target == null)
            return;

        _transformStack.Push(_target.Transform);
        var matrix = BuildTransformMatrix(command.Layout, command.Transform, command.Origin);
        _target.Transform = _target.Transform * matrix;
    }

    private void PopTransformCommand()
    {
        if (_target == null || _transformStack.Count == 0)
            return;

        _target.Transform = _transformStack.Pop();
    }

    private ID2D1SolidColorBrush CreateBrush(EchoColor color)
    {
        return _resources.GetBrush(color);
    }

    private ID2D1StrokeStyle? CreateStrokeStyle(BorderStyle style)
    {
        return _resources.GetStrokeStyle(style);
    }

    private IDWriteTextFormat CreateTextFormat(TextStyle style)
    {
        return _resources.GetTextFormat(style);
    }

    private static RawRectF ToRawRect(LayoutBox layout)
    {
        return new RawRectF(layout.X, layout.Y, layout.X + layout.Width, layout.Y + layout.Height);
    }

    private static System.Drawing.RectangleF ToRectangleF(LayoutBox layout)
    {
        return new System.Drawing.RectangleF(layout.X, layout.Y, layout.Width, layout.Height);
    }

    private static float ClampRadius(LayoutBox layout, float radius)
    {
        if (radius <= 0 || layout.Width <= 0 || layout.Height <= 0)
            return 0;

        return Math.Min(radius, Math.Min(layout.Width, layout.Height) / 2f);
    }

    private static Color4 ToColor4(EchoColor color)
    {
        return new Color4(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);
    }

    private static Matrix3x2 BuildTransformMatrix(LayoutBox layout, Transform transform, TransformOrigin origin)
    {
        var ox = layout.X + layout.Width * origin.X;
        var oy = layout.Y + layout.Height * origin.Y;
        var matrix = Matrix3x2.CreateTranslation(-ox, -oy);

        foreach (var fn in transform.Functions)
        {
            var next = fn switch
            {
                TranslateTransform t => Matrix3x2.CreateTranslation(t.X, t.Y),
                ScaleTransform s => Matrix3x2.CreateScale(s.X, s.Y),
                RotateTransform r => Matrix3x2.CreateRotation(r.AngleDeg * MathF.PI / 180f),
                SkewTransform s => Matrix3x2.CreateSkew(s.XDeg * MathF.PI / 180f, s.YDeg * MathF.PI / 180f),
                _ => Matrix3x2.Identity
            };
            matrix *= next;
        }

        return matrix * Matrix3x2.CreateTranslation(ox, oy);
    }
}
