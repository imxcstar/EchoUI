using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Styling;
using EchoUI.Core;

namespace EchoUI.Render.Avalonia;

/// <summary>
/// Avalonia 渲染器，实现 IRenderer 接口。
/// 将 EchoUI 元素映射到 Avalonia 原生控件。
/// </summary>
public class AvaloniaRenderer : IRenderer
{
    private readonly Panel _rootPanel;

    /// <summary>
    /// 存储每个控件上绑定的事件处理器，用于移除旧事件后重新绑定。
    /// </summary>
    private readonly Dictionary<Control, EventHandlerStore> _eventStores = [];

    public AvaloniaRenderer(Panel rootPanel)
    {
        _rootPanel = rootPanel;
    }

    public object CreateElement(string type)
    {
        Control control = type switch
        {
            ElementCoreName.Container => CreateContainer(),
            ElementCoreName.Text => CreateTextBlock(),
            ElementCoreName.Input => CreateTextBox(),
            _ => CreateNativeElement(type)
        };

        return control;
    }

    public void PatchProperties(object nativeElement, Props newProps, PropertyPatch patch)
    {
        var control = (Control)nativeElement;

        // 始终同步事件处理器
        UpdateEventHandlers(control, newProps);

        if (patch.UpdatedProperties != null)
        {
            foreach (var (propName, propValue) in patch.UpdatedProperties)
            {
                ApplyProperty(control, newProps, propName, propValue);
            }
        }

        // 应用默认值
        switch (newProps)
        {
            case ContainerProps p:
                ApplyContainerDefaults(control, p);
                break;
            case TextProps:
                ApplyTextDefaults(control);
                break;
            case InputProps:
                ApplyInputDefaults(control);
                break;
        }
    }

    public void AddChild(object parent, object child, int index)
    {
        var parentPanel = GetParentPanel(parent);
        var childControl = (Control)child;

        if (index >= 0 && index < parentPanel.Children.Count)
            parentPanel.Children.Insert(index, childControl);
        else
            parentPanel.Children.Add(childControl);
    }

    public void RemoveChild(object parent, object child)
    {
        var parentPanel = GetParentPanel(parent);
        var childControl = (Control)child;
        parentPanel.Children.Remove(childControl);

        // 清理事件存储
        CleanupEventHandlers(childControl);
    }

    public void MoveChild(object parent, object child, int newIndex)
    {
        var parentPanel = GetParentPanel(parent);
        var childControl = (Control)child;
        parentPanel.Children.Remove(childControl);

        if (newIndex >= 0 && newIndex < parentPanel.Children.Count)
            parentPanel.Children.Insert(newIndex, childControl);
        else
            parentPanel.Children.Add(childControl);
    }

    public IUpdateScheduler GetScheduler(object rootContainer) => new AvaloniaUpdateScheduler();

    // --- 元素创建 ---

    private static Border CreateContainer()
    {
        var panel = new EchoUIPanel();
        var border = new Border
        {
            Child = panel,
            // 确保 Border 能接收指针事件（即使背景透明）
            Background = global::Avalonia.Media.Brushes.Transparent,
        };
        return border;
    }

    private static TextBlock CreateTextBlock()
    {
        return new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            IsHitTestVisible = false // MouseThrough 默认 true
        };
    }

    private static TextBox CreateTextBox()
    {
        var tb = new TextBox
        {
            AcceptsReturn = false,
            BorderThickness = new Thickness(0),
            // Padding = new Thickness(0), // Use default padding for correct height
            MinHeight = 0,
            MinWidth = 0,
            Background = global::Avalonia.Media.Brushes.Transparent,
            VerticalContentAlignment = VerticalAlignment.Center
        };

        // Remove the default FocusAdorner to prevent the focus ring
        tb.SetValue(TextBox.FocusAdornerProperty, null);

        // Attempt to remove focus visual border by explicit style override
        tb.Styles.Add(new Style(x => x.OfType<TextBox>().Class(":focus"))
        {
            Setters =
            {
                new Setter(TextBox.BorderThicknessProperty, new Thickness(0)),
                new Setter(TextBox.BackgroundProperty, global::Avalonia.Media.Brushes.Transparent)
            }
        });

        return tb;
    }

    private static Border CreateNativeElement(string type)
    {
        var panel = new EchoUIPanel();
        var border = new Border
        {
            Child = panel,
            Background = global::Avalonia.Media.Brushes.Transparent,
        };
        return border;
    }

    // --- 获取父面板 ---

    private Panel GetParentPanel(object parent)
    {
        if (parent is string)
            return _rootPanel;

        if (parent is Border border && border.Child is Panel panel)
            return panel;

        if (parent is Panel p)
            return p;

        return _rootPanel;
    }

    // --- 属性应用 ---

    private void ApplyProperty(Control control, Props props, string propName, object? propValue)
    {
        switch (props)
        {
            case ContainerProps:
                ApplyContainerProperty(control, propName, propValue);
                break;
            case TextProps:
                ApplyTextProperty(control, propName, propValue);
                break;
            case InputProps:
                ApplyInputProperty(control, propName, propValue);
                break;
            case NativeProps nativeProps:
                ApplyNativeProperty(control, nativeProps, propName, propValue);
                break;
        }
    }

    private void ApplyContainerProperty(Control control, string propName, object? propValue)
    {
        var border = (Border)control;
        var panel = (EchoUIPanel)border.Child!;

        switch (propName)
        {
            // --- 尺寸 ---
            // 对于 Pixels 直接设置 Width/Height，对于 Percent/ViewportHeight 存储到附加属性由布局引擎解析
            case nameof(ContainerProps.Width):
                var wDim = propValue as Dimension?;
                EchoUIPanel.SetEchoWidth(border, wDim);
                border.Width = ResolvePixelDimension(wDim) ?? double.NaN;
                break;
            case nameof(ContainerProps.Height):
                var hDim = propValue as Dimension?;
                EchoUIPanel.SetEchoHeight(border, hDim);
                border.Height = ResolvePixelDimension(hDim) ?? double.NaN;
                break;
            case nameof(ContainerProps.MinWidth):
                border.MinWidth = ResolvePixelDimension(propValue as Dimension?) ?? 0;
                break;
            case nameof(ContainerProps.MinHeight):
                border.MinHeight = ResolvePixelDimension(propValue as Dimension?) ?? 0;
                break;
            case nameof(ContainerProps.MaxWidth):
                border.MaxWidth = ResolvePixelDimension(propValue as Dimension?) ?? double.PositiveInfinity;
                break;
            case nameof(ContainerProps.MaxHeight):
                border.MaxHeight = ResolvePixelDimension(propValue as Dimension?) ?? double.PositiveInfinity;
                break;

            // --- 间距 ---
            case nameof(ContainerProps.Margin):
                border.Margin = ToThickness(propValue as Spacing?);
                break;
            case nameof(ContainerProps.Padding):
                border.Padding = ToThickness(propValue as Spacing?);
                break;

            // --- Flex 布局 ---
            case nameof(ContainerProps.Direction):
                panel.Direction = propValue is LayoutDirection dir ? dir : LayoutDirection.Vertical;
                break;
            case nameof(ContainerProps.JustifyContent):
                panel.JustifyContent = propValue is JustifyContent jc ? jc : JustifyContent.Start;
                break;
            case nameof(ContainerProps.AlignItems):
                panel.AlignItems = propValue is AlignItems ai ? ai : AlignItems.Start;
                break;
            case nameof(ContainerProps.Gap):
                panel.Gap = propValue is float gap ? gap : 0;
                break;
            case nameof(ContainerProps.FlexGrow):
                EchoUIPanel.SetFlexGrow(border, propValue is float fg ? fg : 0);
                break;
            case nameof(ContainerProps.FlexShrink):
                EchoUIPanel.SetFlexShrink(border, propValue is float fs ? fs : 0);
                break;
            case nameof(ContainerProps.Float):
                EchoUIPanel.SetIsFloat(border, propValue is true);
                if (propValue is true)
                {
                    // Float 容器：EchoUIPanel 负责布局时不占用空间
                    border.ClipToBounds = false;
                    border.ZIndex = 1000;
                    // 内部 panel 也不裁剪
                    panel.ClipToBounds = false;
                }
                else
                {
                    border.ZIndex = 0;
                }
                break;
            case nameof(ContainerProps.Overflow):
                var overflow = propValue is Overflow ov ? ov : Overflow.Visible;
                if (overflow == Overflow.Visible)
                {
                    border.ClipToBounds = false;
                    panel.ClipToBounds = false;
                }
                else
                {
                    border.ClipToBounds = true;
                    panel.ClipToBounds = true;
                }
                break;

            // --- 外观 ---
            case nameof(ContainerProps.BackgroundColor):
                var bgBrush = ToBrush(propValue as Core.Color?);
                // 如果设置了背景色就用它，否则保持透明（用于事件命中测试）
                border.Background = bgBrush ?? global::Avalonia.Media.Brushes.Transparent;
                break;
            case nameof(ContainerProps.BorderColor):
                border.BorderBrush = ToBrush(propValue as Core.Color?);
                break;
            case nameof(ContainerProps.BorderWidth):
                var bw = propValue is float bwf ? bwf : 0;
                border.BorderThickness = new Thickness(bw);
                break;
            case nameof(ContainerProps.BorderRadius):
                var br = propValue is float brf ? brf : 0;
                border.CornerRadius = new CornerRadius(br);
                break;
            case nameof(ContainerProps.BorderStyle):
                if (propValue is Core.BorderStyle bs && bs == Core.BorderStyle.None)
                {
                    border.BorderThickness = new Thickness(0);
                }
                break;

            // --- 动画 (Transitions) ---
            case nameof(ContainerProps.Transitions):
                ApplyTransitions(border, propValue as ValueDictionary<string, Transition>?);
                break;
        }
    }

    private void ApplyTextProperty(Control control, string propName, object? propValue)
    {
        var textBlock = (TextBlock)control;

        switch (propName)
        {
            case nameof(TextProps.Text):
                textBlock.Text = propValue as string ?? "";
                break;
            case nameof(TextProps.FontFamily):
                if (propValue is string ff && !string.IsNullOrEmpty(ff))
                    textBlock.FontFamily = new FontFamily(ff);
                break;
            case nameof(TextProps.FontSize):
                textBlock.FontSize = propValue is float fs ? fs : 14;
                break;
            case nameof(TextProps.Color):
                textBlock.Foreground = ToBrush(propValue as Core.Color?);
                break;
            case nameof(TextProps.FontWeight):
                textBlock.FontWeight = ToFontWeight(propValue as string);
                break;
            case nameof(TextProps.MouseThrough):
                textBlock.IsHitTestVisible = propValue is false;
                break;
        }
    }

    private void ApplyInputProperty(Control control, string propName, object? propValue)
    {
        var textBox = (TextBox)control;

        switch (propName)
        {
            case nameof(InputProps.Value):
                var newValue = propValue as string ?? "";
                if (textBox.Text != newValue)
                    textBox.Text = newValue;
                break;
            case nameof(InputProps.BackgroundColor):
                textBox.Background = ToBrush(propValue as Core.Color?);
                break;
            case nameof(InputProps.TextColor):
                textBox.Foreground = ToBrush(propValue as Core.Color?);
                break;
            case nameof(InputProps.BorderColor):
                textBox.BorderBrush = ToBrush(propValue as Core.Color?);
                break;
            case nameof(InputProps.Padding):
                textBox.Padding = ToThickness(propValue as Spacing?);
                break;
        }
    }

    private void ApplyNativeProperty(Control control, NativeProps nativeProps, string propName, object? propValue)
    {
        if (propValue is Delegate) return;

        if (nativeProps.Type == "img" && control is Border imgBorder)
        {
            if (propName == "src" && propValue is string src)
            {
                LoadImage(imgBorder, src);
            }
        }
    }

    // --- 默认值应用 ---

    private void ApplyContainerDefaults(Control control, ContainerProps p)
    {
        var border = (Border)control;
        var panel = (EchoUIPanel)border.Child!;

        panel.Direction = p.Direction ?? LayoutDirection.Vertical;
        EchoUIPanel.SetFlexShrink(border, p.FlexShrink ?? 0);
        EchoUIPanel.SetFlexGrow(border, p.FlexGrow ?? 0);
    }

    private void ApplyTextDefaults(Control control)
    {
        if (control is TextBlock tb)
        {
            tb.IsHitTestVisible = false;
        }
    }

    private void ApplyInputDefaults(Control control)
    {
        if (control is TextBox tb)
        {
            tb.HorizontalAlignment = HorizontalAlignment.Stretch;
            tb.VerticalAlignment = VerticalAlignment.Stretch;
        }
    }

    // --- 事件处理 ---

    private void UpdateEventHandlers(Control control, Props newProps)
    {
        if (!_eventStores.TryGetValue(control, out var store))
        {
            store = new EventHandlerStore();
            _eventStores[control] = store;
        }

        // 先移除旧的事件处理器
        DetachEvents(control, store);

        switch (newProps)
        {
            case ContainerProps p:
                AttachContainerEvents(control, store, p);
                break;
            case InputProps ip:
                AttachInputEvents(control, store, ip);
                break;
            case NativeProps np when np.Properties != null:
                AttachNativeEvents(control, store, np);
                break;
        }
    }

    private void AttachContainerEvents(Control control, EventHandlerStore store, ContainerProps p)
    {
        // OnClick 和 OnMouseDown 都用 PointerPressed，需要合并处理
        if (p.OnClick != null || p.OnMouseDown != null)
        {
            var handler = new EventHandler<PointerPressedEventArgs>((s, e) =>
            {
                // Bring to front on interaction to ensure dropdowns/popups render on top of siblings
                BringToFront(control);

                if (p.OnClick != null)
                {
                    var button = e.GetCurrentPoint(control).Properties.PointerUpdateKind switch
                    {
                        PointerUpdateKind.RightButtonPressed => Core.MouseButton.Right,
                        PointerUpdateKind.MiddleButtonPressed => Core.MouseButton.Middle,
                        _ => Core.MouseButton.Left
                    };
                    p.OnClick(button);
                }
                p.OnMouseDown?.Invoke();
                e.Handled = true; // 防止事件冒泡到父元素
            });
            control.PointerPressed += handler;
            store.PointerPressed = handler;
        }

        if (p.OnMouseMove != null)
        {
            var handler = new EventHandler<PointerEventArgs>((s, e) =>
            {
                var pos = e.GetPosition(control);
                p.OnMouseMove(new Core.Point((int)pos.X, (int)pos.Y));
            });
            control.PointerMoved += handler;
            store.PointerMoved = handler;
        }

        if (p.OnMouseEnter != null)
        {
            var handler = new EventHandler<PointerEventArgs>((s, e) => p.OnMouseEnter());
            control.PointerEntered += handler;
            store.PointerEntered = handler;
        }

        if (p.OnMouseLeave != null)
        {
            var handler = new EventHandler<PointerEventArgs>((s, e) => p.OnMouseLeave());
            control.PointerExited += handler;
            store.PointerExited = handler;
        }

        if (p.OnMouseUp != null)
        {
            var handler = new EventHandler<PointerReleasedEventArgs>((s, e) =>
            {
                p.OnMouseUp();
                e.Handled = true;
            });
            control.PointerReleased += handler;
            store.PointerReleased = handler;
        }

        if (p.OnKeyDown != null)
        {
            var handler = new EventHandler<KeyEventArgs>((s, e) => p.OnKeyDown((int)e.Key));
            control.KeyDown += handler;
            store.KeyDown = handler;
        }

        if (p.OnKeyUp != null)
        {
            var handler = new EventHandler<KeyEventArgs>((s, e) => p.OnKeyUp((int)e.Key));
            control.KeyUp += handler;
            store.KeyUp = handler;
        }
    }

    private void AttachInputEvents(Control control, EventHandlerStore store, InputProps ip)
    {
        if (ip.OnValueChanged != null && control is TextBox textBox)
        {
            var handler = new EventHandler<TextChangedEventArgs>((s, e) =>
            {
                ip.OnValueChanged(textBox.Text ?? "");
            });
            textBox.TextChanged += handler;
            store.TextChanged = handler;
        }
    }

    private void AttachNativeEvents(Control control, EventHandlerStore store, NativeProps np)
    {
        if (np.Properties == null) return;
        foreach (var item in np.Properties.Value.Data)
        {
            if (item.Value is Action<Core.MouseButton> clickHandler)
            {
                var handler = new EventHandler<PointerPressedEventArgs>((s, e) =>
                {
                    var button = e.GetCurrentPoint(control).Properties.PointerUpdateKind switch
                    {
                        PointerUpdateKind.RightButtonPressed => Core.MouseButton.Right,
                        PointerUpdateKind.MiddleButtonPressed => Core.MouseButton.Middle,
                        _ => Core.MouseButton.Left
                    };
                    clickHandler(button);
                    e.Handled = true;
                });
                control.PointerPressed += handler;
                store.PointerPressed = handler;
            }
            else if (item.Value is Action action && item.Key == "click")
            {
                var handler = new EventHandler<PointerPressedEventArgs>((s, e) =>
                {
                    action();
                    e.Handled = true;
                });
                control.PointerPressed += handler;
                store.PointerPressed = handler;
            }
        }
    }

    private void BringToFront(Control control)
    {
        var current = control;
        while (current != null && current != _rootPanel)
        {
            if (current.Parent is Panel parent)
            {
                int maxZ = 0;
                foreach (var child in parent.Children)
                {
                    if (child.ZIndex > maxZ) maxZ = child.ZIndex;
                }
                if (current.ZIndex <= maxZ)
                    current.ZIndex = maxZ + 1;
            }
            current = current.Parent as Control;
        }
    }

    private void DetachEvents(Control control, EventHandlerStore store)
    {
        if (store.PointerPressed != null)
        {
            control.PointerPressed -= store.PointerPressed;
            store.PointerPressed = null;
        }
        if (store.PointerReleased != null)
        {
            control.PointerReleased -= store.PointerReleased;
            store.PointerReleased = null;
        }
        if (store.PointerMoved != null)
        {
            control.PointerMoved -= store.PointerMoved;
            store.PointerMoved = null;
        }
        if (store.PointerEntered != null)
        {
            control.PointerEntered -= store.PointerEntered;
            store.PointerEntered = null;
        }
        if (store.PointerExited != null)
        {
            control.PointerExited -= store.PointerExited;
            store.PointerExited = null;
        }
        if (store.KeyDown != null)
        {
            control.KeyDown -= store.KeyDown;
            store.KeyDown = null;
        }
        if (store.KeyUp != null)
        {
            control.KeyUp -= store.KeyUp;
            store.KeyUp = null;
        }
        if (store.TextChanged != null && control is TextBox textBox)
        {
            textBox.TextChanged -= store.TextChanged;
            store.TextChanged = null;
        }
    }

    private void CleanupEventHandlers(Control control)
    {
        if (_eventStores.TryGetValue(control, out var store))
        {
            DetachEvents(control, store);
            _eventStores.Remove(control);
        }

        if (control is Border border && border.Child is Panel panel)
        {
            foreach (var child in panel.Children)
            {
                if (child is Control c)
                    CleanupEventHandlers(c);
            }
        }
    }

    // --- 值转换工具 ---

    /// <summary>
    /// 仅解析 Pixels 单位为具体值，Percent/ViewportHeight 返回 NaN（由布局引擎处理）。
    /// </summary>
    private static double? ResolvePixelDimension(Dimension? dim)
    {
        if (!dim.HasValue) return null;
        return dim.Value.Unit switch
        {
            DimensionUnit.Pixels => dim.Value.Value,
            // Percent 和 ViewportHeight 不设置 Width/Height，由 EchoUIPanel 在布局时解析
            DimensionUnit.Percent => double.NaN,
            DimensionUnit.ViewportHeight => double.NaN,
            _ => null
        };
    }

    private static Thickness ToThickness(Spacing? spacing)
    {
        if (!spacing.HasValue) return new Thickness(0);
        var s = spacing.Value;
        return new Thickness(
            ResolveDimensionValue(s.Left),
            ResolveDimensionValue(s.Top),
            ResolveDimensionValue(s.Right),
            ResolveDimensionValue(s.Bottom)
        );
    }

    private static double ResolveDimensionValue(Dimension dim)
    {
        return dim.Unit switch
        {
            DimensionUnit.Pixels => dim.Value,
            _ => 0
        };
    }

    private static IBrush? ToBrush(Core.Color? color)
    {
        if (!color.HasValue) return null;
        var c = color.Value;
        return new SolidColorBrush(new global::Avalonia.Media.Color(c.A, c.R, c.G, c.B));
    }

    private static FontWeight ToFontWeight(string? weight)
    {
        return weight?.ToLower() switch
        {
            "bold" or "700" => FontWeight.Bold,
            "600" or "semibold" => FontWeight.SemiBold,
            "500" or "medium" => FontWeight.Medium,
            "300" or "light" => FontWeight.Light,
            "100" or "thin" => FontWeight.Thin,
            _ => FontWeight.Normal
        };
    }

    // --- Transitions ---

    private static void ApplyTransitions(Border border, ValueDictionary<string, Transition>? transitions)
    {
        if (transitions == null || transitions.Value.Data.Count == 0)
        {
            border.Transitions = null;
            return;
        }

        var avaloniaTransitions = new global::Avalonia.Animation.Transitions();
        foreach (var (propName, transition) in transitions.Value.Data)
        {
            var duration = TimeSpan.FromMilliseconds(transition.DurationMs);
            var easing = ToAvaloniaEasing(transition.Easing);

            switch (propName)
            {
                case nameof(ContainerProps.BackgroundColor):
                    avaloniaTransitions.Add(new global::Avalonia.Animation.BrushTransition
                    {
                        Property = Border.BackgroundProperty,
                        Duration = duration,
                        Easing = easing
                    });
                    break;
                case nameof(ContainerProps.Margin):
                    avaloniaTransitions.Add(new global::Avalonia.Animation.ThicknessTransition
                    {
                        Property = Border.MarginProperty,
                        Duration = duration,
                        Easing = easing
                    });
                    break;
                case nameof(ContainerProps.Width):
                    avaloniaTransitions.Add(new global::Avalonia.Animation.DoubleTransition
                    {
                        Property = Layoutable.WidthProperty,
                        Duration = duration,
                        Easing = easing
                    });
                    break;
                case nameof(ContainerProps.Height):
                    avaloniaTransitions.Add(new global::Avalonia.Animation.DoubleTransition
                    {
                        Property = Layoutable.HeightProperty,
                        Duration = duration,
                        Easing = easing
                    });
                    break;
            }
        }

        border.Transitions = avaloniaTransitions.Count > 0 ? avaloniaTransitions : null;
    }

    private static global::Avalonia.Animation.Easings.Easing ToAvaloniaEasing(Easing easing)
    {
        return easing switch
        {
            Easing.Ease => new global::Avalonia.Animation.Easings.SplineEasing(0.25, 0.1, 0.25, 1.0),
            Easing.EaseIn => new global::Avalonia.Animation.Easings.SplineEasing(0.42, 0, 1.0, 1.0),
            Easing.EaseOut => new global::Avalonia.Animation.Easings.SplineEasing(0, 0, 0.58, 1.0),
            Easing.EaseInOut => new global::Avalonia.Animation.Easings.SplineEasing(0.42, 0, 0.58, 1.0),
            _ => new global::Avalonia.Animation.Easings.LinearEasing()
        };
    }

    // --- 图片加载 ---

    private static void LoadImage(Border border, string src)
    {
        try
        {
            string? path = null;
            if (Path.IsPathRooted(src) && File.Exists(src))
            {
                path = src;
            }
            else
            {
                var currentDir = AppContext.BaseDirectory;
                var p1 = Path.Combine(currentDir, src.TrimStart('/', '\\'));
                if (File.Exists(p1)) path = p1;
            }

            if (path != null)
            {
                var bitmap = new global::Avalonia.Media.Imaging.Bitmap(path);
                var image = new global::Avalonia.Controls.Image
                {
                    Source = bitmap,
                    Stretch = Stretch.Uniform
                };
                border.Child = image;
            }
        }
        catch { /* 忽略加载错误 */ }
    }

    // --- 事件处理器存储 ---

    private class EventHandlerStore
    {
        public EventHandler<PointerPressedEventArgs>? PointerPressed { get; set; }
        public EventHandler<PointerReleasedEventArgs>? PointerReleased { get; set; }
        public EventHandler<PointerEventArgs>? PointerMoved { get; set; }
        public EventHandler<PointerEventArgs>? PointerEntered { get; set; }
        public EventHandler<PointerEventArgs>? PointerExited { get; set; }
        public EventHandler<KeyEventArgs>? KeyDown { get; set; }
        public EventHandler<KeyEventArgs>? KeyUp { get; set; }
        public EventHandler<TextChangedEventArgs>? TextChanged { get; set; }
    }
}

/// <summary>
/// Avalonia 更新调度器，通过 Dispatcher.UIThread 调度更新。
/// </summary>
public class AvaloniaUpdateScheduler : IUpdateScheduler
{
    public void Schedule(Func<Task> updateAction)
    {
        Dispatcher.UIThread.Post(() => _ = updateAction.Invoke());
    }
}
