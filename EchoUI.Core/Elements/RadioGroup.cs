using static EchoUI.Core.Hooks;

namespace EchoUI.Core
{
    /// <summary>
    /// RadioGroup (单选框组) 组件的属性。
    /// </summary>
    public record class RadioGroupProps : Props
    {
        /// <summary>
        /// 所有单选项的文本标签列表。
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

        /// <summary>
        /// 选项的布局方向。
        /// </summary>
        public LayoutDirection Direction { get; init; } = LayoutDirection.Horizontal;

        /// <summary>
        /// 选中状态时圆点的颜色。
        /// </summary>
        public Color? SelectedColor { get; init; }

        /// <summary>
        /// 边框的颜色。
        /// </summary>
        public Color? BorderColor { get; init; }
    }

    public partial class Elements
    {
        /// <summary>
        /// RadioGroup (单选框组) 组件。
        /// </summary>
        [Element(DefaultProperty = nameof(RadioGroupProps.Options))]
        public static Element RadioGroup(RadioGroupProps props)
        {
            var (selectIndex, setSelectIndex, _) = State(props.SelectedIndex);
            var radioItems = new List<Element>();
            for (var i = 0; i < props.Options.Count; i++)
            {
                var index = i; // Capture loop variable
                var isSelected = selectIndex.Value == index;
                var selectedColor = props.SelectedColor ?? DesignTokens.Primary;

                radioItems.Add(Container(new ContainerProps
                {
                    Key = props.Options[index],
                    Width = Dimension.Percent(100.0f / props.Options.Count),
                    Direction = LayoutDirection.Horizontal,
                    AlignItems = AlignItems.Center,
                    Gap = 8,
                    Cursor = "pointer",
                    Padding = new Spacing(Dimension.Pixels(8), Dimension.Pixels(4)),
                    BackgroundColor = isSelected ? DesignTokens.PrimaryBg : Color.Transparent,
                    BorderRadius = DesignTokens.RadiusPill,
                    OnClick = _ =>
                    {
                        setSelectIndex(index);
                        props.OnSelectionChanged?.Invoke(index);
                    },
                    Children =
                    [
                        // Outer circle
                        Container(new ContainerProps
                        {
                            Width = Dimension.Pixels(20),
                            Height = Dimension.Pixels(20),
                            BorderRadius = 10,
                            BorderWidth = 2,
                            BorderStyle = BorderStyle.Solid,
                            BackgroundColor = DesignTokens.BgContent,
                            BorderColor = isSelected ? selectedColor : props.BorderColor ?? DesignTokens.Border,
                            JustifyContent = JustifyContent.Center,
                            AlignItems = AlignItems.Center,
                            Children =
                            [
                                // Inner dot (if selected)
                                isSelected
                                    ? Container(new ContainerProps
                                    {
                                        Width = Dimension.Pixels(10),
                                        Height = Dimension.Pixels(10),
                                        BorderRadius = 5,
                                        BackgroundColor = selectedColor
                                    })
                                    : Empty()
                            ]
                        }),
                        Text(new TextProps { Text = props.Options[index], Color = isSelected ? DesignTokens.TextTitle : DesignTokens.TextBody, FontSize = 14, FontWeight = isSelected ? "700" : "500" })
                    ]
                }));
            }

            return Container(new ContainerProps
            {
                Key = props.Key,
                Direction = props.Direction,
                AlignItems = AlignItems.Start,
                Gap = 10,
                Children = radioItems
            });
        }
    }
}
