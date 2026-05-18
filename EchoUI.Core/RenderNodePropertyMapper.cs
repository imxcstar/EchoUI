namespace EchoUI.Core;

public static class RenderNodePropertyMapper
{
    public static void ApplyProperty<TNode>(TNode node, Props props, string propName, object? propValue)
        where TNode : RenderNode<TNode>
    {
        switch (props)
        {
            case ContainerProps:
                ApplyContainerProperty(node, propName, propValue);
                break;
            case TextProps:
                ApplyTextProperty(node, propName, propValue);
                break;
            case InputProps:
                ApplyInputProperty(node, propName, propValue);
                break;
        }
    }

    public static void ApplyDefaults<TNode>(TNode node, Props props)
        where TNode : RenderNode<TNode>
    {
        switch (props)
        {
            case ContainerProps p:
                node.Direction = p.Direction ?? LayoutDefaults.Direction;
                node.JustifyContent = p.JustifyContent ?? LayoutDefaults.JustifyContent;
                node.AlignItems = p.AlignItems ?? LayoutDefaults.AlignItems;
                node.FlexShrink = p.FlexShrink ?? LayoutDefaults.FlexShrink;
                node.FlexGrow = p.FlexGrow ?? LayoutDefaults.FlexGrow;
                break;
            case TextProps p:
                node.MouseThrough = p.MouseThrough;
                node.NoWrap = p.NoWrap;
                break;
            case InputProps:
                ApplyInputBorderDefaults(node);
                break;
        }
    }

    public static void UpdateEventHandlers<TNode>(TNode node, Props props)
        where TNode : RenderNode<TNode>
    {
        switch (props)
        {
            case ContainerProps p:
                node.OnClick = p.OnClick;
                node.OnMouseMove = p.OnMouseMove;
                node.OnPointerDown = p.OnPointerDown;
                node.OnPointerMove = p.OnPointerMove;
                node.OnPointerUp = p.OnPointerUp;
                node.OnMouseEnter = p.OnMouseEnter;
                node.OnMouseLeave = p.OnMouseLeave;
                node.OnMouseDown = p.OnMouseDown;
                node.OnMouseUp = p.OnMouseUp;
                node.OnKeyDown = p.OnKeyDown;
                node.OnKeyUp = p.OnKeyUp;
                node.OnTextInput = p.OnTextInput;
                node.OnTextComposition = p.OnTextComposition;
                node.OnFocus = p.OnFocus;
                node.OnBlur = p.OnBlur;
                break;
            case InputProps ip:
                node.OnValueChanged = ip.OnValueChanged;
                break;
        }
    }

    public static void ClearEventHandlers<TNode>(TNode node)
        where TNode : RenderNode<TNode>
    {
        node.OnClick = null;
        node.OnMouseMove = null;
        node.OnPointerDown = null;
        node.OnPointerMove = null;
        node.OnPointerUp = null;
        node.OnMouseEnter = null;
        node.OnMouseLeave = null;
        node.OnMouseDown = null;
        node.OnMouseUp = null;
        node.OnKeyDown = null;
        node.OnKeyUp = null;
        node.OnTextInput = null;
        node.OnTextComposition = null;
        node.OnFocus = null;
        node.OnBlur = null;
        node.OnValueChanged = null;
    }

    private static void ApplyContainerProperty<TNode>(TNode node, string propName, object? propValue)
        where TNode : RenderNode<TNode>
    {
        switch (propName)
        {
            case nameof(ContainerProps.Width): node.Width = propValue as Dimension?; break;
            case nameof(ContainerProps.Height): node.Height = propValue as Dimension?; break;
            case nameof(ContainerProps.MinWidth): node.MinWidth = propValue as Dimension?; break;
            case nameof(ContainerProps.MinHeight): node.MinHeight = propValue as Dimension?; break;
            case nameof(ContainerProps.MaxWidth): node.MaxWidth = propValue as Dimension?; break;
            case nameof(ContainerProps.MaxHeight): node.MaxHeight = propValue as Dimension?; break;
            case nameof(ContainerProps.Margin): node.Margin = propValue as Spacing?; break;
            case nameof(ContainerProps.Padding): node.Padding = propValue as Spacing?; break;
            case nameof(ContainerProps.Direction): node.Direction = propValue is LayoutDirection dir ? dir : LayoutDefaults.Direction; break;
            case nameof(ContainerProps.JustifyContent): node.JustifyContent = propValue is JustifyContent jc ? jc : LayoutDefaults.JustifyContent; break;
            case nameof(ContainerProps.AlignItems): node.AlignItems = propValue is AlignItems ai ? ai : LayoutDefaults.AlignItems; break;
            case nameof(ContainerProps.FlexGrow): node.FlexGrow = propValue is float fg ? fg : LayoutDefaults.FlexGrow; break;
            case nameof(ContainerProps.FlexShrink): node.FlexShrink = propValue is float fs ? fs : LayoutDefaults.FlexShrink; break;
            case nameof(ContainerProps.Gap): node.Gap = propValue is float gap ? gap : LayoutDefaults.Gap; break;
            case nameof(ContainerProps.Float): node.Float = propValue is true; break;
            case nameof(ContainerProps.Overflow): node.Overflow = propValue is Overflow ov ? ov : Overflow.Visible; break;
            case nameof(ContainerProps.BackgroundColor): node.BackgroundColor = propValue as Color?; break;
            case nameof(ContainerProps.BorderColor): node.BorderColor = propValue as Color?; break;
            case nameof(ContainerProps.BorderStyle): node.BorderStyle = propValue is BorderStyle bs ? bs : BorderStyle.None; break;
            case nameof(ContainerProps.BorderWidth): node.BorderWidth = propValue is float bw ? bw : 0; break;
            case nameof(ContainerProps.BorderRadius): node.BorderRadius = propValue is float br ? br : 0; break;
            case nameof(ContainerProps.Shadow): node.Shadow = propValue is BoxShadow shadow ? shadow : BoxShadow.None; break;
            case nameof(ContainerProps.Opacity): node.Opacity = propValue is float op ? op : 1f; break;
            case nameof(ContainerProps.Transform): node.Transform = propValue is Transform transform ? transform : new Transform(); break;
            case nameof(ContainerProps.TransformOrigin): node.TransformOrigin = propValue is TransformOrigin origin ? origin : TransformOrigin.Center; break;
            case nameof(ContainerProps.Cursor): node.Cursor = propValue as string; break;
            case nameof(ContainerProps.InputMethodAnchorPoint): node.InputMethodAnchorPoint = propValue is Point point ? point : null; break;
        }
    }

    private static void ApplyTextProperty<TNode>(TNode node, string propName, object? propValue)
        where TNode : RenderNode<TNode>
    {
        switch (propName)
        {
            case nameof(TextProps.Text): node.Text = propValue as string; break;
            case nameof(TextProps.FontFamily): node.FontFamily = propValue as string; break;
            case nameof(TextProps.FontSize): node.FontSize = propValue is float fs ? fs : 14; break;
            case nameof(TextProps.Color): node.TextColor = propValue as Color?; break;
            case nameof(TextProps.FontWeight): node.FontWeight = propValue as string; break;
            case nameof(TextProps.MouseThrough): node.MouseThrough = propValue is not false; break;
            case nameof(TextProps.NoWrap): node.NoWrap = propValue is true; break;
        }
    }

    private static void ApplyInputProperty<TNode>(TNode node, string propName, object? propValue)
        where TNode : RenderNode<TNode>
    {
        switch (propName)
        {
            case nameof(InputProps.Value): node.InputValue = propValue as string; break;
            case nameof(InputProps.BackgroundColor): node.BackgroundColor = propValue as Color?; break;
            case nameof(InputProps.TextColor): node.TextColor = propValue as Color?; break;
            case nameof(InputProps.BorderColor):
                node.BorderColor = propValue as Color?;
                ApplyInputBorderDefaults(node);
                break;
            case nameof(InputProps.FocusedBorderColor):
                node.FocusedBorderColor = propValue as Color?;
                ApplyInputBorderDefaults(node);
                break;
            case nameof(InputProps.Padding): node.Padding = propValue as Spacing?; break;
        }
    }

    private static void ApplyInputBorderDefaults<TNode>(TNode node)
        where TNode : RenderNode<TNode>
    {
        if (node.BorderColor.HasValue || node.FocusedBorderColor.HasValue)
        {
            if (node.BorderStyle == BorderStyle.None)
                node.BorderStyle = BorderStyle.Solid;
            if (node.BorderWidth <= 0)
                node.BorderWidth = 1;
        }
        else
        {
            node.BorderStyle = BorderStyle.None;
            node.BorderWidth = 0;
        }
    }
}
