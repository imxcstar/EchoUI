using System.Diagnostics;
using EchoUI.Core;

namespace EchoUI.Render.Win32
{
    /// <summary>
    /// Win32 渲染器，实现 IRenderer 接口。
    /// 使用 GDI+ 自绘模式，在单个 Win32 窗口上绘制所有 UI 元素。
    /// Input 元素使用嵌入的原生 Win32 Edit 控件。
    /// </summary>
    public class Win32Renderer : IRenderer, IInstanceBindingRenderer, IElementStateRenderer, IDisposable
    {
        private readonly Win32Window _window;
        private Win32Element? _rootElement;
        private Win32UpdateScheduler? _scheduler;
        private HitTestManager<Win32Element>? _hitTestManager;
        private readonly List<Win32Element> _floatingElements = [];
        private readonly Win32AnimationManager _animationManager;
        private ComponentInstance? _rootInstance;

        internal IReadOnlyList<Win32Element> FloatingElements => _floatingElements;
        internal Win32AnimationManager AnimationManager => _animationManager;

        private readonly Win32ImageService _imageService;
        private readonly Win32InputMethodService _inputMethodService;
        private readonly Win32NativeInputService _nativeInputService;
        private bool _disposed;
        private bool _layoutValid;
        private float _layoutViewportWidth;
        private float _layoutViewportHeight;
        private int _layoutCacheGeneration = 1;
        private readonly HashSet<string> _nativeDiagnostics = [];
        private readonly Dictionary<string, ElementStateSnapshot> _elementStateSnapshots = [];
        private readonly Stopwatch _wheelSmoothingStopwatch = Stopwatch.StartNew();
        private float _smoothedWheelPixels;
        private long _lastWheelSmoothingTimestamp;

        private const double WheelSmoothingResetMs = 140.0;
        private const float WheelSmoothingAlpha = 0.55f;

        internal Win32Element? RootElement => _rootElement;
        internal ComponentInstance? RootInstance => _rootInstance;
        internal Win32UpdateScheduler? Scheduler => _scheduler;
        internal HitTestManager<Win32Element> HitTestManager => _hitTestManager!;
        public IPlatformServices PlatformServices { get; }

        /// <summary>
        /// 是否启用滚轮输入值平滑。默认关闭，保持原始滚轮步进。
        /// </summary>
        public bool SmoothScrollEnabled { get; set; }

        public Win32Renderer(Win32Window window)
        {
            _window = window;
            PlatformServices = new Win32PlatformServices();
            _imageService = new Win32ImageService();
            _inputMethodService = new Win32InputMethodService(() => _window.Hwnd);
            _nativeInputService = new Win32NativeInputService(() => _window.Hwnd, RequestRepaint);
            _hitTestManager = new HitTestManager<Win32Element>(new HitTestPlatform<Win32Element>
            {
                GetFloatingElements = () => _floatingElements,
                RequestRepaint = (a, b) =>
                {
                    if (a == null && b == null) return;
                    if (a != null) InvalidateElementBounds(a);
                    if (b != null && !ReferenceEquals(a, b)) InvalidateElementBounds(b);
                },
                RequestRelayout = RequestRelayout,
                RequestScrollReposition = RequestScrollReposition,
                SmoothWheelScrollPixels = SmoothWheelScrollPixels,
                FocusWindow = FocusWindow,
                IsWindowValid = hwnd => NativeInterop.IsWindow(hwnd),
                SetNativeFocus = hwnd => NativeInterop.SetFocus(hwnd),
                IsShiftKeyDown = () => (NativeInterop.GetKeyState(NativeInterop.VK_SHIFT) & 0x8000) != 0
            });
            _animationManager = new Win32AnimationManager(window, this);
            window.SetRenderer(this);
        }

        private void InvalidateLayoutCache()
        {
            _layoutValid = false;
            _layoutCacheGeneration++;
        }

        public void AttachRootInstance(ComponentInstance rootInstance)
        {
            _rootInstance = rootInstance;
        }

        public void BindNativeElement(object nativeElement, ComponentInstance instance)
        {
            if (nativeElement is Win32Element element)
            {
                element.OwnerInstance = instance;
            }
        }

        public void UnbindNativeElement(object nativeElement)
        {
            if (nativeElement is Win32Element element)
            {
                element.OwnerInstance = null;
            }
        }

        public void SaveElementState(object nativeElement, string stateKey)
        {
            if (nativeElement is not Win32Element element)
                return;

            _elementStateSnapshots[stateKey] = new ElementStateSnapshot(
                element.ScrollOffsetX,
                element.ScrollOffsetY);
        }

        public void RestoreElementState(object nativeElement, string stateKey)
        {
            if (nativeElement is not Win32Element element)
                return;

            if (_elementStateSnapshots.TryGetValue(stateKey, out var snapshot))
            {
                element.ScrollOffsetX = snapshot.ScrollOffsetX;
                element.ScrollOffsetY = snapshot.ScrollOffsetY;
            }
        }

        private readonly record struct ElementStateSnapshot(float ScrollOffsetX, float ScrollOffsetY);

        public object CreateElement(string type)
        {
            var element = new Win32Element(type);

            // Input 元素创建原生 Edit 控件
            if (type == ElementCoreName.Input)
            {
                element.Width = Dimension.Percent(100);
                element.Height = Dimension.Percent(100);
                _nativeInputService.Create(element);
            }
            else if (type != ElementCoreName.Container && type != ElementCoreName.Text && type != "img")
            {
                ReportNativeDiagnostic($"[EchoUI.Win32] Native type '{type}' is not fully supported and will be rendered as a generic container.");
            }

            return element;
        }

        public void PatchProperties(object nativeElement, Props newProps, PropertyPatch patch)
        {
            var element = (Win32Element)nativeElement;

            // 始终同步事件处理器
            UpdateEventHandlers(element, newProps);

            if (patch.UpdatedProperties == null) return;
            InvalidateLayoutCache();

            // 1. 捕获动画属性的旧值
            (string propName, object? oldValue)[]? animatedProps = null;
            Transition[]? transitions = null;

            if (newProps is ContainerProps containerProps)
            {
                var transData = containerProps.Transitions?.Data;
                if (transData != null)
                {
                    var matched = new List<(string, object?)>();
                    var matchedTrans = new List<Transition>();
                    foreach (var kvp in transData)
                    {
                        if (patch.UpdatedProperties.ContainsKey(kvp.Key))
                        {
                            matched.Add((kvp.Key, _animationManager.GetPropertyValue(element, kvp.Key)));
                            if (kvp.Value is Transition tr)
                                matchedTrans.Add(tr);
                        }
                    }
                    if (matched.Count > 0)
                    {
                        animatedProps = [.. matched];
                        transitions = [.. matchedTrans];
                    }
                }
            }

            // 2. 应用属性变化
            foreach (var (propName, propValue) in patch.UpdatedProperties)
            {
                ApplyProperty(element, newProps, propName, propValue);
            }

            // 为不同类型的元素应用默认值（与 WebRenderer 保持一致）
            RenderNodePropertyMapper.ApplyDefaults(element, newProps);

            // 3. 启动动画（从旧值 → 新值）
            if (animatedProps != null && transitions != null)
            {
                for (int i = 0; i < animatedProps.Length; i++)
                {
                    var (propName, oldValue) = animatedProps[i];
                    var newValue = _animationManager.GetPropertyValue(element, propName);
                    _animationManager.StartAnimation(element, propName, oldValue, newValue, transitions[i]);
                }
            }

            // 同步 Input 的原生 Edit 控件
            if (element.ElementType == ElementCoreName.Input && element.EditHwnd != 0)
            {
                _nativeInputService.Sync(element);
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

            InvalidateLayoutCache();
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
            _hitTestManager?.DetachSubtree(childElement);
            parentElement.Children.Remove(childElement);
            childElement.Parent = null;
            InvalidateLayoutCache();

            ReleaseElementTree(childElement);
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

            InvalidateLayoutCache();
        }

        public TextMeasurementResult MeasureText(TextMeasurementRequest request)
        {
            return PlatformServices.TextMeasurer.Measure(request);
        }

        private static TextMeasurementResult MeasureTextForLayout(Win32Element element, float? widthConstraint, bool noWrap)
        {
            var fontSize = element.FontSize > 0 ? element.FontSize : 14f;
            return GdiText.MeasureText(element.Text, element.FontFamily, fontSize, element.FontWeight, widthConstraint, noWrap);
        }

        public Task<string> ReadClipboardTextAsync()
        {
            return PlatformServices.Clipboard.ReadTextAsync();
        }

        public Task WriteClipboardTextAsync(string text)
        {
            return PlatformServices.Clipboard.WriteTextAsync(text);
        }

        public IUpdateScheduler GetScheduler(object rootContainer)
        {
            _scheduler = new Win32UpdateScheduler(_window.Hwnd);
            return _scheduler;
        }

        // --- 属性应用 ---

        private void ApplyProperty(Win32Element element, Props props, string propName, object? propValue)
        {
            if (props is NativeProps nativeProps)
            {
                ApplyNativeProperty(element, nativeProps, propName, propValue);
                return;
            }

            RenderNodePropertyMapper.ApplyProperty(element, props, propName, propValue);

            if (propName == nameof(ContainerProps.InputMethodAnchorPoint) && element.IsFocused)
            {
                _inputMethodService.UpdatePosition(element);
            }
        }

        private void ApplyNativeProperty(Win32Element element, NativeProps nativeProps, string propName, object? propValue)
        {
            if (propValue is Delegate) return;

            if (propName == "textContent" || propName == "text")
            {
                element.Text = propValue?.ToString();
                return;
            }

            if (element.ElementType == "img")
            {
                if (propName == "src")
                {
                    if (propValue is string src)
                    {
                        _imageService.Load(element, src);
                    }
                    else if (propValue == null && element.NativeImageHandle != 0)
                    {
                        _imageService.Clear(element);
                    }
                    return;
                }

                if (propName == "style")
                {
                    if (propValue is string style)
                    {
                        ParseStyle(element, style);
                    }
                    else if (propValue == null)
                    {
                        ResetNativeStyle(element);
                    }
                    return;
                }
            }

            ReportNativeDiagnostic($"[EchoUI.Win32] Native property '{propName}' on '{nativeProps.Type}' is not supported.");
        }

        // --- 事件处理器同步 ---

        private void UpdateEventHandlers(Win32Element element, Props newProps)
        {
            if (newProps is NativeProps nativeProps)
            {
                RenderNodePropertyMapper.ClearEventHandlers(element);
                if (nativeProps.Properties == null) return;

                foreach (var item in nativeProps.Properties.Value.Data)
                {
                    ApplyNativeEventHandler(element, item.Key, item.Value);
                }

                return;
            }

            RenderNodePropertyMapper.UpdateEventHandlers(element, newProps);
        }

        private void ApplyNativeEventHandler(Win32Element element, string eventName, object? value)
        {
            switch (eventName)
            {
                case "click" when value is Action<MouseButton> clickHandler:
                    element.OnClick = clickHandler;
                    return;
                case "click" when value is Action clickAction:
                    element.OnClick = _ => clickAction();
                    return;
                case "mousemove" when value is Action<Core.Point> mouseMoveHandler:
                    element.OnMouseMove = mouseMoveHandler;
                    return;
                case "mousedown" when value is Action<MouseEvent> pointerDownHandler:
                    element.OnPointerDown = pointerDownHandler;
                    return;
                case "mousemove" when value is Action<MouseEvent> pointerMoveHandler:
                    element.OnPointerMove = pointerMoveHandler;
                    return;
                case "mouseup" when value is Action<MouseEvent> pointerUpHandler:
                    element.OnPointerUp = pointerUpHandler;
                    return;
                case "mouseenter" when value is Action mouseEnterHandler:
                    element.OnMouseEnter = mouseEnterHandler;
                    return;
                case "mouseleave" when value is Action mouseLeaveHandler:
                    element.OnMouseLeave = mouseLeaveHandler;
                    return;
                case "mousedown" when value is Action mouseDownHandler:
                    element.OnMouseDown = mouseDownHandler;
                    return;
                case "mouseup" when value is Action mouseUpHandler:
                    element.OnMouseUp = mouseUpHandler;
                    return;
                case "keydown" when value is Action<int> keyDownHandler:
                    element.OnKeyDown = keyDownHandler;
                    return;
                case "keyup" when value is Action<int> keyUpHandler:
                    element.OnKeyUp = keyUpHandler;
                    return;
                case "keypress" when value is Action<string> textInputHandler:
                    element.OnTextInput = textInputHandler;
                    return;
                case "textcomposition" when value is Action<TextCompositionEvent> textCompositionHandler:
                    element.OnTextComposition = textCompositionHandler;
                    return;
                case "focus" when value is Action focusHandler:
                    element.OnFocus = focusHandler;
                    return;
                case "blur" when value is Action blurHandler:
                    element.OnBlur = blurHandler;
                    return;
                case "input" when value is Action<string> inputHandler:
                    element.OnValueChanged = inputHandler;
                    return;
                default:
                    ReportNativeDiagnostic($"[EchoUI.Win32] Native event '{eventName}' is not supported.");
                    return;
            }
        }

        [Conditional("DEBUG")]
        private void ReportNativeDiagnostic(string message)
        {
            if (_nativeDiagnostics.Add(message))
            {
                Debug.WriteLine(message);
            }
        }

        /// <summary>
        /// 处理 Edit 控件的 EN_CHANGE 通知
        /// </summary>
        internal void HandleEditChange(nint editHwnd)
        {
            _nativeInputService.HandleChange(editHwnd);
        }

        internal void HandleEditFocusChange(nint editHwnd, bool isFocused)
        {
            _nativeInputService.HandleFocusChange(editHwnd, isFocused);
        }

        private void ReleaseElementTree(Win32Element element)
        {
            _animationManager.StopAnimationsForElement(element);

            foreach (var child in element.Children.ToArray())
            {
                ReleaseElementTree(child);
            }

            element.Children.Clear();
            ReleasePlatformResources(element);
            RenderNodePropertyMapper.ClearEventHandlers(element);
            element.Parent = null;
        }

        private void ReleasePlatformResources(Win32Element element)
        {
            _nativeInputService.Release(element);
            GdiPainter.ReleaseCachedResources(element);
            _imageService.Clear(element);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            if (_rootElement != null)
            {
                _hitTestManager?.DetachSubtree(_rootElement);
                ReleaseElementTree(_rootElement);
                _rootElement = null;
            }

            _rootInstance = null;
            _floatingElements.Clear();
        }

        // --- 布局与重绘 ---

        /// <summary>
        /// 请求重新布局并重绘
        /// </summary>
        public void RequestRelayout()
        {
            if (_rootElement == null || _window.Hwnd == 0) return;

            _layoutValid = false;
            NativeInterop.GetClientRect(_window.Hwnd, out var rect);
            EnsureLayout(rect.Width, rect.Height);
            NativeInterop.InvalidateRect(_window.Hwnd, 0, false);
        }

        internal void RequestAnimationRelayout()
        {
            if (_rootElement == null || _window.Hwnd == 0) return;

            _layoutValid = false;
            _layoutCacheGeneration++;
            NativeInterop.InvalidateRect(_window.Hwnd, 0, false);
        }

        internal void RequestScrollReposition(Win32Element scrollTarget, float previousScrollX, float previousScrollY)
        {
            if (SmoothScrollEnabled)
            {
                _animationManager.StartScrollAnimation(scrollTarget, previousScrollX, previousScrollY);
                return;
            }

            ApplyScrollReposition(scrollTarget);
        }

        private float SmoothWheelScrollPixels(float wheelPixels)
        {
            if (!SmoothScrollEnabled)
                return wheelPixels;

            var now = _wheelSmoothingStopwatch.ElapsedTicks;
            var elapsedMs = _lastWheelSmoothingTimestamp == 0
                ? WheelSmoothingResetMs
                : (now - _lastWheelSmoothingTimestamp) * 1000.0 / Stopwatch.Frequency;
            _lastWheelSmoothingTimestamp = now;

            if (elapsedMs >= WheelSmoothingResetMs || Math.Sign(_smoothedWheelPixels) != Math.Sign(wheelPixels))
                _smoothedWheelPixels = wheelPixels;
            else
                _smoothedWheelPixels += (wheelPixels - _smoothedWheelPixels) * WheelSmoothingAlpha;

            return _smoothedWheelPixels;
        }

        internal void ApplyScrollReposition(
            Win32Element scrollTarget,
            bool syncManagedLayout = true,
            bool syncNativeInputs = true)
        {
            if (_rootElement == null || _window.Hwnd == 0)
                return;

            NativeInterop.GetClientRect(_window.Hwnd, out var rect);
            float vpW = rect.Width;
            float vpH = rect.Height;
            if (vpW <= 0 || vpH <= 0)
                return;

            if (!_layoutValid || !_layoutViewportWidth.Equals(vpW) || !_layoutViewportHeight.Equals(vpH))
            {
                RequestRelayout();
                return;
            }

            LayoutEngine.UpdateAbsoluteLayout(scrollTarget);
            if (syncManagedLayout)
                SyncInstanceLayouts();
            if (syncNativeInputs)
                _nativeInputService.UpdatePositions(scrollTarget, vpW, vpH);
            RequestRepaint(scrollTarget);
        }

        internal void EnsureLayout(float vpW, float vpH)
        {
            if (_rootElement == null || vpW <= 0 || vpH <= 0)
                return;

            if (_layoutValid && _layoutViewportWidth.Equals(vpW) && _layoutViewportHeight.Equals(vpH))
                return;

            if (!_layoutViewportWidth.Equals(vpW) || !_layoutViewportHeight.Equals(vpH))
            {
                _layoutCacheGeneration++;
            }

            LayoutEngine.ComputeLayout(_rootElement, vpW, vpH, _layoutCacheGeneration, MeasureTextForLayout);

#if DEBUG
            DumpLayoutDiagnostics(_rootElement, vpW, vpH);
#endif

            SyncInstanceLayouts();
            _nativeInputService.UpdatePositions(_rootElement, vpW, vpH);
            CollectFloatingElements();
            _layoutViewportWidth = vpW;
            _layoutViewportHeight = vpH;
            _layoutValid = true;
        }

        private void SyncInstanceLayouts()
        {
            if (_rootInstance == null)
                return;

            SyncInstanceLayoutsRecursive(_rootInstance);
        }

        private LayoutBox? SyncInstanceLayoutsRecursive(ComponentInstance instance)
        {
            foreach (var child in instance.Children)
            {
                SyncInstanceLayoutsRecursive(child);
            }

            if (instance.NativeElement is Win32Element native)
            {
                instance.Layout = new LayoutBox(native.AbsoluteX, native.AbsoluteY, native.LayoutWidth, native.LayoutHeight);
                return instance.Layout;
            }

            if (instance.Children.Count == 1)
            {
                instance.Layout = instance.Children[0].Layout;
                return instance.Layout;
            }

            var childLayouts = instance.Children.Select(c => c.Layout).Where(l => l.HasValue).Select(l => l!.Value).ToList();
            if (childLayouts.Count == 0)
            {
                instance.Layout = null;
                return null;
            }

            var left = childLayouts.Min(l => l.X);
            var top = childLayouts.Min(l => l.Y);
            var right = childLayouts.Max(l => l.X + l.Width);
            var bottom = childLayouts.Max(l => l.Y + l.Height);
            instance.Layout = new LayoutBox(left, top, right - left, bottom - top);
            return instance.Layout;
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

        // ──────────────── Layout Diagnostics ────────────────

        [Conditional("DEBUG")]
        private void DumpLayoutDiagnostics(Win32Element root, float vpW, float vpH)
        {
            var logPath = Path.Combine(AppContext.BaseDirectory, "layout_diagnostics.txt");
            if (File.Exists(logPath)) return; // dump only first layout

            using var w = new StreamWriter(logPath, false);
            w.WriteLine($"=== EchoUI Layout Diagnostics (viewport {vpW}x{vpH}) ===");
            w.WriteLine($"LayoutDefaults: FlexShrink={LayoutDefaults.FlexShrink}, Overflow={LayoutDefaults.Overflow}, AlignItems={LayoutDefaults.AlignItems}, Direction={LayoutDefaults.Direction}");
            w.WriteLine();
            DumpElementTree(root, "", w);
        }

        private static void DumpElementTree(Win32Element e, string indent, StreamWriter w)
        {
            var ovf = e.Overflow;
            var maxScrollY = Math.Max(0, e.CachedContentHeight - e.LayoutHeight);
            var scrollY = e.ScrollOffsetY;
            var key = e.OwnerInstance?.Element.Props.Key;
            var text = e.Text;
            var label = key != null ? $"[{key}]" : text != null ? $"\"{(text.Length <= 40 ? text : text[..40] + "...")}\"" : "";

            w.WriteLine(
                $"{indent}{e.ElementType} {label}" +
                $"  size=({e.LayoutWidth:F1}x{e.LayoutHeight:F1})" +
                $"  content=({e.CachedContentWidth:F1}x{e.CachedContentHeight:F1})" +
                $"  overflow={ovf}" +
                $"  flexShrink={e.FlexShrink}" +
                $"  flexGrow={e.FlexGrow}" +
                $"  direction={e.Direction}" +
                $"  alignItems={e.AlignItems}" +
                (maxScrollY > 0 ? $"  ⭐ maxScrollY={maxScrollY:F1}" : "") +
                (scrollY != 0 ? $"  scrollY={scrollY:F1}" : ""));

            foreach (var child in e.Children)
            {
                DumpElementTree(child, indent + "  ", w);
            }
        }

        internal void FocusWindow()
        {
            if (_window.Hwnd != 0)
                NativeInterop.SetFocus(_window.Hwnd);
        }

        /// <summary>
        /// 请求重绘（不重新布局）
        /// </summary>
        internal void RequestRepaint()
        {
            if (_window.Hwnd != 0)
                NativeInterop.InvalidateRect(_window.Hwnd, 0, false);
        }

        internal void RequestRepaint(Win32Element? element)
        {
            if (_window.Hwnd == 0)
                return;

            if (element == null || element.LayoutWidth <= 0 || element.LayoutHeight <= 0)
            {
                NativeInterop.InvalidateRect(_window.Hwnd, 0, false);
                return;
            }

            InvalidateElementBounds(element);
        }

        internal void RequestRepaint(Win32Element? first, Win32Element? second)
        {
            if (_window.Hwnd == 0)
                return;

            if (first == null && second == null)
            {
                NativeInterop.InvalidateRect(_window.Hwnd, 0, false);
                return;
            }

            if (first != null)
                InvalidateElementBounds(first);
            if (second != null && !ReferenceEquals(first, second))
                InvalidateElementBounds(second);
        }

        private void InvalidateElementBounds(Win32Element element)
        {
            const int padding = 3;
            var shadowBlur = element.Shadow.IsVisible
                ? (int)Math.Ceiling(Math.Max(0, element.Shadow.Blur))
                : 0;
            var shadowBottom = element.Shadow.IsVisible
                ? (int)Math.Ceiling(Math.Max(0, element.Shadow.OffsetY + element.Shadow.Blur))
                : 0;
            var rect = new NativeInterop.RECT
            {
                Left = (int)Math.Floor(element.AbsoluteX) - padding - shadowBlur,
                Top = (int)Math.Floor(element.AbsoluteY) - padding,
                Right = (int)Math.Ceiling(element.AbsoluteX + element.LayoutWidth) + padding + shadowBlur,
                Bottom = (int)Math.Ceiling(element.AbsoluteY + element.LayoutHeight) + padding + shadowBottom
            };

            NativeInterop.InvalidateRect(_window.Hwnd, ref rect, false);
        }

        /// <summary>
        /// 更新所有 Edit 控件的位置以匹配布局结果（公开方法供 Win32Window 调用）
        /// </summary>
        public void UpdateAllEditPositions(float vpW, float vpH)
        {
            if (_rootElement != null)
                _nativeInputService.UpdatePositions(_rootElement, vpW, vpH);
        }

        private static void ResetNativeStyle(Win32Element element)
        {
            element.Width = null;
            element.Height = null;
            element.BorderRadius = 0;
        }

        private void ParseStyle(Win32Element element, string style)
        {
            var parts = style.Split(';');
            foreach (var part in parts)
            {
                var kv = part.Split(':');
                if (kv.Length != 2) continue;
                var key = kv[0].Trim().ToLower();
                var value = kv[1].Trim().ToLower();

                if (key == "width")
                {
                    if (value.EndsWith("px") && float.TryParse(value[..^2], out float v1))
                        element.Width = Dimension.Pixels(v1);
                    else if (value.EndsWith("%") && float.TryParse(value[..^1], out float v2))
                        element.Width = Dimension.Percent(v2);
                }
                else if (key == "height")
                {
                    if (value.EndsWith("px") && float.TryParse(value[..^2], out float v3))
                        element.Height = Dimension.Pixels(v3);
                    else if (value.EndsWith("%") && float.TryParse(value[..^1], out float v4))
                        element.Height = Dimension.Percent(v4);
                }
                else if (key == "border-radius")
                {
                    if (value.EndsWith("px") && float.TryParse(value[..^2], out float v5))
                        element.BorderRadius = v5;
                }
            }
        }

        internal Win32Element? GetElementByEditHwnd(nint hwnd)
        {
            return _nativeInputService.GetElement(hwnd);
        }

        internal void UpdateImePosition(Win32Element? element)
        {
            _inputMethodService.UpdatePosition(element);
        }
    }
}
