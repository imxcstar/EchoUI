namespace EchoUI.Core;

internal readonly record struct PropsProperty(
    string Name,
    Func<Props, object?> Getter,
    object? DefaultValue,
    bool IsDelegate = false);

internal static class PropsMetadata
{
    private static readonly PropsProperty[] BaseProps =
    [
        new(nameof(Props.Key), static props => props.Key, null),
        new(nameof(Props.Fallback), static props => props.Fallback, null),
        new(nameof(Props.AreEqual), static props => props.AreEqual, null, IsDelegate: true),
    ];

    private static readonly PropsProperty[] ContainerPropsProperties =
    [
        .. BaseProps,
        new(nameof(ContainerProps.Width), static props => ((ContainerProps)props).Width, null),
        new(nameof(ContainerProps.Height), static props => ((ContainerProps)props).Height, null),
        new(nameof(ContainerProps.MinWidth), static props => ((ContainerProps)props).MinWidth, null),
        new(nameof(ContainerProps.MinHeight), static props => ((ContainerProps)props).MinHeight, null),
        new(nameof(ContainerProps.MaxWidth), static props => ((ContainerProps)props).MaxWidth, null),
        new(nameof(ContainerProps.MaxHeight), static props => ((ContainerProps)props).MaxHeight, null),
        new(nameof(ContainerProps.Margin), static props => ((ContainerProps)props).Margin, null),
        new(nameof(ContainerProps.Float), static props => ((ContainerProps)props).Float, false),
        new(nameof(ContainerProps.Overflow), static props => ((ContainerProps)props).Overflow, null),
        new(nameof(ContainerProps.BackgroundColor), static props => ((ContainerProps)props).BackgroundColor, null),
        new(nameof(ContainerProps.BorderStyle), static props => ((ContainerProps)props).BorderStyle, null),
        new(nameof(ContainerProps.BorderColor), static props => ((ContainerProps)props).BorderColor, null),
        new(nameof(ContainerProps.BorderWidth), static props => ((ContainerProps)props).BorderWidth, null),
        new(nameof(ContainerProps.BorderRadius), static props => ((ContainerProps)props).BorderRadius, null),
        new(nameof(ContainerProps.Transitions), static props => ((ContainerProps)props).Transitions, null),
        new(nameof(ContainerProps.OnClick), static props => ((ContainerProps)props).OnClick, null, IsDelegate: true),
        new(nameof(ContainerProps.OnMouseMove), static props => ((ContainerProps)props).OnMouseMove, null, IsDelegate: true),
        new(nameof(ContainerProps.OnPointerDown), static props => ((ContainerProps)props).OnPointerDown, null, IsDelegate: true),
        new(nameof(ContainerProps.OnPointerMove), static props => ((ContainerProps)props).OnPointerMove, null, IsDelegate: true),
        new(nameof(ContainerProps.OnPointerUp), static props => ((ContainerProps)props).OnPointerUp, null, IsDelegate: true),
        new(nameof(ContainerProps.OnMouseEnter), static props => ((ContainerProps)props).OnMouseEnter, null, IsDelegate: true),
        new(nameof(ContainerProps.OnMouseLeave), static props => ((ContainerProps)props).OnMouseLeave, null, IsDelegate: true),
        new(nameof(ContainerProps.OnMouseDown), static props => ((ContainerProps)props).OnMouseDown, null, IsDelegate: true),
        new(nameof(ContainerProps.OnMouseUp), static props => ((ContainerProps)props).OnMouseUp, null, IsDelegate: true),
        new(nameof(ContainerProps.OnKeyDown), static props => ((ContainerProps)props).OnKeyDown, null, IsDelegate: true),
        new(nameof(ContainerProps.OnKeyUp), static props => ((ContainerProps)props).OnKeyUp, null, IsDelegate: true),
        new(nameof(ContainerProps.OnTextInput), static props => ((ContainerProps)props).OnTextInput, null, IsDelegate: true),
        new(nameof(ContainerProps.OnTextComposition), static props => ((ContainerProps)props).OnTextComposition, null, IsDelegate: true),
        new(nameof(ContainerProps.InputMethodAnchorPoint), static props => ((ContainerProps)props).InputMethodAnchorPoint, null),
        new(nameof(ContainerProps.SuppressContextMenu), static props => ((ContainerProps)props).SuppressContextMenu, false),
        new(nameof(ContainerProps.OnFocus), static props => ((ContainerProps)props).OnFocus, null, IsDelegate: true),
        new(nameof(ContainerProps.OnBlur), static props => ((ContainerProps)props).OnBlur, null, IsDelegate: true),
        new(nameof(ContainerProps.Direction), static props => ((ContainerProps)props).Direction, LayoutDirection.Vertical),
        new(nameof(ContainerProps.JustifyContent), static props => ((ContainerProps)props).JustifyContent, null),
        new(nameof(ContainerProps.AlignItems), static props => ((ContainerProps)props).AlignItems, null),
        new(nameof(ContainerProps.FlexGrow), static props => ((ContainerProps)props).FlexGrow, null),
        new(nameof(ContainerProps.FlexShrink), static props => ((ContainerProps)props).FlexShrink, null),
        new(nameof(ContainerProps.Gap), static props => ((ContainerProps)props).Gap, null),
        new(nameof(ContainerProps.Padding), static props => ((ContainerProps)props).Padding, null),
        new(nameof(ContainerProps.Shadow), static props => ((ContainerProps)props).Shadow, null),
        new(nameof(ContainerProps.Cursor), static props => ((ContainerProps)props).Cursor, null),
        new(nameof(ContainerProps.Opacity), static props => ((ContainerProps)props).Opacity, null),
        new(nameof(ContainerProps.Transform), static props => ((ContainerProps)props).Transform, null),
        new(nameof(ContainerProps.TransformOrigin), static props => ((ContainerProps)props).TransformOrigin, null),
    ];

    private static readonly PropsProperty[] TextPropsProperties =
    [
        .. BaseProps,
        new(nameof(TextProps.Text), static props => ((TextProps)props).Text, string.Empty),
        new(nameof(TextProps.FontFamily), static props => ((TextProps)props).FontFamily, null),
        new(nameof(TextProps.FontSize), static props => ((TextProps)props).FontSize, null),
        new(nameof(TextProps.Color), static props => ((TextProps)props).Color, null),
        new(nameof(TextProps.FontWeight), static props => ((TextProps)props).FontWeight, null),
        new(nameof(TextProps.MouseThrough), static props => ((TextProps)props).MouseThrough, true),
        new(nameof(TextProps.NoWrap), static props => ((TextProps)props).NoWrap, false),
    ];

    private static readonly PropsProperty[] NativePropsProperties =
    [
        .. BaseProps,
        new(nameof(NativeProps.Type), static props => ((NativeProps)props).Type, null),
    ];

    private static readonly PropsProperty[] InputPropsProperties =
    [
        .. BaseProps,
        new(nameof(InputProps.Value), static props => ((InputProps)props).Value, string.Empty),
        new(nameof(InputProps.OnValueChanged), static props => ((InputProps)props).OnValueChanged, null, IsDelegate: true),
        new(nameof(InputProps.BackgroundColor), static props => ((InputProps)props).BackgroundColor, null),
        new(nameof(InputProps.TextColor), static props => ((InputProps)props).TextColor, null),
        new(nameof(InputProps.BorderColor), static props => ((InputProps)props).BorderColor, null),
        new(nameof(InputProps.FocusedBorderColor), static props => ((InputProps)props).FocusedBorderColor, null),
        new(nameof(InputProps.Padding), static props => ((InputProps)props).Padding, null),
    ];

    public static IReadOnlyList<PropsProperty> Get(Props props)
    {
        return props switch
        {
            ContainerProps => ContainerPropsProperties,
            TextProps => TextPropsProperties,
            NativeProps => NativePropsProperties,
            InputProps => InputPropsProperties,
            _ => BaseProps,
        };
    }
}
