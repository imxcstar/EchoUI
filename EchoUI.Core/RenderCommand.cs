using EchoUI.Core.Text;

namespace EchoUI.Core;

/// <summary>绘制命令抽象基类</summary>
public abstract record RenderCommand(LayoutBox Layout);

/// <summary>填充色块（含圆角）</summary>
public sealed record DrawRect(LayoutBox Layout, Color? BackgroundColor, float BorderRadius) : RenderCommand(Layout);

/// <summary>绘制文本：后端使用 Core.Text 布局引擎测绘后逐片段渲染。</summary>
public sealed record DrawText(
    LayoutBox Layout,
    string Text,
    Color Color,
    string? FontFamily,
    float FontSize,
    string? FontWeight,
    bool NoWrap = true,
    int MaxLines = 0,
    TextTrimming Trimming = TextTrimming.CharacterEllipsis,
    float? LineHeight = null,
    float LetterSpacing = 0) : RenderCommand(Layout)
{
    public TextStyle CreateStyle() => new(FontFamily, FontSize, FontWeight, Color, LetterSpacing, LineHeight);

    public TextLayoutOptions CreateLayoutOptions() => new(
        Math.Max(0, Layout.Width),
        NoWrap,
        MaxLines,
        Trimming,
        LineHeight);
}

/// <summary>已完成布局的文本绘制命令，可供后端或测试直接复用。</summary>
public sealed record DrawTextLayout(LayoutBox Layout, TextLayoutResult TextLayout) : RenderCommand(Layout);

public sealed record ImageResource(ReadOnlyMemory<byte> Pixels, int Width, int Height, int Stride, ImagePixelFormat Format);

public enum ImagePixelFormat
{
    Bgra8888Premultiplied
}

public sealed record DrawImage(LayoutBox Layout, ImageResource Image) : RenderCommand(Layout);

/// <summary>绘制边框</summary>
public sealed record DrawBorder(LayoutBox Layout, Color Color, float Width, float Radius, BorderStyle Style) : RenderCommand(Layout);

/// <summary>绘制 Y 偏移阴影（跟随圆角）</summary>
public sealed record DrawShadow(LayoutBox Layout, Color Color, float OffsetY, float BorderRadius, float Blur = 0) : RenderCommand(Layout);

/// <summary>设置裁剪区域</summary>
public sealed record PushClip(LayoutBox Layout) : RenderCommand(Layout);

/// <summary>恢复裁剪区域</summary>
public sealed record PopClip : RenderCommand
{
    public PopClip() : base(LayoutBox.Zero) { }
}

/// <summary>应用 CSS Transform（translate/rotate/scale/skew）</summary>
public sealed record PushTransform(LayoutBox Layout, Transform Transform, TransformOrigin Origin) : RenderCommand(Layout);

/// <summary>恢复 Transform</summary>
public sealed record PopTransform : RenderCommand
{
    public PopTransform() : base(LayoutBox.Zero) { }
}
