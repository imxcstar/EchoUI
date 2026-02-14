using System.Drawing;
using EchoUI.Core;

namespace EchoUI.Render.Win32
{
    /// <summary>
    /// 鼠标命中测试与事件分发。
    /// 从元素树中找到鼠标坐标命中的最深层元素，并分发事件。
    /// </summary>
    internal class HitTestManager
    {
        private Win32Element? _hoveredElement;
        private Win32Element? _pressedElement;
        private Win32Element? _focusedElement;
        private readonly Win32Renderer _renderer;

        public HitTestManager(Win32Renderer renderer)
        {
            _renderer = renderer;
        }

        /// <summary>
        /// 从根元素递归查找命中的最深层元素
        /// </summary>
        public Win32Element? HitTest(Win32Element root, float x, float y)
        {
            return HitTestRecursive(root, x, y);
        }

        private Win32Element? HitTestRecursive(Win32Element element, float x, float y)
        {
            var bounds = element.GetAbsoluteBounds();

            // 检查是否在元素边界内
            if (x < bounds.X || x > bounds.Right || y < bounds.Y || y > bounds.Bottom)
                return null;

            // 如果元素有 Overflow 裁剪，超出部分不可点击
            if (element.Overflow != Overflow.Visible)
            {
                if (x < bounds.X || x > bounds.Right || y < bounds.Y || y > bounds.Bottom)
                    return null;
            }

            // 从后往前遍历子元素（后绘制的在上层）
            for (int i = element.Children.Count - 1; i >= 0; i--)
            {
                var child = element.Children[i];

                // Text 元素如果 MouseThrough 为 true，跳过命中测试
                if (child.ElementType == ElementCoreName.Text && child.MouseThrough)
                    continue;

                var hit = HitTestRecursive(child, x, y);
                if (hit != null) return hit;
            }

            // 如果没有子元素命中，返回当前元素（如果它有事件处理器）
            if (HasEventHandler(element))
                return element;

            // 容器本身也可以被命中（用于接收事件）
            if (element.ElementType == ElementCoreName.Container)
                return element;

            return null;
        }

        private static bool HasEventHandler(Win32Element element)
        {
            return element.OnClick != null ||
                   element.OnMouseMove != null ||
                   element.OnMouseEnter != null ||
                   element.OnMouseLeave != null ||
                   element.OnMouseDown != null ||
                   element.OnMouseUp != null;
        }

        /// <summary>
        /// 处理鼠标移动事件
        /// </summary>
        public void HandleMouseMove(Win32Element root, float x, float y)
        {
            var hit = HitTest(root, x, y);

            // 处理 Enter/Leave
            if (hit != _hoveredElement)
            {
                // 向上遍历旧元素链，触发 Leave
                if (_hoveredElement != null)
                {
                    FireMouseLeaveChain(_hoveredElement, hit);
                }

                // 向上遍历新元素链，触发 Enter
                if (hit != null)
                {
                    FireMouseEnterChain(hit, _hoveredElement);
                }

                _hoveredElement = hit;
                _renderer.RequestRepaint();
            }

            // 触发 MouseMove
            if (hit?.OnMouseMove != null)
            {
                hit.OnMouseMove(new Core.Point((int)x, (int)y));
            }
        }

        /// <summary>
        /// 处理鼠标按下事件
        /// </summary>
        public void HandleMouseDown(Win32Element root, float x, float y, MouseButton button)
        {
            var hit = HitTest(root, x, y);
            _pressedElement = hit;

            if (hit != null)
            {
                // 设置焦点
                _focusedElement = hit;
                hit.OnMouseDown?.Invoke();
                _renderer.RequestRepaint();
            }
        }

        /// <summary>
        /// 处理鼠标释放事件
        /// </summary>
        public void HandleMouseUp(Win32Element root, float x, float y, MouseButton button)
        {
            var hit = HitTest(root, x, y);

            if (hit != null)
            {
                hit.OnMouseUp?.Invoke();

                // 如果按下和释放在同一个元素上，触发 Click
                if (hit == _pressedElement || IsAncestorOf(_pressedElement, hit))
                {
                    // 向上冒泡查找有 OnClick 的元素
                    var clickTarget = FindClickTarget(hit);
                    clickTarget?.OnClick?.Invoke(button);
                }

                _renderer.RequestRepaint();
            }

            _pressedElement = null;
        }

        /// <summary>
        /// 处理鼠标滚轮事件
        /// </summary>
        public void HandleMouseWheel(Win32Element root, float x, float y, int delta, float vpW, float vpH)
        {
            var hit = HitTest(root, x, y);
            var scrollTarget = FindScrollTarget(hit);

            if (scrollTarget != null)
            {
                float contentHeight = FlexLayout.MeasureContentHeight(scrollTarget, vpW, vpH);
                float maxScroll = Math.Max(0, contentHeight - scrollTarget.LayoutHeight);

                scrollTarget.ScrollOffsetY -= delta * 0.3f;
                scrollTarget.ScrollOffsetY = Math.Clamp(scrollTarget.ScrollOffsetY, 0, maxScroll);

                _renderer.RequestRelayout();
            }
        }

        /// <summary>
        /// 处理键盘按下事件
        /// </summary>
        public void HandleKeyDown(int keyCode)
        {
            _focusedElement?.OnKeyDown?.Invoke(keyCode);
        }

        /// <summary>
        /// 处理键盘释放事件
        /// </summary>
        public void HandleKeyUp(int keyCode)
        {
            _focusedElement?.OnKeyUp?.Invoke(keyCode);
        }

        /// <summary>
        /// 处理鼠标离开窗口
        /// </summary>
        public void HandleMouseLeave()
        {
            if (_hoveredElement != null)
            {
                FireMouseLeaveChain(_hoveredElement, null);
                _hoveredElement = null;
                _renderer.RequestRepaint();
            }
        }

        // --- 辅助方法 ---

        private void FireMouseLeaveChain(Win32Element from, Win32Element? to)
        {
            var current = from;
            while (current != null)
            {
                if (current == to) break;
                if (IsAncestorOf(current, to)) break;
                current.OnMouseLeave?.Invoke();
                current.IsHovered = false;
                current = current.Parent;
            }
        }

        private void FireMouseEnterChain(Win32Element to, Win32Element? from)
        {
            // 收集需要触发 Enter 的元素
            var chain = new List<Win32Element>();
            var current = to;
            while (current != null)
            {
                if (current == from) break;
                if (IsAncestorOf(current, from)) break;
                chain.Add(current);
                current = current.Parent;
            }

            // 从外到内触发 Enter
            for (int i = chain.Count - 1; i >= 0; i--)
            {
                chain[i].OnMouseEnter?.Invoke();
                chain[i].IsHovered = true;
            }
        }

        private static Win32Element? FindClickTarget(Win32Element? element)
        {
            var current = element;
            while (current != null)
            {
                if (current.OnClick != null) return current;
                current = current.Parent;
            }
            return null;
        }

        private static Win32Element? FindScrollTarget(Win32Element? element)
        {
            var current = element;
            while (current != null)
            {
                if (current.Overflow == Overflow.Auto || current.Overflow == Overflow.Scroll)
                    return current;
                current = current.Parent;
            }
            return null;
        }

        private static bool IsAncestorOf(Win32Element? ancestor, Win32Element? descendant)
        {
            if (ancestor == null || descendant == null) return false;
            var current = descendant.Parent;
            while (current != null)
            {
                if (current == ancestor) return true;
                current = current.Parent;
            }
            return false;
        }
    }
}
