using static EchoUI.Core.Hooks;

namespace EchoUI.Core
{
    /// <summary>
    /// ComboBox (下拉选择框) 组件的属性。
    /// </summary>
    public record class ComboBoxProps : Props
    {
        /// <summary>
        /// 所有可选项的文本列表。
        /// </summary>
        public IReadOnlyList<string> Options { get; init; } = Array.Empty<string>();

        /// <summary>
        /// 当前选中的选项的索引。
        /// </summary>
        public int SelectedIndex { get; init; } = 0;

        /// <summary>
        /// 当选项改变时触发的回调。
        /// </summary>
        public Action<int>? OnSelectionChanged { get; init; }

        public Color? BackgroundColor { get; init; }
        public Color? TextColor { get; init; }
        public Color? BorderColor { get; init; }

        /// <summary>
        /// 下拉菜单的背景颜色。
        /// </summary>
        public Color? DropdownBackgroundColor { get; init; }
    }

    public partial class Elements
    {
        /// <summary>
        /// ComboBox (下拉选择框) 组件。
        /// </summary>
        [Element(DefaultProperty = nameof(ComboBoxProps.Options))]
        public static Element ComboBox(ComboBoxProps props)
        {
            var (isOpen, setIsOpen, _) = Hooks.State(false);
            var (selectIndex, setSelectIndex, _) = State(props.SelectedIndex);
            var (hoverIndex, setHoverIndex, _) = State(-1);

            var backgroundColor = props.BackgroundColor ?? DesignTokens.BgContent;
            var textColor = props.TextColor ?? DesignTokens.TextBody;
            var borderColor = props.BorderColor ?? DesignTokens.Border;
            var dropdownBackgroundColor = props.DropdownBackgroundColor ?? DesignTokens.BgContent;
            var accentColor = DesignTokens.Primary;
            var hoverBackgroundColor = DesignTokens.PrimaryBg;
            var selectedBackgroundColor = DesignTokens.PrimaryBg;
            var mutedTextColor = DesignTokens.TextMuted;

            var selectedOptionText = (selectIndex.Value >= 0 && selectIndex.Value < props.Options.Count)
                ? props.Options[selectIndex.Value]
                : "Select...";

            var dropdownItems = new List<Element>();
            if (isOpen.Value)
            {
                for (var i = 0; i < props.Options.Count; i++)
                {
                    var index = i;
                    var isSelected = selectIndex.Value == index;
                    var isHovered = hoverIndex.Value == index;

                    dropdownItems.Add(Container(new ContainerProps
                    {
                        Key = props.Options[index],
                        Width = Dimension.Percent(100),
                        Height = Dimension.Pixels(38),
                        Direction = LayoutDirection.Horizontal,
                        JustifyContent = JustifyContent.SpaceBetween,
                        AlignItems = AlignItems.Center,
                        Padding = new Spacing(Dimension.Pixels(12), Dimension.Pixels(8)),
                        BackgroundColor = isHovered ? hoverBackgroundColor : (isSelected ? selectedBackgroundColor : Color.Transparent),
                        BorderRadius = 14,
                        OnMouseEnter = () => setHoverIndex(index),
                        OnMouseLeave = () =>
                        {
                            if (hoverIndex.Value == index)
                                setHoverIndex(-1);
                        },
                        OnClick = _ =>
                        {
                            setSelectIndex(index);
                            setHoverIndex(-1);
                            props.OnSelectionChanged?.Invoke(index);
                            setIsOpen(false);
                        },
                        Children =
                        [
                            Text(new TextProps
                            {
                                Text = props.Options[index],
                                Color = isSelected ? accentColor : textColor,
                                FontSize = 14,
                                FontWeight = isSelected ? "700" : "500"
                            }),
                            Text(new TextProps
                            {
                                Text = isSelected ? "✓" : string.Empty,
                                Color = accentColor,
                                FontSize = 11,
                                MouseThrough = true
                            })
                        ]
                    }));
                }
            }

            var visibleOptionCount = Math.Min(props.Options.Count, 6);
            var shouldScroll = props.Options.Count > visibleOptionCount;
            var dropdownHeight = isOpen.Value
                ? Dimension.Pixels(visibleOptionCount * 36 + Math.Max(0, visibleOptionCount - 1) * 4 + 8 + 2)
                : Dimension.ZeroPixels;

            return Container(new ContainerProps
            {
                Key = props.Key,
                Direction = LayoutDirection.Vertical,
                Overflow = Overflow.Visible,
                OnBlur = () =>
                {
                    setIsOpen(false);
                    setHoverIndex(-1);
                },
                Children =
                [
                    Container(new ContainerProps
                    {
                        Width = Dimension.Percent(100),
                        Height = Dimension.Pixels(40),
                        Direction = LayoutDirection.Horizontal,
                        JustifyContent = JustifyContent.SpaceBetween,
                        AlignItems = AlignItems.Center,
                        Padding = new Spacing(Dimension.Pixels(18), Dimension.Pixels(0)),
                        BackgroundColor = backgroundColor,
                        BorderWidth = 2.5f,
                        BorderStyle = BorderStyle.Solid,
                        BorderColor = isOpen.Value ? DesignTokens.BorderFocus : borderColor,
                        BorderRadius = DesignTokens.RadiusPill,
                        Shadow = new BoxShadow(isOpen.Value ? Color.FromHex("#e0b800") : DesignTokens.ShadowInput, 3),
                        Transitions = new ValueDictionary<string, Transition>(new Dictionary<string, Transition>
                        {
                            [nameof(ContainerProps.BorderColor)] = new(180, Easing.EaseOut),
                            [nameof(ContainerProps.Shadow)] = new(180, Easing.EaseOut),
                        }),
                        OnClick = _ =>
                        {
                            setHoverIndex(-1);
                            setIsOpen(!isOpen.Value);
                        },
                        Children =
                        [
                            Text(new TextProps
                            {
                                Text = selectedOptionText,
                                Color = textColor,
                                FontSize = 14,
                                FontWeight = "600",
                                NoWrap = true
                            }),
                            Text(new TextProps
                            {
                                Text = isOpen.Value ? "▲" : "▼",
                                FontSize = 10,
                                Color = isOpen.Value ? accentColor : mutedTextColor,
                                MouseThrough = true
                            })
                        ]
                    }),
                    Container(new ContainerProps
                    {
                        Float = true,
                        Width = Dimension.Percent(100),
                        Margin = new Spacing(Dimension.ZeroPixels, Dimension.Pixels(4), Dimension.ZeroPixels, Dimension.ZeroPixels),
                        Children =
                        [
                            Container(new ContainerProps
                            {
                                Width = Dimension.Percent(100),
                                Height = dropdownHeight,
                                Padding = new Spacing(Dimension.Pixels(4)),
                                Direction = LayoutDirection.Vertical,
                                Gap = 4,
                                Overflow = Overflow.Auto,
                                BackgroundColor = dropdownBackgroundColor,
                                BorderWidth = isOpen.Value ? 2 : 0,
                                BorderStyle = BorderStyle.Solid,
                                BorderColor = borderColor,
                                BorderRadius = DesignTokens.RadiusBase,
                                Shadow = isOpen.Value ? new BoxShadow(DesignTokens.ShadowInput, 5, 12) : BoxShadow.None,
                                Transitions =
                                [
                                    [nameof(ContainerProps.Height), new Transition(150, Easing.EaseInOut)]
                                ],
                                Children = dropdownItems
                            })
                        ]
                    })
                ]
            });
        }
    }
}
