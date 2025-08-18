using System;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using static EchoUI.Core.Hooks;

namespace EchoUI.Core
{
    public static class ElementCoreName
    {
        public const string Container = "EchoUI-Container";
        public const string Text = "EchoUI-Text";
        public const string Input = "EchoUI-Input";
    }

    public record class NativeProps : Props
    {
        public string Type { get; init; } = null!;
        public ValueDictionary<string, object?>? Properties { get; init; }
    }

    /// <summary>
    /// 容器属性模块：整合了布局、外观、交互和子元素布局的完整设置。
    /// </summary>
    public record class ContainerProps : Props
    {
        // --- 布局 (尺寸与外边距) ---
        /// <summary>
        /// 元素的宽度。
        /// </summary>
        public Dimension? Width { get; init; } = Dimension.Percent(100);
        /// <summary>
        /// 元素的高度。
        /// </summary>
        public Dimension? Height { get; init; }
        /// <summary>
        /// 元素的最小宽度。
        /// </summary>
        public Dimension? MinWidth { get; init; }
        /// <summary>
        /// 元素的最小高度。
        /// </summary>
        public Dimension? MinHeight { get; init; }
        /// <summary>
        /// 元素的最大宽度。
        /// </summary>
        public Dimension? MaxWidth { get; init; }
        /// <summary>
        /// 元素的最大高度。
        /// </summary>
        public Dimension? MaxHeight { get; init; }
        /// <summary>
        /// 元素的外边距。
        /// </summary>
        public Spacing? Margin { get; init; }

        // --- 背景 ---
        /// <summary>
        /// 元素的背景颜色。
        /// </summary>
        public Color? BackgroundColor { get; init; }

        // --- 边框 ---
        /// <summary>
        /// 边框的样式。
        /// </summary>
        public BorderStyle? BorderStyle { get; init; }
        /// <summary>
        /// 边框的颜色。
        /// </summary>
        public Color? BorderColor { get; init; }
        /// <summary>
        /// 边框的宽度。
        /// </summary>
        public float? BorderWidth { get; init; }
        /// <summary>
        /// 边框的圆角半径。
        /// </summary>
        public float? BorderRadius { get; init; }

        // --- 动画 ---
        /// <summary>
        /// 定义当指定属性发生变化时应用的过渡动画。
        /// Key: 使用 nameof() 指定的属性名 (例如, nameof(BackgroundColor))。
        /// Value: 描述动画的 Transition 对象。
        /// </summary>
        public ValueDictionary<string, Transition>? Transitions { get; init; }

        // --- 通用交互与状态 ---
        /// <summary>
        /// 点击事件，参数描述了点击的鼠标按键。
        /// </summary>
        public Action<MouseButton>? OnClick { get; init; }

        /// <summary>
        /// 鼠标移动事件，参数为鼠标在控件内的坐标。
        /// </summary>
        public Action<Point>? OnMouseMove { get; init; }

        /// <summary>
        /// 鼠标进入元素区域时触发。
        /// </summary>
        public Action? OnMouseEnter { get; init; }
        /// <summary>
        /// 鼠标离开元素区域时触发。
        /// </summary>
        public Action? OnMouseLeave { get; init; }
        /// <summary>
        /// 在元素上按下鼠标按键时触发。
        /// </summary>
        public Action? OnMouseDown { get; init; }
        /// <summary>
        /// 在元素上释放鼠标按键时触发。
        /// </summary>
        public Action? OnMouseUp { get; init; }

        /// <summary>
        /// 键盘按下事件，参数为按键的虚拟码。
        /// </summary>
        public Action<int>? OnKeyDown { get; init; }

        /// <summary>
        /// 键盘抬起事件，参数为按键的虚拟码。
        /// </summary>
        public Action<int>? OnKeyUp { get; init; }

        // --- 子元素布局与内边距 ---
        /// <summary>
        /// 子元素的布局方向（垂直或水平）。
        /// </summary>
        public LayoutDirection? Direction { get; init; } = LayoutDirection.Vertical;
        /// <summary>
        /// 子元素在主轴上的对齐方式。
        /// </summary>
        public JustifyContent? JustifyContent { get; init; }
        /// <summary>
        /// 子元素在交叉轴上的对齐方式。
        /// </summary>
        public AlignItems? AlignItems { get; init; }
        /// <summary>
        /// 子元素之间的间距。
        /// </summary>
        public float? Gap { get; init; }
        /// <summary>
        /// 元素的内边距。
        /// </summary>
        public Spacing? Padding { get; init; }
    }

    /// <summary>
    /// 文本属性模块：负责文本内容和样式。
    /// </summary>
    public record class TextProps : Props
    {
        /// <summary>
        /// 显示的文本内容。
        /// </summary>
        public string Text { get; init; } = "";
        /// <summary>
        /// 字体家族的名称。
        /// </summary>
        public string? FontFamily { get; init; }
        /// <summary>
        /// 字体的大小。
        /// </summary>
        public float? FontSize { get; init; }
        /// <summary>
        /// 文本的颜色。
        /// </summary>
        public Color? Color { get; init; }
        /// <summary>
        /// 设置为 true 时，该文本将不会拦截鼠标事件，事件会“穿透”到下层控件。
        /// </summary>
        public bool MouseThrough { get; set; } = true;
    }

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
    }

    /// <summary>
    /// Tabs 组件的属性。
    /// </summary>
    public record class TabProps : Props
    {
        /// <summary>
        /// 每个 Tab 的标题。
        /// </summary>
        public IReadOnlyList<string> Titles { get; init; } = Array.Empty<string>();

        /// <summary>
        /// 一个根据索引生成 Tab 内容的函数。
        /// </summary>
        public Func<int, Element> Content { get; init; } = _ => Elements.Container(new ContainerProps());

        /// <summary>
        /// 初始选中的 Tab 索引。
        /// </summary>
        public int InitialIndex { get; init; } = 0;

        /// <summary>
        /// 当 Tab 切换时触发的回调。
        /// </summary>
        public Action<int>? OnTabChanged { get; init; }

        // --- Styling ---

        /// <summary>
        /// 激活状态 Tab 的背景颜色。
        /// </summary>
        public Color? ActiveTabBackgroundColor { get; init; }

        /// <summary>
        /// 非激活状态 Tab 的背景颜色。
        /// </summary>
        public Color? InactiveTabBackgroundColor { get; init; }

        /// <summary>
        /// 激活状态 Tab 的文本颜色。
        /// </summary>
        public Color? ActiveTabTextColor { get; init; }

        /// <summary>
        /// 非激活状态 Tab 的文本颜色。
        /// </summary>
        public Color? InactiveTabTextColor { get; init; }

        /// <summary>
        /// 切换动画的持续时间（毫秒）。
        /// </summary>
        public int AnimationDuration { get; init; } = 250; // ms
    }

    /// <summary>
    /// Switch (开关) 组件的属性。
    /// </summary>
    public record class SwitchProps : Props
    {
        /// <summary>
        /// 开关的初始状态。默认为 false (关)。
        /// </summary>
        public bool DefaultIsOn { get; init; } = false;

        /// <summary>
        /// 当开关状态因用户点击而改变时触发的回调，参数为新的状态。
        /// </summary>
        public Action<bool>? OnToggle { get; init; }

        /// <summary>
        /// “开”状态下的背景颜色。
        /// </summary>
        public Color? OnColor { get; init; }

        /// <summary>
        /// “关”状态下的背景颜色。
        /// </summary>
        public Color? OffColor { get; init; }

        /// <summary>
        /// 滑块的颜色。
        /// </summary>
        public Color? ThumbColor { get; init; }

        /// <summary>
        /// 组件的宽度。
        /// </summary>
        public Dimension? Width { get; init; }

        /// <summary>
        /// 组件的高度。
        /// </summary>
        public Dimension? Height { get; init; }

        /// <summary>
        /// 切换动画的持续时间（毫秒）。
        /// </summary>
        public int AnimationDuration { get; init; } = 150; // ms
    }

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
        public string Label { get; init; }

        /// <summary>
        /// 勾选标记的颜色。
        /// </summary>
        public Color? CheckColor { get; init; }

        /// <summary>
        /// 边框的颜色。
        /// </summary>
        public Color? BorderColor { get; init; }
    }

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

    /// <summary>
    /// Input (输入框) 组件的属性。
    /// Note: This is a renaming of the original TextBoxProps for clarity.
    /// </summary>
    public record class InputProps : Props
    {
        /// <summary>
        /// 输入框的当前值。
        /// </summary>
        public string Value { get; init; } = "";

        /// <summary>
        /// 当输入框的值改变时触发的回调。
        /// </summary>
        public Action<string>? OnValueChanged { get; init; }

        public Color? BackgroundColor { get; init; }
        public Color? TextColor { get; init; }
        public Color? BorderColor { get; init; }
        public Color? FocusedBorderColor { get; init; }
        public Spacing? Padding { get; init; }
    }

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
        // --- 组件函数 ---

        //public static Element Button(Func<string> action) => Button(new ButtonProps()
        //{
        //    Text = action.Invoke()
        //});
        //public static Element Button(Func<string> text, Action? onClick) => Button(new ButtonProps()
        //{
        //    Text = text.Invoke(),
        //    OnClick = onClick
        //});
        //public static Element Button(string text, Action? onClick) => Button(new ButtonProps()
        //{
        //    Text = text,
        //    OnClick = onClick
        //});
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

        /// <summary>
        /// A Tabs component with a simplified sliding animation.
        /// </summary>
        [Element(DefaultProperty = nameof(TabProps.Titles))]
        public static Element Tabs(TabProps props)
        {
            var (currentIndex, setCurrentIndex, _) = Hooks.State(props.InitialIndex);

            // The click handler is now much simpler.
            Action<int> selectTab = (newIndex) =>
            {
                if (newIndex == currentIndex.Value) return;

                setCurrentIndex(newIndex);
                props.OnTabChanged?.Invoke(newIndex);
            };

            // --- Tab Headers ---
            var tabHeaders = new List<Element>();
            for (var i = 0; i < props.Titles.Count; i++)
            {
                var index = i;
                var isActive = currentIndex.Value == index;
                tabHeaders.Add(
                    Container(new ContainerProps
                    {
                        Key = props.Titles[index],
                        Width = Dimension.Percent(100.0f / props.Titles.Count),
                        JustifyContent = JustifyContent.Center,
                        AlignItems = AlignItems.Center,
                        Padding = new Spacing(Dimension.Pixels(12), Dimension.Pixels(8)),
                        BackgroundColor = isActive
                            ? (props.ActiveTabBackgroundColor ?? Color.Gainsboro)
                            : props.InactiveTabBackgroundColor,
                        OnClick = _ => selectTab(index),
                        Children =
                        [
                            Text(new TextProps
                            {
                                Text = props.Titles[index],
                                Color = isActive
                                    ? (props.ActiveTabTextColor ?? Color.Black)
                                    : (props.InactiveTabTextColor ?? Color.Gray)
                            })
                        ]
                    })
                );
            }

            // --- Tab Content ---
            // 1. Create all panels.
            var allPanels = new List<Element>();
            for (int i = 0; i < props.Titles.Count; i++)
            {
                allPanels.Add(Container(new ContainerProps
                {
                    Key = props.Titles[i],
                    Children = [props.Content(i)],
                    Width = Dimension.Percent(currentIndex.Value == i ? 100 : 0),
                    Transitions = new ValueDictionary<string, Transition>(new Dictionary<string, Transition>
                    {
                        [nameof(ContainerProps.Width)] = new(props.AnimationDuration, Easing.EaseInOut)
                    }),
                }));
            }

            // 2. Place all panels into a single wide track.
            var contentTrack = Container(new ContainerProps
            {
                Direction = LayoutDirection.Horizontal,
                Children = allPanels
            });

            return Container(new ContainerProps
            {
                Key = props.Key,
                Direction = LayoutDirection.Vertical,
                Children =
                [
                    // Tab Header container
                    Container(new ContainerProps
                    {
                        Direction = LayoutDirection.Horizontal,
                        Width = Dimension.Percent(100),
                        BorderColor = Color.LightGray,
                        BorderWidth = 1,
                        BorderStyle = BorderStyle.Solid,
                        Children = tabHeaders
                    }),

                    // Content Viewport (acts as a mask for the sliding track)
                    Container(new ContainerProps
                    {
                        Direction= LayoutDirection.Horizontal,
                        Children = [contentTrack]
                    })
                ]
            });
        }

        /// <summary>
        /// 创建一个空元素，用于占位。
        /// </summary>
        public static Element Empty() => new(ElementCoreName.Container, new ContainerProps { Width = Dimension.ZeroPixels, Height = Dimension.ZeroPixels });

        /// <summary>
        /// Input (输入框) 组件。
        /// </summary>
        [Element]
        public static Element Input(InputProps props)
        {
            return new Element(ElementCoreName.Input, props);
        }

        /// <summary>
        /// Switch (开关) 组件，使用可配置曲线动画
        /// </summary>
        [Element]
        public static Element Switch(SwitchProps props)
        {
            var (isOn, setIsOn, _) = Hooks.State(props.DefaultIsOn);

            var widthPx = props.Width?.Unit == DimensionUnit.Pixels ? props.Width.Value.Value : 50f;
            var heightPx = props.Height?.Unit == DimensionUnit.Pixels ? props.Height.Value.Value : 26f;
            const float paddingPx = 3f;
            var thumbSizePx = heightPx - (2 * paddingPx);
            var slidableWidth = widthPx - (2 * paddingPx) - thumbSizePx;

            // 动画
            var trackTransitions = new ValueDictionary<string, Transition>(new Dictionary<string, Transition>
            {
                [nameof(ContainerProps.BackgroundColor)] = new(props.AnimationDuration, Easing.EaseInOut)
            });

            var thumbTransitions = new ValueDictionary<string, Transition>(new Dictionary<string, Transition>
            {
                [nameof(ContainerProps.Margin)] = new(props.AnimationDuration, Easing.EaseInOut)
            });

            return Container(new ContainerProps()
            {
                Direction = LayoutDirection.Horizontal,
                Key = props.Key,
                Width = Dimension.Pixels(widthPx),
                Height = Dimension.Pixels(heightPx),
                BackgroundColor = isOn.Value ? (props.OnColor ?? Color.Green) : (props.OffColor ?? Color.LightGray),
                Transitions = trackTransitions,
                BorderRadius = heightPx / 2,
                Padding = new Spacing(Dimension.Pixels(paddingPx)),
                JustifyContent = JustifyContent.Start,
                AlignItems = AlignItems.Center,
                OnClick = _ =>
                {
                    var newState = !isOn.Value;
                    setIsOn(newState);
                    props.OnToggle?.Invoke(newState);
                },
                Children =
                [
                    Container(new ContainerProps()
                    {
                        Width = Dimension.Pixels(thumbSizePx),
                        Height = Dimension.Pixels(thumbSizePx),
                        BackgroundColor = props.ThumbColor ?? Color.White,
                        BorderRadius = thumbSizePx / 2,
                        Margin = new Spacing(Dimension.Pixels(isOn.Value ? slidableWidth : 0f), Dimension.Pixels(0), Dimension.Pixels(isOn.Value ? 0f : slidableWidth), Dimension.Pixels(0)),
                        Transitions = thumbTransitions
                    })
                ]
            });
        }

        /// <summary>
        /// CheckBox (复选框) 组件。
        /// </summary>
        [Element(DefaultProperty = nameof(CheckBoxProps.Label))]
        public static Element CheckBox(CheckBoxProps props)
        {
            var (check, _, updateCheck) = Hooks.State(props.IsChecked);

            return Container(new ContainerProps
            {
                Key = props.Key,
                Direction = LayoutDirection.Horizontal,
                AlignItems = AlignItems.Center,
                Gap = 8,
                OnClick = _ =>
                {
                    updateCheck(v => !v);
                    props.OnToggle?.Invoke(check.Value);
                },
                Children =
                [
                    Container(new ContainerProps
                    {
                        Width = Dimension.Pixels(20),
                        Height = Dimension.Pixels(20),
                        BorderWidth = 2,
                        BorderStyle = BorderStyle.Solid,
                        BorderColor = props.BorderColor ?? Color.Gray,
                        JustifyContent = JustifyContent.Center,
                        AlignItems = AlignItems.Center,
                        Children =
                        [
                            check.Value
                                ? Text(new TextProps
                                {
                                    Text = "✓",
                                    FontSize = 16,
                                    Color = props.CheckColor ?? Color.Black
                                })
                                : Empty()
                        ]
                    }),
                    string.IsNullOrEmpty(props.Label)
                        ? Empty()
                        : Text(new TextProps { Text = props.Label })
                    ]
            });
        }

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

                radioItems.Add(Container(new ContainerProps
                {
                    Key = props.Options[index],
                    Width = Dimension.Percent(100.0f / props.Options.Count),
                    Direction = LayoutDirection.Horizontal,
                    AlignItems = AlignItems.Center,
                    Gap = 8,
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
                            BorderColor = props.BorderColor ?? Color.Gray,
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
                                        BackgroundColor = props.SelectedColor ?? Color.Black
                                    })
                                    : Empty()
                            ]
                        }),
                        Text(new TextProps { Text = props.Options[index] })
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

        /// <summary>
        /// ComboBox (下拉选择框) 组件。
        /// </summary>
        [Element(DefaultProperty = nameof(ComboBoxProps.Options))]
        public static Element ComboBox(ComboBoxProps props)
        {
            var (isOpen, setIsOpen, _) = Hooks.State(false);

            var (selectIndex, setSelectIndex, _) = State(props.SelectedIndex);

            var selectedOptionText = (selectIndex.Value >= 0 && selectIndex.Value < props.Options.Count)
                ? props.Options[selectIndex.Value]
                : "Select...";

            var (moveIndex, setMoveIndex, _) = State(props.SelectedIndex);
            // Build the dropdown items list when open
            var dropdownItems = new List<Element>();
            if (isOpen.Value)
            {
                for (var i = 0; i < props.Options.Count; i++)
                {
                    var index = i;
                    dropdownItems.Add(Container(new ContainerProps
                    {
                        Key = props.Options[index],
                        Width = Dimension.Percent(100),
                        Height = Dimension.Pixels(35),
                        JustifyContent = JustifyContent.Center,
                        Padding = new Spacing(Dimension.Pixels(8), Dimension.Pixels(6)),
                        BackgroundColor = moveIndex.Value == index ? Color.Gray : Color.White,
                        OnClick = _ =>
                        {
                            setSelectIndex(index);
                            props.OnSelectionChanged?.Invoke(index);
                            setIsOpen(false); // Close after selection
                        },
                        OnMouseMove = _ => setMoveIndex(index),
                        Children = [Text(new TextProps { Text = props.Options[index], Color = props.TextColor ?? Color.Black })]
                    }));
                }
            }

            return Container(new ContainerProps // Main wrapper
            {
                Key = props.Key,
                Direction = LayoutDirection.Vertical,
                Children =
                [
                    // Display box
                    Container(new ContainerProps
                    {
                        Width = Dimension.Percent(100),
                        Direction = LayoutDirection.Horizontal,
                        JustifyContent = JustifyContent.SpaceBetween,
                        AlignItems = AlignItems.Center,
                        Padding = new Spacing(Dimension.Pixels(8), Dimension.Pixels(6)),
                        BackgroundColor = props.BackgroundColor ?? Color.White,
                        BorderWidth = 1,
                        BorderStyle = BorderStyle.Solid,
                        BorderColor = props.BorderColor ?? Color.Gray,
                        OnClick = _ => setIsOpen(!isOpen.Value),
                        Children =
                        [
                            Text(new TextProps { Text = selectedOptionText, Color = props.TextColor ?? Color.Black }),
                            Text(new TextProps { Text = "▼", FontSize = 10, Color = props.TextColor ?? Color.Gray })
                        ]
                    }),

                    // Dropdown list (rendered below if open)
                    Container(new ContainerProps
                    {
                        Width = Dimension.Percent(100),
                        Height = isOpen.Value ? Dimension.Pixels(35 * props.Options.Count) : Dimension.ZeroPixels,
                        Transitions = [
                            [nameof(ContainerProps.Height), new Transition(150, Easing.EaseInOut)]
                        ],
                        Direction = LayoutDirection.Vertical,
                        BackgroundColor = props.DropdownBackgroundColor ?? Color.White,
                        BorderWidth = isOpen.Value ? 1 : 0,
                        BorderStyle = BorderStyle.Solid,
                        BorderColor = props.BorderColor ?? Color.Gray,
                        Children = dropdownItems
                    })
                ]
            });
        }

        // 创建组件Element的工厂
        public static Element Create(Component component, Props props) => new(component, props);
        public static Element Create(AsyncComponent asyncComponent, Props props) => new(asyncComponent, props);

        // 创建备忘录组件的工厂
        public static Element Memo(Component component, Props props) => new(component, props with { AreEqual = (old, @new) => old.Equals(@new) });
        public static Element Memo(AsyncComponent asyncComponent, Props props) => new(asyncComponent, props with { AreEqual = (old, @new) => old.Equals(@new) });

        // 创建原生元素Element的工厂
        [Element(DefaultProperty = nameof(ContainerProps.Children))]
        public static Element Container(ContainerProps props) => new(ElementCoreName.Container, props);

        [Element(DefaultProperty = nameof(TextProps.Text))]
        public static Element Text(TextProps props) => new(ElementCoreName.Text, props);

        [Element(DefaultProperty = nameof(NativeProps.Type))]
        public static Element Native(NativeProps props) => new(props.Type, props);
    }
}