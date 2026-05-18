namespace EchoUI.Core;

/// <summary>绘制引擎：从 Element 的属性自动生成 RenderCommand 列表</summary>
public static class PaintEngine
{
    /// <summary>从单个元素生成绘制命令（不含子元素递归）</summary>
    public static List<RenderCommand> GenerateCommands(Element el, LayoutBox layout)
    {
        var list = new List<RenderCommand>();
        AddElementCommands(el, layout, list);
        return list;
    }

    /// <summary>从元素树生成绘制命令，布局由调用方提供。</summary>
    public static List<RenderCommand> GenerateCommands(Element root, Func<Element, LayoutBox?> resolveLayout)
    {
        var list = new List<RenderCommand>();
        AddTreeCommands(root, resolveLayout, list);
        return list;
    }

    /// <summary>从实例树生成绘制命令，布局直接取自实例。</summary>
    public static List<RenderCommand> GenerateCommands(ComponentInstance root)
    {
        var list = new List<RenderCommand>();
        AddInstanceTreeCommands(root, list);
        return list;
    }

    /// <summary>从 RenderNode 树生成绘制命令，样式和布局直接取自平台无关节点。</summary>
    public static List<RenderCommand> GenerateCommands<TNode>(TNode root, Func<TNode, bool>? skipSubtree = null)
        where TNode : RenderNode<TNode>
    {
        var list = new List<RenderCommand>();
        AddRenderNodeTreeCommands(root, list, skipSubtree);
        return list;
    }

    /// <summary>从单个 RenderNode 生成绘制命令（不含子元素递归）。</summary>
    public static List<RenderCommand> GenerateCommands<TNode>(TNode node, LayoutBox layout)
        where TNode : RenderNode<TNode>
    {
        var list = new List<RenderCommand>();
        AddRenderNodeCommands(node, layout, list);
        return list;
    }

    private static void AddTreeCommands(Element el, Func<Element, LayoutBox?> resolveLayout, List<RenderCommand> list)
    {
        var layout = resolveLayout(el);
        if (!layout.HasValue) return;

        bool hasTransform = el.Props is ContainerProps cp && cp.Transform.HasValue && !cp.Transform.Value.IsEmpty;
        if (hasTransform)
        {
            var t = ((ContainerProps)el.Props).Transform!.Value;
            var origin = ((ContainerProps)el.Props).TransformOrigin ?? TransformOrigin.Center;
            list.Add(new PushTransform(layout.Value, t, origin));
        }

        AddElementCommands(el, layout.Value, list);

        var shouldClip = el.Props is ContainerProps cp2 && (cp2.Overflow ?? Overflow.Visible) != Overflow.Visible;
        if (shouldClip)
        {
            list.Add(new PushClip(layout.Value));
        }

        foreach (var child in el.Props.Children)
        {
            AddTreeCommands(child, resolveLayout, list);
        }

        if (shouldClip)
        {
            list.Add(new PopClip());
        }

        if (hasTransform)
        {
            list.Add(new PopTransform());
        }
    }

    private static void AddRenderNodeTreeCommands<TNode>(TNode node, List<RenderCommand> list, Func<TNode, bool>? skipSubtree)
        where TNode : RenderNode<TNode>
    {
        if (skipSubtree?.Invoke(node) == true)
            return;

        var layout = node.AbsoluteBounds;
        var hasTransform = !node.Transform.IsEmpty;
        if (hasTransform)
        {
            list.Add(new PushTransform(layout, node.Transform, node.TransformOrigin));
        }

        AddRenderNodeCommands(node, layout, list);

        var shouldClip = node.Overflow != Overflow.Visible;
        if (shouldClip)
        {
            list.Add(new PushClip(layout));
        }

        foreach (var child in node.Children)
        {
            AddRenderNodeTreeCommands(child, list, skipSubtree);
        }

        if (shouldClip)
        {
            list.Add(new PopClip());
        }

        if (hasTransform)
        {
            list.Add(new PopTransform());
        }
    }

    private static void AddRenderNodeCommands<TNode>(TNode node, LayoutBox layout, List<RenderCommand> list)
        where TNode : RenderNode<TNode>
    {
        if (node.ElementType == ElementCoreName.Container || string.IsNullOrEmpty(node.ElementType))
        {
            AddContainerNodeCommands(node, layout, list, node.BorderColor, node.BorderStyle, node.BorderWidth, node.BorderRadius, node.Shadow);
        }
        else if (node.ElementType == ElementCoreName.Text && !string.IsNullOrEmpty(node.Text))
        {
            list.Add(new DrawText(layout, node.Text,
                node.TextColor ?? new Color(0, 0, 0),
                node.FontFamily, node.FontSize > 0 ? node.FontSize : 14f, node.FontWeight, node.NoWrap));
        }
        else if (node.ElementType == ElementCoreName.Input)
        {
            var effectiveBorderColor = node.IsFocused && node.FocusedBorderColor.HasValue
                ? node.FocusedBorderColor
                : node.BorderColor;
            var borderStyle = effectiveBorderColor.HasValue
                ? (node.BorderStyle == BorderStyle.None ? BorderStyle.Solid : node.BorderStyle)
                : BorderStyle.None;
            var borderWidth = effectiveBorderColor.HasValue
                ? Math.Max(1f, node.BorderWidth)
                : 0f;
            AddContainerNodeCommands(node, layout, list, effectiveBorderColor, borderStyle, borderWidth, node.BorderRadius, BoxShadow.None);
        }
    }

    private static void AddContainerNodeCommands<TNode>(
        TNode node,
        LayoutBox layout,
        List<RenderCommand> list,
        Color? borderColor,
        BorderStyle borderStyle,
        float borderWidth,
        float borderRadius,
        BoxShadow shadow)
        where TNode : RenderNode<TNode>
    {
        if (shadow.IsVisible)
        {
            list.Add(new DrawShadow(layout, shadow.Color, shadow.OffsetY, borderRadius, shadow.Blur));
        }

        if (node.BackgroundColor is { A: > 0 } bg)
        {
            list.Add(new DrawRect(layout, bg, borderRadius));
        }

        if (borderStyle != BorderStyle.None && borderColor is { A: > 0 } bc && borderWidth > 0)
        {
            list.Add(new DrawBorder(layout, bc, borderWidth, borderRadius, borderStyle));
        }
    }

    private static void AddInstanceTreeCommands(ComponentInstance instance, List<RenderCommand> list)
    {
        var isFloat = instance.Element.Type.IsNative
            && instance.Element.Props is ContainerProps { Float: true };
        if (isFloat)
        {
            return;
        }

        bool hasTransform = instance.Element.Type.IsNative
            && instance.Layout.HasValue
            && instance.Element.Props is ContainerProps cp3
            && cp3.Transform.HasValue && !cp3.Transform.Value.IsEmpty;

        if (hasTransform)
        {
            var t = ((ContainerProps)instance.Element.Props).Transform!.Value;
            var origin = ((ContainerProps)instance.Element.Props).TransformOrigin ?? TransformOrigin.Center;
            list.Add(new PushTransform(instance.Layout!.Value, t, origin));
        }

        if (instance.Element.Type.IsNative && instance.Layout.HasValue)
        {
            var nativeType = instance.Element.Type.AsNativeType;
            if (nativeType == ElementCoreName.Container || nativeType == ElementCoreName.Text)
            {
                AddElementCommands(instance.Element, instance.Layout.Value, list);
            }
        }

        var shouldClip = instance.Element.Type.IsNative
            && instance.Layout.HasValue
            && instance.Element.Props is ContainerProps clp
            && (clp.Overflow ?? Overflow.Visible) != Overflow.Visible;

        if (shouldClip)
        {
            list.Add(new PushClip(instance.Layout!.Value));
        }

        foreach (var child in instance.Children)
        {
            AddInstanceTreeCommands(child, list);
        }

        if (shouldClip)
        {
            list.Add(new PopClip());
        }

        if (hasTransform)
        {
            list.Add(new PopTransform());
        }
    }

    private static void AddElementCommands(Element el, LayoutBox layout, List<RenderCommand> list)
    {
        if (el.Props is ContainerProps cp)
        {
            if (cp.Shadow.HasValue && cp.Shadow.Value.IsVisible)
            {
                var shadow = cp.Shadow.Value;
                list.Add(new DrawShadow(layout, shadow.Color, shadow.OffsetY, cp.BorderRadius ?? 0f, shadow.Blur));
            }

            if (cp.BackgroundColor is { A: > 0 } bg)
            {
                list.Add(new DrawRect(layout, bg, cp.BorderRadius ?? 0f));
            }

            if (cp.BorderStyle.HasValue && cp.BorderStyle.Value != BorderStyle.None
                && cp.BorderColor is { A: > 0 } bc && cp.BorderWidth.HasValue && cp.BorderWidth.Value > 0)
            {
                list.Add(new DrawBorder(layout, bc, cp.BorderWidth.Value, cp.BorderRadius ?? 0f, cp.BorderStyle.Value));
            }
        }

        if (el.Props is TextProps tp && !string.IsNullOrEmpty(tp.Text))
        {
            list.Add(new DrawText(layout, tp.Text,
                tp.Color ?? new Color(0, 0, 0),
                tp.FontFamily, tp.FontSize ?? 14f, tp.FontWeight, tp.NoWrap));
        }
    }
}
