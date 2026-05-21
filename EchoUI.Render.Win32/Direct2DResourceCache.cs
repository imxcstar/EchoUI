using System.Runtime.InteropServices;
using EchoUI.Core;
using EchoUI.Core.Text;
using Vortice.DCommon;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.DXGI;
using Vortice.Mathematics;
using EchoColor = EchoUI.Core.Color;

namespace EchoUI.Render.Win32;

internal sealed class Direct2DResourceCache : IDisposable
{
    private const int MaxBrushes = 256;
    private const int MaxTextFormats = 128;

    private readonly IDWriteFactory _writeFactory;
    private readonly Dictionary<uint, BrushCacheEntry> _brushes = [];
    private readonly LinkedList<uint> _brushLru = [];
    private readonly Dictionary<BorderStyle, ID2D1StrokeStyle?> _strokeStyles = [];
    private readonly Dictionary<TextFormatKey, TextFormatCacheEntry> _textFormats = [];
    private readonly LinkedList<TextFormatKey> _textFormatLru = [];
    private readonly Dictionary<ImageResource, ID2D1Bitmap> _bitmaps = [];
    private ID2D1RenderTarget? _target;
    private bool _disposed;

    public Direct2DResourceCache()
    {
        _writeFactory = DWrite.DWriteCreateFactory<IDWriteFactory>(Vortice.DirectWrite.FactoryType.Shared);
    }

    public void SetRenderTarget(ID2D1RenderTarget target)
    {
        if (ReferenceEquals(_target, target))
            return;

        ClearTargetResources();
        _target = target;
    }

    public ID2D1SolidColorBrush GetBrush(EchoColor color)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_target == null)
            throw new InvalidOperationException("Direct2D render target has not been set.");

        var key = PackColor(color);
        if (_brushes.TryGetValue(key, out var entry))
        {
            Touch(_brushLru, entry.Node);
            return entry.Brush;
        }

        var brush = _target.CreateSolidColorBrush(ToColor4(color));
        var node = _brushLru.AddFirst(key);
        _brushes[key] = new BrushCacheEntry(brush, node);
        TrimBrushes();
        return brush;
    }

    public ID2D1StrokeStyle? GetStrokeStyle(BorderStyle style)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_target == null)
            throw new InvalidOperationException("Direct2D render target has not been set.");

        if (style == BorderStyle.Solid)
            return null;

        if (!_strokeStyles.TryGetValue(style, out var strokeStyle))
        {
            var dashStyle = style switch
            {
                BorderStyle.Dashed => DashStyle.Dash,
                BorderStyle.Dotted => DashStyle.Dot,
                _ => DashStyle.Solid
            };
            strokeStyle = _target.Factory.CreateStrokeStyle(new StrokeStyleProperties { DashStyle = dashStyle });
            _strokeStyles[style] = strokeStyle;
        }

        return strokeStyle;
    }

    public IDWriteTextFormat GetTextFormat(TextStyle style)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var key = TextFormatKey.From(style);
        if (_textFormats.TryGetValue(key, out var entry))
        {
            Touch(_textFormatLru, entry.Node);
            return entry.Format;
        }

        var format = _writeFactory.CreateTextFormat(key.FontFamily, null, key.FontWeight, FontStyle.Normal, FontStretch.Normal, key.FontSize, string.Empty);
        format.TextAlignment = TextAlignment.Leading;
        format.ParagraphAlignment = ParagraphAlignment.Near;
        format.WordWrapping = WordWrapping.NoWrap;
        var node = _textFormatLru.AddFirst(key);
        _textFormats[key] = new TextFormatCacheEntry(format, node);
        TrimTextFormats();
        return format;
    }

    public ID2D1Bitmap GetBitmap(ImageResource image)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_target == null)
            throw new InvalidOperationException("Direct2D render target has not been set.");

        if (_bitmaps.TryGetValue(image, out var bitmap))
            return bitmap;

        if (!MemoryMarshal.TryGetArray(image.Pixels, out ArraySegment<byte> segment) || segment.Array == null)
            segment = new ArraySegment<byte>(image.Pixels.ToArray());

        var handle = GCHandle.Alloc(segment.Array, GCHandleType.Pinned);
        try
        {
            var properties = new BitmapProperties(new PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied));
            bitmap = _target.CreateBitmap(
                new SizeI(image.Width, image.Height),
                handle.AddrOfPinnedObject() + segment.Offset,
                (uint)image.Stride,
                properties);
            _bitmaps[image] = bitmap;
            return bitmap;
        }
        finally
        {
            handle.Free();
        }
    }

    public void ResetRenderTarget()
    {
        ClearTargetResources();
        _target = null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        ClearTargetResources();
        foreach (var entry in _textFormats.Values)
            entry.Format.Dispose();
        _textFormats.Clear();
        _textFormatLru.Clear();
        _writeFactory.Dispose();
    }

    private void ClearTargetResources()
    {
        foreach (var entry in _brushes.Values)
            entry.Brush.Dispose();
        _brushes.Clear();
        _brushLru.Clear();

        foreach (var strokeStyle in _strokeStyles.Values)
            strokeStyle?.Dispose();
        _strokeStyles.Clear();

        foreach (var bitmap in _bitmaps.Values)
            bitmap.Dispose();
        _bitmaps.Clear();
    }

    private void TrimBrushes()
    {
        while (_brushes.Count > MaxBrushes && _brushLru.Last != null)
        {
            var key = _brushLru.Last.Value;
            _brushLru.RemoveLast();
            if (_brushes.Remove(key, out var entry))
                entry.Brush.Dispose();
        }
    }

    private void TrimTextFormats()
    {
        while (_textFormats.Count > MaxTextFormats && _textFormatLru.Last != null)
        {
            var key = _textFormatLru.Last.Value;
            _textFormatLru.RemoveLast();
            if (_textFormats.Remove(key, out var entry))
                entry.Format.Dispose();
        }
    }

    private static void Touch<T>(LinkedList<T> lru, LinkedListNode<T> node)
    {
        if (!ReferenceEquals(lru.First, node))
        {
            lru.Remove(node);
            lru.AddFirst(node);
        }
    }

    private static uint PackColor(EchoColor color)
    {
        return (uint)(color.R | (color.G << 8) | (color.B << 16) | (color.A << 24));
    }

    private static Color4 ToColor4(EchoColor color)
    {
        return new Color4(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);
    }

    private readonly record struct TextFormatKey(string FontFamily, float FontSize, FontWeight FontWeight)
    {
        public static TextFormatKey From(TextStyle style)
        {
            var family = string.IsNullOrWhiteSpace(style.FontFamily) ? "Segoe UI" : style.FontFamily!;
            var weight = string.Equals(style.FontWeight, "bold", StringComparison.OrdinalIgnoreCase) || style.FontWeight == "700" || style.FontWeight == "800" || style.FontWeight == "900"
                ? FontWeight.Bold
                : FontWeight.Normal;
            return new TextFormatKey(family, style.EffectiveFontSize, weight);
        }
    }

    private sealed class BrushCacheEntry(ID2D1SolidColorBrush brush, LinkedListNode<uint> node)
    {
        public ID2D1SolidColorBrush Brush { get; } = brush;
        public LinkedListNode<uint> Node { get; } = node;
    }

    private sealed class TextFormatCacheEntry(IDWriteTextFormat format, LinkedListNode<TextFormatKey> node)
    {
        public IDWriteTextFormat Format { get; } = format;
        public LinkedListNode<TextFormatKey> Node { get; } = node;
    }
}
