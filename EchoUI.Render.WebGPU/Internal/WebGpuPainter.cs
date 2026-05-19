using EchoUI.Core;
using EchoUI.Render.Win32;
using WebGPU;

namespace EchoUI.Render.WebGPU.Internal;

/// <summary>
/// 遍历 Win32Element 树并向 UiBatchRenderer 提交 draw 命令。
/// 处理：背景填充、边框、圆角、文本（atlas 字形）、img 图片、Overflow clip、Float 顶层、Scroll 偏移。
/// </summary>
internal sealed class WebGpuPainter
{
    private readonly UiBatchRenderer _batch;
    private readonly GdiTextAtlas _textAtlas;
    private readonly TextureCache _textures;
    private readonly UiPipeline _pipeline;

    private readonly Stack<ScissorRect> _scissorStack = new();
    private int _viewportW;
    private int _viewportH;

    private readonly struct ScissorRect
    {
        public readonly int X, Y, W, H;
        public ScissorRect(int x, int y, int w, int h) { X = x; Y = y; W = w; H = h; }
        public ScissorRect Intersect(ScissorRect other)
        {
            int x = Math.Max(X, other.X);
            int y = Math.Max(Y, other.Y);
            int r = Math.Min(X + W, other.X + other.W);
            int b = Math.Min(Y + H, other.Y + other.H);
            return new ScissorRect(x, y, Math.Max(0, r - x), Math.Max(0, b - y));
        }
    }

    public WebGpuPainter(UiBatchRenderer batch, GdiTextAtlas textAtlas, TextureCache textures, UiPipeline pipeline)
    {
        _batch = batch;
        _textAtlas = textAtlas;
        _textures = textures;
        _pipeline = pipeline;
    }

    public void Paint(Win32Element root, IReadOnlyList<Win32Element> floatingElements, int viewportW, int viewportH)
    {
        _viewportW = viewportW;
        _viewportH = viewportH;
        _scissorStack.Clear();
        _scissorStack.Push(new ScissorRect(0, 0, viewportW, viewportH));
        ApplyScissor();

        // Clear is done by render pass loadOp=Clear; here we just draw children
        foreach (var child in root.Children)
        {
            if (child.Float) continue;
            DrawElement(child);
        }

        // Floating elements on top
        foreach (var f in floatingElements)
        {
            DrawElement(f);
        }
    }

    private void ApplyScissor()
    {
        var s = _scissorStack.Peek();
        _batch.SetScissor(s.X, s.Y, s.W, s.H);
    }

    private void DrawElement(Win32Element el)
    {
        if (el.LayoutWidth <= 0 || el.LayoutHeight <= 0)
            return;
        // Skip if entirely outside current scissor
        var cur = _scissorStack.Peek();
        if (el.AbsoluteX + el.LayoutWidth < cur.X || el.AbsoluteX > cur.X + cur.W ||
            el.AbsoluteY + el.LayoutHeight < cur.Y || el.AbsoluteY > cur.Y + cur.H)
        {
            return;
        }

        bool clip = el.Overflow != Overflow.Visible;
        if (clip)
        {
            var pushed = new ScissorRect(
                (int)el.AbsoluteX, (int)el.AbsoluteY,
                (int)Math.Ceiling(el.LayoutWidth), (int)Math.Ceiling(el.LayoutHeight))
                .Intersect(cur);
            _scissorStack.Push(pushed);
            ApplyScissor();
        }

        // Draw self
        switch (el.ElementType)
        {
            case ElementCoreName.Container:
            case "":
            case null:
                DrawBox(el);
                break;
            case ElementCoreName.Text:
                DrawText(el);
                break;
            case ElementCoreName.Input:
                DrawBox(el);  // input as a styled box (no native EDIT in WebGPU mode)
                break;
            case "img":
                DrawImage(el);
                break;
            default:
                // Unknown native -> render as container box
                DrawBox(el);
                break;
        }

        // Children — Note: LayoutEngine already applies parent.ScrollOffsetX/Y to child.AbsoluteX/Y,
        // so no additional translation is needed here.
        foreach (var child in el.Children)
        {
            if (child.Float) continue;
            DrawElement(child);
        }

        if (clip)
        {
            _scissorStack.Pop();
            ApplyScissor();
        }
    }

    private void DrawBox(Win32Element el)
    {
        var bg = el.BackgroundColor ?? default;
        Color borderColor = default;
        float borderW = 0;
        if (el.BorderStyle != BorderStyle.None)
        {
            var bc = el.IsFocused && el.FocusedBorderColor.HasValue
                ? el.FocusedBorderColor!.Value
                : el.BorderColor ?? default;
            if (bc.A > 0)
            {
                borderColor = bc;
                borderW = Math.Max(0f, el.BorderWidth);
            }
        }
        // If both invisible, nothing to draw
        if (bg.A == 0 && borderW == 0 && el.ElementType != ElementCoreName.Input)
            return;

        _batch.SetTexture(_textures.WhiteTextureView, _pipeline.LinearSampler);
        _batch.AddRect(
            el.AbsoluteX, el.AbsoluteY, el.LayoutWidth, el.LayoutHeight,
            bg, borderColor, borderW, el.BorderRadius,
            0, 0, 1, 1,
            hasTexture: false, isAlphaMask: false);
    }

    private void DrawImage(Win32Element el)
    {
        string? src = el.ImageSrc;
        // Actually 'img' is from NativeProps; let's try a couple of common paths.
        if (string.IsNullOrEmpty(src))
        {
            // No src — draw bg box only
            DrawBox(el);
            return;
        }

        var tex = _textures.LoadFromFile(src);
        _batch.SetTexture(tex.View, _pipeline.LinearSampler);
        _batch.AddRect(
            el.AbsoluteX, el.AbsoluteY, el.LayoutWidth, el.LayoutHeight,
            new Color(255, 255, 255, 255), default, 0, el.BorderRadius,
            0, 0, 1, 1,
            hasTexture: true, isAlphaMask: false);
    }

    private void DrawText(Win32Element el)
    {
        if (string.IsNullOrEmpty(el.Text)) return;

        float fontSize = el.FontSize > 0 ? el.FontSize : 14f;
        var color = el.TextColor ?? new Color(0, 0, 0);

        // 用 GDI 栅格化整条文本 → atlas region；painter 只画一个/几个矩形，绘制结果与
        // GdiPainter.DrawText 完全一致（同一字体、同一 DrawText 调用、同一 ClearType 输出）。
        _batch.SetTexture(_textAtlas.TextureView, _pipeline.PointSampler);

        var text = el.Text!;
        float baseX = MathF.Round(el.AbsoluteX);
        float y = MathF.Round(el.AbsoluteY);

        // 多行：按 '\n' 拆开，每行单独走 atlas。每行高度统一使用 atlas 返回的行盒高度。
        int start = 0;
        while (start <= text.Length)
        {
            int end = text.IndexOf('\n', start);
            if (end < 0) end = text.Length;

            // 去掉行尾 '\r'
            int lineEnd = end;
            if (lineEnd > start && text[lineEnd - 1] == '\r') lineEnd--;

            string line = text.Substring(start, lineEnd - start);

            // 空行：使用空格测一次以获得行高，仅推进 y。
            string runText = line.Length == 0 ? " " : line;
            var run = _textAtlas.GetRun(runText, el.FontFamily, fontSize, el.FontWeight);

            if (line.Length > 0)
            {
                _batch.AddRect(baseX, y, run.W, run.H, color, default, 0, 0,
                    run.U0, run.V0, run.U1, run.V1,
                    hasTexture: true, isAlphaMask: true);
            }

            y += run.H;
            if (end >= text.Length) break;
            start = end + 1;
        }
    }
}
