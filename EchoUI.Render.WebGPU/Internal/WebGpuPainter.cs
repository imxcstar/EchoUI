using System.Numerics;
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

        // Transform：与 GdiPainter 一致 —— 围绕 (layout.X + W*originX, layout.Y + H*originY) 应用
        // translate/rotate/scale/skew。变换矩阵以 row-vector 方式叠加 (M_self * M_parent)，使
        // 旋转/缩放等会自然作用到所有后代。
        bool hasTransform = !el.Transform.IsEmpty;
        Matrix3x2 savedMatrix = default;
        if (hasTransform)
        {
            float ox = el.AbsoluteX + el.LayoutWidth * el.TransformOrigin.X;
            float oy = el.AbsoluteY + el.LayoutHeight * el.TransformOrigin.Y;
            var local = BuildTransformMatrix(ox, oy, el.Transform);
            // 当前批已有矩阵 M_parent，子层在它之上再套上自己的 local：v * (local * M_parent)
            // 但 SetTransform 是覆盖式 API，所以这里手动取出 + 组合。
            var combined = local * _batch.CurrentMatrix;
            savedMatrix = _batch.SetTransform(combined);
        }

        // Skip if entirely outside current scissor — 旋转下 axis-aligned 测试不准，跳过此优化。
        var cur = _scissorStack.Peek();
        if (!hasTransform && _batch.CurrentMatrix.IsIdentity &&
            (el.AbsoluteX + el.LayoutWidth < cur.X || el.AbsoluteX > cur.X + cur.W ||
             el.AbsoluteY + el.LayoutHeight < cur.Y || el.AbsoluteY > cur.Y + cur.H))
        {
            return;
        }

        // 注意：与 PaintEngine / GdiPainter 保持一致 —— 元素自身的 shadow/background/border 不受
        // 自己的 overflow:hidden 裁剪，PushClip 只作用于 children。所以这里先画自身，再 push scissor。

        // Box shadow（与 GdiPainter / Win32CommandExecutor.DrawShadow 同一套分层算法）：
        // 在自身绘制前画 N 层圆角矩形，每层向外扩展 expand=blur*t、alpha 衰减，叠加成软阴影。
        // 仅对容器/Input/未知 native 应用（与 PaintEngine 的 AddContainerNodeCommands 一致）。
        if (el.Shadow.IsVisible
            && el.ElementType != ElementCoreName.Text
            && el.ElementType != "img")
        {
            DrawShadow(el);
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

        // 自身绘制完成 → 现在 push scissor 给 children 用
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

        if (hasTransform)
        {
            _batch.SetTransform(savedMatrix);
        }
    }

    /// <summary>构建围绕 (ox, oy) 应用 transform.Functions 的 row-vector 2D 矩阵：
    /// M = T(-ox,-oy) * F1 * F2 * ... * Fn * T(+ox,+oy)。与 GdiPainter.BuildTransformMatrix 等价。</summary>
    private static Matrix3x2 BuildTransformMatrix(float ox, float oy, Transform transform)
    {
        var m = Matrix3x2.CreateTranslation(-ox, -oy);
        foreach (var fn in transform.Functions)
        {
            switch (fn)
            {
                case TranslateTransform t:
                    m *= Matrix3x2.CreateTranslation(t.X, t.Y);
                    break;
                case ScaleTransform s:
                    m *= Matrix3x2.CreateScale(s.X, s.Y);
                    break;
                case RotateTransform r:
                    m *= Matrix3x2.CreateRotation(r.AngleDeg * MathF.PI / 180f);
                    break;
                case SkewTransform k:
                    m *= Matrix3x2.CreateSkew(
                        k.XDeg * MathF.PI / 180f,
                        k.YDeg * MathF.PI / 180f);
                    break;
            }
        }
        m *= Matrix3x2.CreateTranslation(ox, oy);
        return m;
    }

    private void DrawBox(Win32Element el)
    {
        var bg = el.BackgroundColor ?? default;
        Color borderColor = default;
        float borderW = 0;
        var borderStyle = el.BorderStyle;
        if (borderStyle != BorderStyle.None)
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

        // Dashed/Dotted 边框：让 SDF shader 只画填充（不画边），边框用一串短线段单独贴。
        bool customBorder = borderW > 0 && (borderStyle == BorderStyle.Dashed || borderStyle == BorderStyle.Dotted);

        _batch.AddRect(
            el.AbsoluteX, el.AbsoluteY, el.LayoutWidth, el.LayoutHeight,
            bg, customBorder ? default : borderColor, customBorder ? 0 : borderW, el.BorderRadius,
            0, 0, 1, 1,
            hasTexture: false, isAlphaMask: false);

        if (customBorder)
        {
            DrawDashedBorder(el, borderColor, borderW, borderStyle);
        }
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
        // 使用 LinearSampler：在 identity 变换 + 整像素对齐下，双线性在纹素中心采样等同 point；
        // 但在旋转/缩放下会正确做过滤，避免点采样导致的阶梯锯齿（与 GdiPainter 一致：GDI+
        // 在 World Transform 下也对位图做线性插值）。
        _batch.SetTexture(_textAtlas.TextureView, _pipeline.LinearSampler);

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

    /// <summary>多层圆角矩形堆叠模拟模糊阴影 —— 算法与 Win32CommandExecutor.DrawShadow 完全一致，
    /// 保证 GDI 与 WebGPU 视觉一致。</summary>
    private void DrawShadow(Win32Element el)
    {
        var shadow = el.Shadow;
        float blur = MathF.Max(0, shadow.Blur);
        float br = el.BorderRadius;

        _batch.SetTexture(_textures.WhiteTextureView, _pipeline.LinearSampler);

        if (blur <= 0)
        {
            // 硬阴影：直接画一个偏移的实色矩形。
            _batch.AddRect(
                el.AbsoluteX, el.AbsoluteY + shadow.OffsetY,
                el.LayoutWidth, el.LayoutHeight,
                shadow.Color, default, 0, br,
                0, 0, 1, 1,
                hasTexture: false, isAlphaMask: false);
            return;
        }

        int layerCount = Math.Clamp((int)MathF.Ceiling(blur), 3, 18);
        int maxAlpha = Math.Min(shadow.Color.A, (byte)120);

        for (int layer = layerCount; layer >= 1; layer--)
        {
            float t = layer / (float)layerCount;
            float expand = blur * t;
            double weight = Math.Pow(1 - t * 0.75f, 2);
            int alpha = Math.Clamp((int)Math.Round(maxAlpha * weight / 3), 0, 255);
            if (alpha <= 0) continue;

            var c = shadow.Color.WithAlpha((byte)alpha);
            _batch.AddRect(
                el.AbsoluteX - expand,
                el.AbsoluteY,
                el.LayoutWidth + expand * 2,
                el.LayoutHeight + shadow.OffsetY + expand,
                c, default, 0, br + expand,
                0, 0, 1, 1,
                hasTexture: false, isAlphaMask: false);
        }
    }

    /// <summary>沿矩形边缘按 dash/dot 节奏画一串小段，模拟 GDI PS_DASH / PS_DOT 边框。
    /// 简化处理：圆角段直接被切，不沿圆弧走（与 GDI 在大圆角下也不完美贴合）。</summary>
    private void DrawDashedBorder(Win32Element el, Color color, float borderW, BorderStyle style)
    {
        // dash 节奏（像素）：与 GDI PS_DASH ≈ 4*w on / 2*w off；PS_DOT ≈ 1*w on / 2*w off
        float w = MathF.Max(1, borderW);
        float dash = style == BorderStyle.Dotted ? w : w * 4f;
        float gap = style == BorderStyle.Dotted ? w * 2f : w * 2f;

        float x = el.AbsoluteX;
        float y = el.AbsoluteY;
        float W = el.LayoutWidth;
        float H = el.LayoutHeight;

        // 四条边在内侧画（与 SDF 边框一致），半边宽贴合 rect 内边缘。
        float inset = w * 0.5f;

        // top
        DashLine(x + inset, y + inset, W - w, true, dash, gap, w, color);
        // bottom
        DashLine(x + inset, y + H - inset - w, W - w, true, dash, gap, w, color);
        // left
        DashLine(x + inset, y + inset, H - w, false, dash, gap, w, color);
        // right
        DashLine(x + W - inset - w, y + inset, H - w, false, dash, gap, w, color);
    }

    private void DashLine(float x, float y, float length, bool horizontal, float dash, float gap, float thickness, Color color)
    {
        if (length <= 0) return;
        float period = dash + gap;
        if (period <= 0) period = dash + 1;

        float cursor = 0;
        while (cursor < length)
        {
            float segLen = MathF.Min(dash, length - cursor);
            if (segLen <= 0) break;
            float rx = horizontal ? x + cursor : x;
            float ry = horizontal ? y : y + cursor;
            float rw = horizontal ? segLen : thickness;
            float rh = horizontal ? thickness : segLen;
            _batch.AddRect(rx, ry, rw, rh, color, default, 0, 0,
                0, 0, 1, 1,
                hasTexture: false, isAlphaMask: false);
            cursor += period;
        }
    }
}
