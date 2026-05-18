namespace EchoUI.Core;

/// <summary>
/// 命中测试节点接口：供 HitTestManager 遍历元素树、检查边界、分发事件。
/// 每个渲染后端的原生元素节点实现此接口即可复用命中测试逻辑。
/// </summary>
public interface IHitTestNode<TNode> where TNode : class, IHitTestNode<TNode>
{
    /// <summary>父节点</summary>
    TNode? HitParent { get; }
    /// <summary>子节点列表（用于命中测试遍历）</summary>
    IReadOnlyList<TNode> HitChildren { get; }

    // --- 布局 ---
    float AbsoluteX { get; }
    float AbsoluteY { get; }
    float LayoutWidth { get; }
    float LayoutHeight { get; }
    float ScrollOffsetX { get; set; }
    float ScrollOffsetY { get; set; }
    float CachedContentWidth { get; }
    float CachedContentHeight { get; }

    // --- 属性 ---
    string ElementType { get; }
    bool Float { get; }
    Overflow Overflow { get; }
    bool MouseThrough { get; }

    // --- 状态 ---
    bool IsHovered { get; set; }
    bool IsFocused { get; set; }

    // --- 事件处理器（只读） ---
    Action<MouseButton>? OnClick { get; }
    Action<Point>? OnMouseMove { get; }
    Action<MouseEvent>? OnPointerDown { get; }
    Action<MouseEvent>? OnPointerMove { get; }
    Action<MouseEvent>? OnPointerUp { get; }
    Action? OnMouseEnter { get; }
    Action? OnMouseLeave { get; }
    Action? OnMouseDown { get; }
    Action? OnMouseUp { get; }
    Action<int>? OnKeyDown { get; }
    Action<int>? OnKeyUp { get; }
    Action<string>? OnTextInput { get; }
    Action<TextCompositionEvent>? OnTextComposition { get; }
    Action? OnFocus { get; }
    Action? OnBlur { get; }

    /// <summary>原生 Input 控件的句柄（无则为 0）</summary>
    nint EditHandle { get; }
}

/// <summary>
/// 命中测试的平台回调：封装平台相关的重绘、焦点、浮动元素收集等操作。
/// </summary>
public record struct HitTestPlatform<TNode> where TNode : class, IHitTestNode<TNode>
{
    /// <summary>获取浮动元素列表</summary>
    public Func<IReadOnlyList<TNode>> GetFloatingElements { get; init; }
    /// <summary>请求重绘指定元素（第一个为 null 表示全窗口重绘）</summary>
    public Action<TNode?, TNode?> RequestRepaint { get; init; }
    /// <summary>请求重新布局并重绘</summary>
    public Action RequestRelayout { get; init; }
    /// <summary>将焦点设置到宿主窗口</summary>
    public Action FocusWindow { get; init; }
    /// <summary>判断原生窗口句柄是否有效</summary>
    public Func<nint, bool> IsWindowValid { get; init; }
    /// <summary>将焦点设置到原生控件</summary>
    public Action<nint> SetNativeFocus { get; init; }
    /// <summary>Shift 键是否按下（影响滚轮方向）</summary>
    public Func<bool> IsShiftKeyDown { get; init; }
}