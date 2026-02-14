using System.Runtime.InteropServices;
using EchoUI.Core;

namespace EchoUI.Render.Win32
{
    /// <summary>
    /// Win32 渲染器，实现 IRenderer 接口。
    /// 使用 GDI+ 自绘模式，在单个 Win32 窗口上绘制所有 UI 元素。
    /// Input 元素使用嵌入的原生 Win32 Edit 控件。
    /// </summary>
    public class Win32Renderer : IRenderer
    {
        private readonly Win32Window _window;
        private Win32Element? _rootElement;
        private Win32UpdateScheduler? _scheduler;
        private HitTestManager? _hitTestManager;
        private readonly List<Win32Element> _floatingElements = [];

        internal IReadOnlyList<Win32Element> FloatingElements => _floatingElements;

        /// <summary>
        /// 所有 Input 元素的 Edit HWND → Win32Element 映射
        /// </summary>
        private readonly Dictionary<nint, Win32Element> _editElements = [];

        /// <summary>
        /// 防止 Edit 控件 EN_CHANGE 通知的递归触发
        /// </summary>
        private bool _suppressEditNotification;

        internal Win32Element? RootElement => _rootElement;
        internal Win32UpdateScheduler? Scheduler => _scheduler;
        internal HitTestManager HitTestManager => _hitTestManager!;

        public Win32Renderer(Win32Window window)
        {
            _window = window;
            _hitTestManager = new HitTestManager(this);
            window.SetRenderer(this);
        }

        public object CreateElement(string type)
        {
            var element = new Win32Element(type);

            // Input 元素创建原生 Edit 控件
            if (type == ElementCoreName.Input)
            {
                CreateEditControl(element);
            }

            return element;
        }

        public void PatchProperties(object nativeElement, Props newProps, PropertyPatch patch)
        {
            var element = (Win32Element)nativeElement;

            // 始终同步事件处理器
            UpdateEventHandlers(element, newProps);

            if (patch.UpdatedProperties == null) return;

            foreach (var (propName, propValue) in patch.UpdatedProperties)
            {
                ApplyProperty(element, newProps, propName, propValue);
            }

            // 为不同类型的元素应用默认值（与 WebRenderer 保持一致）
            switch (newProps)
            {
                case ContainerProps p:
                    element.Direction = p.Direction ?? LayoutDirection.Vertical;
                    element.FlexShrink = p.FlexShrink ?? 0;
                    element.FlexGrow = p.FlexGrow ?? 0;
                    break;
                case TextProps:
                    element.MouseThrough = true;
                    break;
            }

            // 同步 Input 的原生 Edit 控件
            if (element.ElementType == ElementCoreName.Input && element.EditHwnd != 0)
            {
                SyncEditControl(element);
            }


        }

        public void AddChild(object parent, object child, int index)
        {
            Win32Element parentElement;
            if (parent is string)
            {
                // 根容器
                _rootElement ??= new Win32Element(ElementCoreName.Container);
                parentElement = _rootElement;
            }
            else
            {
                parentElement = (Win32Element)parent;
            }

            var childElement = (Win32Element)child;
            childElement.Parent = parentElement;

            if (index >= 0 && index < parentElement.Children.Count)
                parentElement.Children.Insert(index, childElement);
            else
                parentElement.Children.Add(childElement);


        }

        public void RemoveChild(object parent, object child)
        {
            Win32Element parentElement;
            if (parent is string)
            {
                parentElement = _rootElement!;
            }
            else
            {
                parentElement = (Win32Element)parent;
            }

            var childElement = (Win32Element)child;
            parentElement.Children.Remove(childElement);
            childElement.Parent = null;

            // 销毁 Input 的 Edit 控件
            DestroyEditControls(childElement);


        }

        public void MoveChild(object parent, object child, int newIndex)
        {
            Win32Element parentElement;
            if (parent is string)
            {
                parentElement = _rootElement!;
            }
            else
            {
                parentElement = (Win32Element)parent;
            }

            var childElement = (Win32Element)child;
            parentElement.Children.Remove(childElement);

            if (newIndex >= 0 && newIndex < parentElement.Children.Count)
                parentElement.Children.Insert(newIndex, childElement);
            else
                parentElement.Children.Add(childElement);


        }

        public IUpdateScheduler GetScheduler(object rootContainer)
        {
            _scheduler = new Win32UpdateScheduler(_window.Hwnd);
            return _scheduler;
        }

        // --- 属性应用 ---

        private void ApplyProperty(Win32Element element, Props props, string propName, object? propValue)
        {
            switch (props)
            {
                case ContainerProps:
                    ApplyContainerProperty(element, propName, propValue);
                    break;
                case TextProps:
                    ApplyTextProperty(element, propName, propValue);
                    break;
                case InputProps:
                    ApplyInputProperty(element, propName, propValue);
                    break;
                case NativeProps nativeProps:
                    ApplyNativeProperty(element, nativeProps, propName, propValue);
                    break;
            }
        }

        private void ApplyContainerProperty(Win32Element element, string propName, object? propValue)
        {
            switch (propName)
            {
                // 尺寸
                case nameof(ContainerProps.Width): element.Width = propValue as Dimension?; break;
                case nameof(ContainerProps.Height): element.Height = propValue as Dimension?; break;
                case nameof(ContainerProps.MinWidth): element.MinWidth = propValue as Dimension?; break;
                case nameof(ContainerProps.MinHeight): element.MinHeight = propValue as Dimension?; break;
                case nameof(ContainerProps.MaxWidth): element.MaxWidth = propValue as Dimension?; break;
                case nameof(ContainerProps.MaxHeight): element.MaxHeight = propValue as Dimension?; break;

                // 间距
                case nameof(ContainerProps.Margin): element.Margin = propValue as Spacing?; break;
                case nameof(ContainerProps.Padding): element.Padding = propValue as Spacing?; break;

                // Flex
                case nameof(ContainerProps.Direction):
                    element.Direction = propValue is LayoutDirection dir ? dir : LayoutDirection.Vertical;
                    break;
                case nameof(ContainerProps.JustifyContent):
                    element.JustifyContent = propValue is JustifyContent jc ? jc : JustifyContent.Start;
                    break;
                case nameof(ContainerProps.AlignItems):
                    element.AlignItems = propValue is AlignItems ai ? ai : AlignItems.Start;
                    break;
                case nameof(ContainerProps.FlexGrow):
                    element.FlexGrow = propValue is float fg ? fg : 0;
                    break;
                case nameof(ContainerProps.FlexShrink):
                    element.FlexShrink = propValue is float fs ? fs : 0;
                    break;
                case nameof(ContainerProps.Gap):
                    element.Gap = propValue is float gap ? gap : 0;
                    break;
                case nameof(ContainerProps.Float):
                    element.Float = propValue is true;
                    break;
                case nameof(ContainerProps.Overflow):
                    element.Overflow = propValue is Overflow ov ? ov : Overflow.Visible;
                    break;

                // 外观
                case nameof(ContainerProps.BackgroundColor):
                    element.BackgroundColor = propValue as Core.Color?;
                    break;
                case nameof(ContainerProps.BorderColor):
                    element.BorderColor = propValue as Core.Color?;
                    break;
                case nameof(ContainerProps.BorderStyle):
                    element.BorderStyle = propValue is Core.BorderStyle bs ? bs : Core.BorderStyle.None;
                    break;
                case nameof(ContainerProps.BorderWidth):
                    element.BorderWidth = propValue is float bw ? bw : 0;
                    break;
                case nameof(ContainerProps.BorderRadius):
                    element.BorderRadius = propValue is float br ? br : 0;
                    break;

                // 事件由 UpdateEventHandlers 处理
                // Transitions 在 Win32 下暂不支持动画，直接忽略
            }
        }

        private void ApplyTextProperty(Win32Element element, string propName, object? propValue)
        {
            switch (propName)
            {
                case nameof(TextProps.Text):
                    element.Text = propValue as string;
                    break;
                case nameof(TextProps.FontFamily):
                    element.FontFamily = propValue as string;
                    break;
                case nameof(TextProps.FontSize):
                    element.FontSize = propValue is float fs ? fs : 14;
                    break;
                case nameof(TextProps.Color):
                    element.TextColor = propValue as Core.Color?;
                    break;
                case nameof(TextProps.FontWeight):
                    element.FontWeight = propValue as string;
                    break;
                case nameof(TextProps.MouseThrough):
                    element.MouseThrough = propValue is not false;
                    break;
            }
        }

        private void ApplyInputProperty(Win32Element element, string propName, object? propValue)
        {
            switch (propName)
            {
                case nameof(InputProps.Value):
                    element.InputValue = propValue as string;
                    break;
                // OnValueChanged 由 UpdateEventHandlers 处理
            }
        }

        private void ApplyNativeProperty(Win32Element element, NativeProps nativeProps, string propName, object? propValue)
        {
            // NativeProps 的任意属性：仅处理已知的样式属性
            if (propValue is Delegate) return; // 事件由 UpdateEventHandlers 处理

            // 尝试作为文本内容
            if (propName == "textContent" || propName == "text")
            {
                element.Text = propValue?.ToString();
            }
        }

        // --- 事件处理器同步 ---

        private void UpdateEventHandlers(Win32Element element, Props newProps)
        {
            switch (newProps)
            {
                case ContainerProps p:
                    element.OnClick = p.OnClick;
                    element.OnMouseMove = p.OnMouseMove;
                    element.OnMouseEnter = p.OnMouseEnter;
                    element.OnMouseLeave = p.OnMouseLeave;
                    element.OnMouseDown = p.OnMouseDown;
                    element.OnMouseUp = p.OnMouseUp;
                    element.OnKeyDown = p.OnKeyDown;
                    element.OnKeyUp = p.OnKeyUp;
                    break;
                case InputProps ip:
                    element.OnValueChanged = ip.OnValueChanged;
                    break;
                case NativeProps nativeProps when nativeProps.Properties != null:
                    foreach (var item in nativeProps.Properties.Value.Data)
                    {
                        if (item.Value is Action<MouseButton> clickHandler)
                            element.OnClick = clickHandler;
                        else if (item.Value is Action action)
                        {
                            // 简单映射
                            if (item.Key == "click") element.OnClick = _ => action();
                        }
                    }
                    break;
            }
        }

        // --- Edit 控件管理 ---

        private void CreateEditControl(Win32Element element)
        {
            if (_window.Hwnd == 0) return;

            var hwnd = NativeInterop.CreateWindowEx(
                0,
                "EDIT",
                "",
                NativeInterop.WS_CHILD | NativeInterop.WS_VISIBLE | NativeInterop.ES_AUTOHSCROLL | NativeInterop.ES_LEFT,
                0, 0, 100, 24,
                _window.Hwnd,
                0,
                NativeInterop.GetModuleHandle(null),
                0);

            if (hwnd != 0)
            {
                element.EditHwnd = hwnd;
                _editElements[hwnd] = element;
            }
        }

        private void SyncEditControl(Win32Element element)
        {
            if (element.EditHwnd == 0) return;

            // 同步文本值
            if (element.InputValue != null)
            {
                int len = NativeInterop.GetWindowTextLength(element.EditHwnd);
                var buffer = new char[len + 1];
                NativeInterop.GetWindowText(element.EditHwnd, buffer, buffer.Length);
                var currentText = new string(buffer, 0, len);

                if (currentText != element.InputValue)
                {
                    _suppressEditNotification = true;
                    NativeInterop.SetWindowText(element.EditHwnd, element.InputValue);
                    _suppressEditNotification = false;
                }
            }
        }

        /// <summary>
        /// 处理 Edit 控件的 EN_CHANGE 通知
        /// </summary>
        internal void HandleEditChange(nint editHwnd)
        {
            if (_suppressEditNotification) return;

            if (_editElements.TryGetValue(editHwnd, out var element))
            {
                int len = NativeInterop.GetWindowTextLength(editHwnd);
                var buffer = new char[len + 1];
                NativeInterop.GetWindowText(editHwnd, buffer, buffer.Length);
                var text = new string(buffer, 0, len);

                element.InputValue = text;
                element.OnValueChanged?.Invoke(text);
            }
        }

        private void DestroyEditControls(Win32Element element)
        {
            if (element.EditHwnd != 0)
            {
                _editElements.Remove(element.EditHwnd);
                if (NativeInterop.IsWindow(element.EditHwnd))
                    NativeInterop.DestroyWindow(element.EditHwnd);
                element.EditHwnd = 0;
            }

            foreach (var child in element.Children)
            {
                DestroyEditControls(child);
            }
        }

        // --- 布局与重绘 ---

        /// <summary>
        /// 请求重新布局并重绘
        /// </summary>
        public void RequestRelayout()
        {
            if (_rootElement == null || _window.Hwnd == 0) return;

            NativeInterop.GetClientRect(_window.Hwnd, out var rect);
            float vpW = rect.Width;
            float vpH = rect.Height;

            if (vpW > 0 && vpH > 0)
            {
                FlexLayout.ComputeLayout(_rootElement, vpW, vpH);
                UpdateEditPositions(_rootElement);
                CollectFloatingElements();
            }

            NativeInterop.InvalidateRect(_window.Hwnd, 0, false);
        }

        private void CollectFloatingElements()
        {
            _floatingElements.Clear();
            if (_rootElement == null) return;
            CollectFloatingElementsRecursive(_rootElement);
        }

        private void CollectFloatingElementsRecursive(Win32Element element)
        {
            foreach (var child in element.Children)
            {
                if (child.Float)
                {
                    _floatingElements.Add(child);
                    // 如果它是 Float 元素，我们把它作为独立的层。
                    // 它的子元素如果也是 Float，通常是相对于它的（如下级菜单），
                    // 所以我们暂时不把嵌套的 Float 提升到顶层，而是跟随这个 Float 元素。
                    // 但这里策略是：只要是 Float，就收集？
                    // 如果 A(Float) -> B(Float)，B 是 A 的子元素。
                    // 如果 Paint(A) 会 Paint(B)。
                    // 如果我们收集了 A，GdiPainter 会 Paint(A)。
                    // 此时我们不应该再收集 B，否则 B 会被画两次（一次在 A 内部，一次作为 Top Layer）。
                    // 所以：一旦遇到 Float，加入列表，并且不再遍历其子元素寻找 Float。
                }
                else
                {
                    CollectFloatingElementsRecursive(child);
                }
            }
        }

        /// <summary>
        /// 请求重绘（不重新布局）
        /// </summary>
        internal void RequestRepaint()
        {
            if (_window.Hwnd != 0)
                NativeInterop.InvalidateRect(_window.Hwnd, 0, false);
        }

        /// <summary>
        /// 更新所有 Edit 控件的位置以匹配布局结果（公开方法供 Win32Window 调用）
        /// </summary>
        public void UpdateAllEditPositions()
        {
            if (_rootElement != null)
                UpdateEditPositions(_rootElement);
        }

        /// <summary>
        /// 更新所有 Edit 控件的位置以匹配布局结果
        /// </summary>
        private void UpdateEditPositions(Win32Element element)
        {
            if (element.EditHwnd != 0)
            {
                NativeInterop.MoveWindow(
                    element.EditHwnd,
                    (int)element.AbsoluteX,
                    (int)element.AbsoluteY,
                    (int)element.LayoutWidth,
                    (int)element.LayoutHeight,
                    true);
            }

            foreach (var child in element.Children)
            {
                UpdateEditPositions(child);
            }
        }
    }
}
