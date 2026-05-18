namespace EchoUI.Core;

/// <summary>
/// 平台无关的命中测试与事件分发引擎。
/// 从元素树中找到坐标命中的最深层元素，管理 hover/focus/pressed 状态，分发鼠标/键盘/IME 事件。
/// </summary>
public class HitTestManager<TNode> where TNode : class, IHitTestNode<TNode>
{
    private TNode? _hoveredElement;
    private TNode? _pressedClickTarget;
    private TNode? _focusedElement;
    private MouseButton? _pressedButton;
    private int _suppressedCommittedCharCount;
    private readonly HitTestPlatform<TNode> _platform;

    public TNode? FocusedElement => _focusedElement;

    public HitTestManager(HitTestPlatform<TNode> platform)
    {
        _platform = platform;
    }

    // ──────────────── 命中测试 ────────────────

    /// <summary>
    /// 从根元素递归查找命中的最深层元素
    /// </summary>
    public TNode? HitTest(TNode root, float x, float y)
    {
        var floats = _platform.GetFloatingElements();
        if (floats.Count > 0)
        {
            for (int i = floats.Count - 1; i >= 0; i--)
            {
                var hit = HitTestRecursive(floats[i], x, y);
                if (hit != null)
                    return hit;
            }
        }

        var localHit = HitTestFromHoveredChain(x, y);
        if (localHit != null)
            return localHit;

        return HitTestRecursive(root, x, y);
    }

    private TNode? HitTestRecursive(TNode element, float x, float y)
    {
        float right = element.AbsoluteX + element.LayoutWidth;
        float bottom = element.AbsoluteY + element.LayoutHeight;

        bool isInBounds = x >= element.AbsoluteX && x <= right && y >= element.AbsoluteY && y <= bottom;
        bool canHitOutsideBounds = element.Overflow == Overflow.Visible || element.Float;

        if (!isInBounds && !canHitOutsideBounds)
            return null;

        // 先检查 Float 子元素
        var children = element.HitChildren;
        for (int i = children.Count - 1; i >= 0; i--)
        {
            var child = children[i];
            if (!child.Float) continue;

            var hit = HitTestRecursive(child, x, y);
            if (hit != null) return hit;
        }

        // 从后往前遍历非 Float 子元素
        for (int i = children.Count - 1; i >= 0; i--)
        {
            var child = children[i];
            if (child.Float) continue;

            if (child.ElementType == ElementCoreName.Text && child.MouseThrough)
                continue;

            var hit = HitTestRecursive(child, x, y);
            if (hit != null) return hit;
        }

        if (!isInBounds)
            return null;

        if (HasEventHandler(element))
            return element;

        if (element.ElementType == ElementCoreName.Input)
            return element;

        if (element.ElementType == ElementCoreName.Container)
            return element;

        if (element.ElementType == ElementCoreName.Text && !element.MouseThrough)
            return element;

        return null;
    }

    private static bool HasEventHandler(TNode element)
    {
        return element.OnClick != null ||
               element.OnMouseMove != null ||
               element.OnMouseEnter != null ||
               element.OnMouseLeave != null ||
               element.OnMouseDown != null ||
               element.OnMouseUp != null;
    }

    // ──────────────── 鼠标移动 ────────────────

    public void HandleMouseMove(TNode root, float x, float y)
    {
        var hit = HitTest(root, x, y);

        if (hit != _hoveredElement)
        {
            var oldHovered = _hoveredElement;
            var commonAncestor = FindCommonAncestor(oldHovered, hit);

            if (oldHovered != null)
                FireMouseLeaveChain(oldHovered, commonAncestor);

            if (hit != null)
                FireMouseEnterChain(hit, commonAncestor);

            _hoveredElement = hit;
            _platform.RequestRepaint(oldHovered, hit);
        }

        var moveTarget = FindMoveHandler(hit);
        if (moveTarget != null)
        {
            var localPoint = ToLocalPoint(moveTarget, x, y);
            moveTarget.OnMouseMove?.Invoke(localPoint);
            moveTarget.OnPointerMove?.Invoke(new MouseEvent(localPoint, _pressedButton ?? MouseButton.Left));
        }
    }

    // ──────────────── 鼠标按下 ────────────────

    public void HandleMouseDown(TNode root, float x, float y, MouseButton button)
    {
        var hit = HitTest(root, x, y);

        // 交叉验证：hover 链在滚动后可能返回过时元素
        if (hit != null && _hoveredElement != null && !IsFloatDescendant(hit))
        {
            var mainHit = HitTestRecursive(root, x, y);
            if (mainHit != null && !ReferenceEquals(hit, mainHit))
            {
                if (_hoveredElement != mainHit)
                {
                    var oldHovered = _hoveredElement;
                    var commonAncestor = FindCommonAncestor(oldHovered, mainHit);
                    if (oldHovered != null)
                        FireMouseLeaveChain(oldHovered, commonAncestor);
                    if (mainHit != null)
                        FireMouseEnterChain(mainHit, commonAncestor);
                    _hoveredElement = mainHit;
                }
                hit = mainHit;
            }
        }

        _pressedButton = button;
        _pressedClickTarget = FindClickHandler(hit);

        SetFocusedElement(FindFocusableElement(hit));

        if (hit != null)
        {
            var downTarget = FindDownHandler(hit);
            if (downTarget != null)
            {
                var localPoint = ToLocalPoint(downTarget, x, y);
                downTarget.OnMouseDown?.Invoke();
                downTarget.OnPointerDown?.Invoke(new MouseEvent(localPoint, button));
            }
            _platform.RequestRepaint(hit, null);
        }
    }

    // ──────────────── 鼠标释放 ────────────────

    public void HandleMouseUp(TNode root, float x, float y, MouseButton button)
    {
        var hit = HitTest(root, x, y);

        if (hit != null)
        {
            var upTarget = FindUpHandler(hit);
            if (upTarget != null)
            {
                var localPoint = ToLocalPoint(upTarget, x, y);
                upTarget.OnMouseUp?.Invoke();
                upTarget.OnPointerUp?.Invoke(new MouseEvent(localPoint, button));
            }

            var releaseClickTarget = FindClickHandler(hit);
            if (releaseClickTarget != null && ReferenceEquals(releaseClickTarget, _pressedClickTarget))
            {
                releaseClickTarget.OnClick?.Invoke(button);
            }

            _platform.RequestRepaint(hit, _pressedClickTarget);
        }

        _pressedButton = null;
        _pressedClickTarget = null;
    }

    // ──────────────── 鼠标滚轮 ────────────────

    public void HandleMouseWheel(TNode root, float x, float y, int delta)
    {
        var hit = HitTest(root, x, y);
        var scrollTarget = FindScrollTarget(hit);

        if (scrollTarget != null)
        {
            float maxScrollX = Math.Max(0, scrollTarget.CachedContentWidth - scrollTarget.LayoutWidth);
            float maxScrollY = Math.Max(0, scrollTarget.CachedContentHeight - scrollTarget.LayoutHeight);
            bool scrollHorizontal = _platform.IsShiftKeyDown() || maxScrollY <= 0;
            float previousScrollX = scrollTarget.ScrollOffsetX;
            float previousScrollY = scrollTarget.ScrollOffsetY;

            if (scrollHorizontal && maxScrollX > 0)
            {
                scrollTarget.ScrollOffsetX -= delta * 0.3f;
                scrollTarget.ScrollOffsetX = Math.Clamp(scrollTarget.ScrollOffsetX, 0, maxScrollX);
            }
            else if (maxScrollY > 0)
            {
                scrollTarget.ScrollOffsetY -= delta * 0.3f;
                scrollTarget.ScrollOffsetY = Math.Clamp(scrollTarget.ScrollOffsetY, 0, maxScrollY);
            }

            if (!previousScrollX.Equals(scrollTarget.ScrollOffsetX) || !previousScrollY.Equals(scrollTarget.ScrollOffsetY))
            {
                _platform.RequestRelayout();

                if (_hoveredElement != null)
                {
                    FireMouseLeaveChain(_hoveredElement, null);
                    _hoveredElement = null;
                }
            }
        }
    }

    // ──────────────── 键盘 / 文本 / IME ────────────────

    public void HandleKeyDown(int keyCode)
    {
        _focusedElement?.OnKeyDown?.Invoke(keyCode);
    }

    public void HandleKeyUp(int keyCode)
    {
        _focusedElement?.OnKeyUp?.Invoke(keyCode);
    }

    public void HandleTextInput(uint charCode)
    {
        if (_suppressedCommittedCharCount > 0 && charCode >= 32)
        {
            _suppressedCommittedCharCount--;
            return;
        }

        _focusedElement?.OnTextInput?.Invoke(new string((char)charCode, 1));
    }

    public void HandleTextComposition(TextCompositionEvent compositionEvent)
    {
        if (compositionEvent.Phase == TextCompositionPhase.Commit && !string.IsNullOrEmpty(compositionEvent.Text))
        {
            _suppressedCommittedCharCount += compositionEvent.Text.Length;
        }

        _focusedElement?.OnTextComposition?.Invoke(compositionEvent);
    }

    // ──────────────── 鼠标离开窗口 ────────────────

    public void HandleMouseLeave()
    {
        if (_hoveredElement != null)
        {
            var oldHovered = _hoveredElement;
            FireMouseLeaveChain(oldHovered, null);
            _hoveredElement = null;
            _platform.RequestRepaint(oldHovered, null);
        }
    }

    // ──────────────── 子树卸载 ────────────────

    /// <summary>
    /// 当一棵子树被从元素树中移除时，清理可能指向子树内部元素的 hover/focus/pressed 状态。
    /// </summary>
    public void DetachSubtree(TNode subtreeRoot)
    {
        if (_hoveredElement != null && IsInSubtree(subtreeRoot, _hoveredElement))
        {
            var oldHovered = _hoveredElement;
            FireMouseLeaveChain(oldHovered, null);
            _hoveredElement = null;
            _platform.RequestRepaint(oldHovered, null);
        }

        if (_pressedClickTarget != null && IsInSubtree(subtreeRoot, _pressedClickTarget))
        {
            _pressedClickTarget = null;
            _pressedButton = null;
        }

        if (_focusedElement != null && IsInSubtree(subtreeRoot, _focusedElement))
        {
            SetFocusedElement(null);
        }
    }

    // ──────────────── 辅助方法 ────────────────

    private void FireMouseLeaveChain(TNode from, TNode? stopAt)
    {
        var current = from;
        while (current != null && !ReferenceEquals(current, stopAt))
        {
            current.OnMouseLeave?.Invoke();
            current.IsHovered = false;
            current = current.HitParent;
        }
    }

    private void FireMouseEnterChain(TNode to, TNode? stopAt)
    {
        var chain = new List<TNode>();
        var current = to;
        while (current != null && !ReferenceEquals(current, stopAt))
        {
            chain.Add(current);
            current = current.HitParent;
        }

        for (int i = chain.Count - 1; i >= 0; i--)
        {
            chain[i].OnMouseEnter?.Invoke();
            chain[i].IsHovered = true;
        }
    }

    private TNode? HitTestFromHoveredChain(float x, float y)
    {
        var current = _hoveredElement?.HitParent;
        while (current != null)
        {
            if (!current.Float)
            {
                float right = current.AbsoluteX + current.LayoutWidth;
                float bottom = current.AbsoluteY + current.LayoutHeight;
                if (x >= current.AbsoluteX && x <= right && y >= current.AbsoluteY && y <= bottom)
                {
                    var hit = HitTestRecursive(current, x, y);
                    if (hit != null)
                        return hit;
                }
            }
            current = current.HitParent;
        }
        return null;
    }

    private static TNode? FindCommonAncestor(TNode? first, TNode? second)
    {
        var firstDepth = GetDepth(first);
        var secondDepth = GetDepth(second);

        while (firstDepth > secondDepth && first != null)
        {
            first = first.HitParent;
            firstDepth--;
        }

        while (secondDepth > firstDepth && second != null)
        {
            second = second.HitParent;
            secondDepth--;
        }

        while (!ReferenceEquals(first, second))
        {
            first = first?.HitParent;
            second = second?.HitParent;
        }

        return first;
    }

    private static int GetDepth(TNode? element)
    {
        int depth = 0;
        var current = element;
        while (current != null)
        {
            depth++;
            current = current.HitParent;
        }
        return depth;
    }

    private static bool IsInSubtree(TNode subtreeRoot, TNode? element)
    {
        var current = element;
        while (current != null)
        {
            if (ReferenceEquals(current, subtreeRoot))
                return true;
            current = current.HitParent;
        }
        return false;
    }

    private static TNode? FindMoveHandler(TNode? element)
    {
        var current = element;
        while (current != null)
        {
            if (current.OnMouseMove != null || current.OnPointerMove != null)
                return current;
            current = current.HitParent;
        }
        return null;
    }

    private static TNode? FindClickHandler(TNode? element)
    {
        var current = element;
        while (current != null)
        {
            if (current.OnClick != null)
                return current;
            current = current.HitParent;
        }
        return null;
    }

    private static bool IsFloatDescendant(TNode? element)
    {
        var current = element;
        while (current != null)
        {
            if (current.Float)
                return true;
            current = current.HitParent;
        }
        return false;
    }

    private static TNode? FindDownHandler(TNode? element)
    {
        var current = element;
        while (current != null)
        {
            if (current.OnMouseDown != null || current.OnPointerDown != null)
                return current;
            current = current.HitParent;
        }
        return null;
    }

    private static TNode? FindUpHandler(TNode? element)
    {
        var current = element;
        while (current != null)
        {
            if (current.OnMouseUp != null || current.OnPointerUp != null)
                return current;
            current = current.HitParent;
        }
        return null;
    }

    private static TNode? FindFocusableElement(TNode? element)
    {
        var current = element;
        while (current != null)
        {
            if (current.ElementType == ElementCoreName.Input ||
                current.OnKeyDown != null ||
                current.OnKeyUp != null ||
                current.OnTextInput != null ||
                current.OnTextComposition != null ||
                current.OnFocus != null ||
                current.OnBlur != null)
            {
                return current;
            }
            current = current.HitParent;
        }
        return null;
    }

    private void SetFocusedElement(TNode? element)
    {
        if (ReferenceEquals(_focusedElement, element))
        {
            if (element?.ElementType == ElementCoreName.Input && element.EditHandle != 0 && _platform.IsWindowValid(element.EditHandle))
            {
                _platform.SetNativeFocus(element.EditHandle);
            }
            else if (element != null)
            {
                _platform.FocusWindow();
            }
            return;
        }

        var oldFocused = _focusedElement;
        if (oldFocused != null)
        {
            oldFocused.IsFocused = false;
            oldFocused.OnBlur?.Invoke();
        }

        _focusedElement = element;

        if (_focusedElement != null)
        {
            _focusedElement.IsFocused = true;

            if (_focusedElement.ElementType == ElementCoreName.Input && _focusedElement.EditHandle != 0 && _platform.IsWindowValid(_focusedElement.EditHandle))
            {
                _platform.SetNativeFocus(_focusedElement.EditHandle);
            }
            else
            {
                _platform.FocusWindow();
            }

            _focusedElement.OnFocus?.Invoke();
        }

        _platform.RequestRepaint(oldFocused, _focusedElement);
    }

    private static Point ToLocalPoint(TNode element, float x, float y)
    {
        return new Point(
            (int)Math.Round(x - element.AbsoluteX, MidpointRounding.AwayFromZero),
            (int)Math.Round(y - element.AbsoluteY, MidpointRounding.AwayFromZero));
    }

    private static TNode? FindScrollTarget(TNode? element)
    {
        var current = element;
        while (current != null)
        {
            if (current.Overflow == Overflow.Auto || current.Overflow == Overflow.Scroll)
                return current;
            current = current.HitParent;
        }
        return null;
    }
}