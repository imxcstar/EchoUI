namespace EchoUI.Core;

/// <summary>
/// 平台无关的渲染节点基类：承载布局、视觉、事件、滚动与交互状态。
/// 平台后端可以通过继承添加原生资源句柄。
/// </summary>
public class RenderNode<TNode> : ILayoutNode<TNode>, IHitTestNode<TNode>
    where TNode : RenderNode<TNode>
{
    public RenderNode(string elementType)
    {
        ElementType = elementType;
    }

    public string ElementType { get; set; }

    public List<TNode> Children { get; } = [];

    public TNode? Parent { get; set; }

    public ComponentInstance? OwnerInstance { get; set; }

    // --- 布局结果 ---
    public float LayoutX { get; set; }
    public float LayoutY { get; set; }
    public float LayoutWidth { get; set; }
    public float LayoutHeight { get; set; }
    public float AbsoluteX { get; set; }
    public float AbsoluteY { get; set; }
    public LayoutBox AbsoluteBounds { get; private set; }

    // --- 尺寸属性 ---
    public Dimension? Width { get; set; }
    public Dimension? Height { get; set; }
    public Dimension? MinWidth { get; set; }
    public Dimension? MinHeight { get; set; }
    public Dimension? MaxWidth { get; set; }
    public Dimension? MaxHeight { get; set; }

    // --- 间距 ---
    public Spacing? Margin { get; set; }
    public Spacing? Padding { get; set; }

    // --- Flex 布局 ---
    public LayoutDirection Direction { get; set; } = LayoutDefaults.Direction;
    public JustifyContent JustifyContent { get; set; } = LayoutDefaults.JustifyContent;
    public AlignItems AlignItems { get; set; } = LayoutDefaults.AlignItems;
    public float FlexGrow { get; set; } = LayoutDefaults.FlexGrow;
    public float FlexShrink { get; set; } = LayoutDefaults.FlexShrink;
    public float Gap { get; set; } = LayoutDefaults.Gap;
    public bool Float { get; set; }
    public Overflow Overflow { get; set; } = LayoutDefaults.Overflow;

    // --- 外观 ---
    public Color? BackgroundColor { get; set; }
    public Color? BorderColor { get; set; }
    public Color? FocusedBorderColor { get; set; }
    public BorderStyle BorderStyle { get; set; } = BorderStyle.None;
    public float BorderWidth { get; set; }
    public float BorderRadius { get; set; }
    public BoxShadow Shadow { get; set; } = BoxShadow.None;
    public float Opacity { get; set; } = 1f;
    public Transform Transform { get; set; } = new();
    public TransformOrigin TransformOrigin { get; set; } = TransformOrigin.Center;
    public string? Cursor { get; set; }

    // --- 文本属性 ---
    public string? Text { get; set; }
    public string? FontFamily { get; set; }
    public float FontSize { get; set; } = 14;
    public Color? TextColor { get; set; }
    public string? FontWeight { get; set; }
    public bool MouseThrough { get; set; } = true;
    public bool NoWrap { get; set; }

    // --- Input 语义属性 ---
    public string? InputValue { get; set; }
    public Point? InputMethodAnchorPoint { get; set; }

    // --- 事件处理器 ---
    public Action<MouseButton>? OnClick { get; set; }
    public Action<Point>? OnMouseMove { get; set; }
    public Action<MouseEvent>? OnPointerDown { get; set; }
    public Action<MouseEvent>? OnPointerMove { get; set; }
    public Action<MouseEvent>? OnPointerUp { get; set; }
    public Action? OnMouseEnter { get; set; }
    public Action? OnMouseLeave { get; set; }
    public Action? OnMouseDown { get; set; }
    public Action? OnMouseUp { get; set; }
    public Action<int>? OnKeyDown { get; set; }
    public Action<int>? OnKeyUp { get; set; }
    public Action<string>? OnTextInput { get; set; }
    public Action<TextCompositionEvent>? OnTextComposition { get; set; }
    public Action? OnFocus { get; set; }
    public Action? OnBlur { get; set; }
    public Action<string>? OnValueChanged { get; set; }

    // --- 滚动 ---
    public float ScrollOffsetX { get; set; }
    public float ScrollOffsetY { get; set; }

    // --- 状态 ---
    public bool IsHovered { get; set; }
    public bool IsFocused { get; set; }

    // --- Layout cache ---
    public float CachedContentWidth { get; set; }
    public float CachedContentHeight { get; set; }
    public int IntrinsicWidthCacheVersion { get; set; } = -1;
    public float IntrinsicWidthCacheConstraint { get; set; }
    public float CachedIntrinsicWidth { get; set; }
    public int IntrinsicHeightCacheVersion { get; set; } = -1;
    public float IntrinsicHeightCacheConstraint { get; set; }
    public float CachedIntrinsicHeight { get; set; }

    public virtual nint EditHandle => 0;

    IReadOnlyList<TNode> ILayoutNode<TNode>.LayoutChildren => Children;

    void ILayoutNode<TNode>.CommitLayout() => CommitLayout();

    TNode? IHitTestNode<TNode>.HitParent => Parent;

    IReadOnlyList<TNode> IHitTestNode<TNode>.HitChildren => Children;

    nint IHitTestNode<TNode>.EditHandle => EditHandle;

    public virtual void CommitLayout()
    {
        AbsoluteBounds = new LayoutBox(AbsoluteX, AbsoluteY, LayoutWidth, LayoutHeight);
    }
}
