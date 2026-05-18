namespace EchoUI.Core
{
    /// <summary>
    /// CheckBox (复选框) 组件的属性。
    /// </summary>
    public record class CheckBoxProps : Props
    {
        /// <summary>
        /// 复选框是否被选中。
        /// </summary>
        public bool IsChecked { get; init; } = false;

        /// <summary>
        /// 当复选框状态改变时触发的回调。
        /// </summary>
        public Action<bool>? OnToggle { get; init; }

        /// <summary>
        /// 显示在复选框旁边的文本标签。
        /// </summary>
        public string? Label { get; init; }

        /// <summary>
        /// 勾选标记的颜色。
        /// </summary>
        public Color? CheckColor { get; init; }

        /// <summary>
        /// 边框的颜色。
        /// </summary>
        public Color? BorderColor { get; init; }
    }

    public partial class Elements
    {
        /// <summary>
        /// CheckBox (复选框) 组件。
        /// </summary>
        [Element(DefaultProperty = nameof(CheckBoxProps.Label))]
        public static Element CheckBox(CheckBoxProps props)
        {
            var (check, _, updateCheck) = Hooks.State(props.IsChecked);
            var (hover, setHover, _) = Hooks.State(false);

            return Container(new ContainerProps
            {
                Key = props.Key,
                Direction = LayoutDirection.Horizontal,
                AlignItems = AlignItems.Center,
                Gap = 8,
                Cursor = "pointer",
                OnMouseEnter = () => setHover(true),
                OnMouseLeave = () => setHover(false),
                OnClick = _ =>
                {
                    updateCheck(v => !v);
                    props.OnToggle?.Invoke(check.Value);
                },
                Children =
                [
                    Container(new ContainerProps
                    {
                        Width = Dimension.Pixels(22),
                        Height = Dimension.Pixels(22),
                        BackgroundColor = check.Value ? DesignTokens.Primary : DesignTokens.BgContent,
                        BorderWidth = 2.5f,
                        BorderStyle = BorderStyle.Solid,
                        BorderColor = check.Value ? DesignTokens.PrimaryActive : (hover.Value ? DesignTokens.Primary : props.BorderColor ?? DesignTokens.Border),
                        BorderRadius = 8,
                        Shadow = new BoxShadow(check.Value ? DesignTokens.PrimaryActive : DesignTokens.ShadowInput, 2),
                        JustifyContent = JustifyContent.Center,
                        AlignItems = AlignItems.Center,
                        Transitions = new ValueDictionary<string, Transition>(new Dictionary<string, Transition>
                        {
                            [nameof(ContainerProps.BackgroundColor)] = new(150, Easing.EaseOut),
                            [nameof(ContainerProps.BorderColor)] = new(150, Easing.EaseOut),
                            [nameof(ContainerProps.Shadow)] = new(150, Easing.EaseOut),
                        }),
                        Children =
                        [
                            check.Value
                                ? Text(new TextProps
                                {
                                    Text = "✓",
                                    FontSize = 13,
                                    FontWeight = "900",
                                    Color = props.CheckColor ?? DesignTokens.TextInverse
                                })
                                : Empty()
                        ]
                    }),
                    string.IsNullOrEmpty(props.Label)
                        ? Empty()
                        : Text(new TextProps { Text = props.Label, Color = hover.Value ? DesignTokens.TextTitle : DesignTokens.TextBody, FontSize = 14, FontWeight = "600" })
                    ]
            });
        }
    }
}
