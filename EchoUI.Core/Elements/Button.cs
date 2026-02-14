namespace EchoUI.Core
{
    /// <summary>
    /// 按钮的属性。
    /// </summary>
    public record class ButtonProps : Props
    {
        public string Text { get; init; }
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

            var currentBgColor = props.BackgroundColor ?? Color.LightGray;
            if (isPressed.Value)
            {
                currentBgColor = props.PressedColor ?? Color.Gray;
            }
            else if (isHovered.Value)
            {
                currentBgColor = props.HoverColor ?? Color.Gainsboro;
            }

            return Container(new ContainerProps
            {
                Key = props.Key,
                Width = props.Width ?? Dimension.Pixels(props.Text.Length * 10),
                Height = props.Height ?? Dimension.Pixels(30),
                JustifyContent = JustifyContent.Center,
                AlignItems = AlignItems.Center,
                Padding = props.Padding ?? new Spacing(Dimension.Pixels(8), Dimension.Pixels(4)),
                BackgroundColor = currentBgColor,
                BorderRadius = props.BorderRadius ?? 4,
                OnMouseEnter = () => setIsHovered(true),
                OnMouseLeave = () =>
                {
                    setIsHovered(false);
                    setIsPressed(false); // 如果鼠标在按下时离开，也重置状态
                },
                OnMouseDown = () => setIsPressed(true),
                OnMouseUp = () => setIsPressed(false),
                OnClick = button => props.OnClick?.Invoke(button),
                Children =
                [
                    Text(new TextProps
                    {
                        Text = props.Text,
                        Color = props.TextColor ?? Color.Black
                    })
                ]
            });
        }
    }
}
