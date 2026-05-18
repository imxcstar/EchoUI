namespace EchoUI.Core
{
    /// <summary>
    /// 按钮的属性。
    /// </summary>
    public record class ButtonProps : Props
    {
        public string? Text { get; init; }
        /// <summary>
        /// 元素的宽度。
        /// </summary>
        public Dimension? Width { get; init; }
        /// <summary>
        /// 元素的高度。
        /// </summary>
        public Dimension? Height { get; init; }
        public Action<MouseButton>? OnClick { get; init; }
        public Color? BackgroundColor { get; init; }
        public Color? HoverColor { get; init; }
        public Color? PressedColor { get; init; }
        public Color? TextColor { get; init; }
        public Spacing? Padding { get; init; }
        public float? BorderRadius { get; init; }
        /// <summary>按钮尺寸: small / middle / large</summary>
        public string? Size { get; init; }
        /// <summary>是否禁用</summary>
        public bool? Disabled { get; init; }
    }

    public partial class Elements
    {
        /// <summary>
        /// Button 组件的渲染函数。
        /// </summary>
        [Element(DefaultProperty = nameof(ButtonProps.Text))]
        public static Element Button(ButtonProps props)
        {
            var (isHovered, setIsHovered, _) = Hooks.State(false);
            var (isPressed, setIsPressed, _) = Hooks.State(false);

            var sz = DesignTokens.PrimaryButton(props.Size ?? "middle");
            var disabled = props.Disabled == true;

            var hasCustomBackground = props.BackgroundColor is not null;
            var baseBg = props.BackgroundColor ?? DesignTokens.BgMain;
            var bg = disabled ? DesignTokens.BgDisabled : baseBg;
            var textColor = props.TextColor ??
                (disabled ? DesignTokens.TextDisabled : (hasCustomBackground ? DesignTokens.TextInverse : DesignTokens.TextTitle));
            var radius = props.BorderRadius ?? sz.Radius;

            // Animal Island 3D shadow: hover 时阴影加深，active 时阴影压缩
            var shadow = disabled ? Color.Transparent : DesignTokens.ShadowBtn;
            var shadowHeight = 5f;
            if (!disabled && isPressed.Value)
            {
                bg = props.PressedColor ?? (hasCustomBackground ? DesignTokens.PrimaryActive : DesignTokens.BgContent);
                shadowHeight = 1f;
            }
            else if (!disabled && isHovered.Value)
            {
                bg = props.HoverColor ?? (hasCustomBackground ? DesignTokens.PrimaryHover : Color.White);
                shadowHeight = 6f;
            }

            var padding = props.Padding ?? new Spacing(
                Dimension.Pixels(sz.PaddingX), Dimension.Pixels(0));

            var measuredTextWidth = Hooks.MeasureText(new TextMeasurementRequest
            {
                Text = props.Text ?? string.Empty,
                FontSize = sz.FontSize,
                FontWeight = "600"
            }).Width;
            var autoWidth = measuredTextWidth + sz.PaddingX * 2 + 24f;

            var transitions = new ValueDictionary<string, Transition>(new Dictionary<string, Transition>
            {
                [nameof(ContainerProps.BackgroundColor)] = new(180, Easing.EaseOut),
                [nameof(ContainerProps.Shadow)] = new(180, Easing.EaseOut),
            });

            return Container(new ContainerProps
            {
                Key = props.Key,
                Width = props.Width ?? Dimension.Pixels(autoWidth),
                Height = props.Height ?? Dimension.Pixels(sz.Height),
                MinWidth = Dimension.Pixels(88),
                JustifyContent = JustifyContent.Center,
                AlignItems = AlignItems.Center,
                Padding = padding,
                BackgroundColor = bg,
                BorderRadius = radius,
                Shadow = new BoxShadow(shadow, shadowHeight),
                Overflow = Overflow.Hidden,
                Transitions = transitions,
                Cursor = disabled ? "not-allowed" : null,
                Opacity = disabled ? 0.5f : 1f,
                OnMouseEnter = disabled ? null : () => setIsHovered(true),
                OnMouseLeave = () =>
                {
                    setIsHovered(false);
                    setIsPressed(false);
                },
                OnMouseDown = disabled ? null : () => setIsPressed(true),
                OnMouseUp = disabled ? null : () => setIsPressed(false),
                OnClick = disabled ? null : (Action<MouseButton>)(button => props.OnClick?.Invoke(button)),
                Children =
                [
                    Text(new TextProps
                    {
                        Text = props.Text ?? string.Empty,
                        Color = textColor,
                        FontSize = sz.FontSize,
                        FontWeight = "600",
                        NoWrap = true
                    })
                ]
            });
        }
    }
}
