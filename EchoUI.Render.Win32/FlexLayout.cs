using System.Drawing;
using EchoUI.Core;

namespace EchoUI.Render.Win32
{
    /// <summary>
    /// 简化版 Flexbox 布局引擎。
    /// 支持 Direction, JustifyContent, AlignItems, FlexGrow, FlexShrink, Gap, Padding, Margin,
    /// Width/Height (Pixels, Percent, ViewportHeight), Min/Max 约束, Float, Overflow。
    /// 没有指定尺寸的容器会被内容撑开（auto sizing）。
    /// </summary>
    internal static class FlexLayout
    {
        /// <summary>
        /// 从根元素开始计算整棵树的布局
        /// </summary>
        public static void ComputeLayout(Win32Element root, float viewportWidth, float viewportHeight)
        {
            root.LayoutX = 0;
            root.LayoutY = 0;
            root.LayoutWidth = viewportWidth;
            root.LayoutHeight = viewportHeight;
            root.AbsoluteX = 0;
            root.AbsoluteY = 0;

            LayoutChildren(root, viewportWidth, viewportHeight);
        }

        private static void LayoutChildren(Win32Element container, float vpW, float vpH)
        {
            if (container.Children.Count == 0) return;

            var padding = ResolveSpacing(container.Padding, container.LayoutWidth, vpW, vpH);
            float contentWidth = Math.Max(0, container.LayoutWidth - padding.Left - padding.Right);
            float contentHeight = Math.Max(0, container.LayoutHeight - padding.Top - padding.Bottom);

            bool isRow = container.Direction == LayoutDirection.Horizontal;
            float mainSize = isRow ? contentWidth : contentHeight;
            float crossSize = isRow ? contentHeight : contentWidth;
            float gap = container.Gap;

            // --- 第一遍：计算每个子元素的基础尺寸 ---
            var items = new List<FlexItem>(container.Children.Count);
            foreach (var child in container.Children)
            {
                if (child.Float)
                {
                    items.Add(new FlexItem { Element = child, IsFloat = true });
                    continue;
                }

                var margin = ResolveSpacing(child.Margin, isRow ? contentWidth : contentHeight, vpW, vpH);

                // 主轴尺寸
                float? explicitMain = isRow
                    ? ResolveSize(child.Width, contentWidth, vpW, vpH)
                    : ResolveSize(child.Height, contentHeight, vpW, vpH);

                float mainBase;
                if (explicitMain.HasValue)
                {
                    mainBase = explicitMain.Value;
                }
                else
                {
                    // 没有显式尺寸 → 测量内容固有尺寸
                    mainBase = isRow
                        ? MeasureIntrinsicWidth(child, contentHeight, vpW, vpH)
                        : MeasureIntrinsicHeight(child, contentWidth, vpW, vpH);
                }

                // 交叉轴尺寸
                float? explicitCross = isRow
                    ? ResolveSize(child.Height, contentHeight, vpW, vpH)
                    : ResolveSize(child.Width, contentWidth, vpW, vpH);

                float crossBase;
                if (explicitCross.HasValue)
                {
                    crossBase = explicitCross.Value;
                }
                else
                {
                    // 交叉轴默认拉伸填满（stretch），除非是 Text 元素
                    if (child.ElementType == ElementCoreName.Text)
                    {
                        crossBase = isRow
                            ? MeasureIntrinsicHeight(child, contentWidth, vpW, vpH)
                            : MeasureIntrinsicWidth(child, contentHeight, vpW, vpH);
                    }
                    else
                    {
                        float marginCross = isRow
                            ? margin.Top + margin.Bottom
                            : margin.Left + margin.Right;
                        crossBase = crossSize - marginCross;
                    }
                }

                if (crossBase < 0) crossBase = 0;
                if (mainBase < 0) mainBase = 0;

                items.Add(new FlexItem
                {
                    Element = child,
                    MainBase = mainBase,
                    CrossBase = crossBase,
                    Margin = margin,
                    Grow = child.FlexGrow,
                    Shrink = child.FlexShrink
                });
            }

            // --- 第二遍：分配 FlexGrow / FlexShrink ---
            var normalItems = items.Where(i => !i.IsFloat).ToList();
            int normalCount = normalItems.Count;
            float totalGaps = normalCount > 1 ? gap * (normalCount - 1) : 0;

            float usedMain = totalGaps;
            foreach (var item in normalItems)
            {
                float marginMain = isRow
                    ? item.Margin.Left + item.Margin.Right
                    : item.Margin.Top + item.Margin.Bottom;
                usedMain += item.MainBase + marginMain;
            }

            float freeSpace = mainSize - usedMain;

            if (freeSpace > 0)
            {
                float totalGrow = normalItems.Sum(i => i.Grow);
                if (totalGrow > 0)
                {
                    foreach (var item in normalItems)
                    {
                        if (item.Grow > 0)
                            item.MainBase += freeSpace * (item.Grow / totalGrow);
                    }
                }
            }
            else if (freeSpace < 0)
            {
                float totalShrink = normalItems.Sum(i => i.Shrink * i.MainBase);
                if (totalShrink > 0)
                {
                    foreach (var item in normalItems)
                    {
                        if (item.Shrink > 0)
                        {
                            float shrinkAmount = (-freeSpace) * (item.Shrink * item.MainBase / totalShrink);
                            item.MainBase = Math.Max(0, item.MainBase - shrinkAmount);
                        }
                    }
                }
            }

            // --- 应用 Min/Max 约束 ---
            foreach (var item in normalItems)
            {
                var child = item.Element;
                float? minMain, maxMain, minCross, maxCross;
                if (isRow)
                {
                    minMain = ResolveSize(child.MinWidth, contentWidth, vpW, vpH);
                    maxMain = ResolveSize(child.MaxWidth, contentWidth, vpW, vpH);
                    minCross = ResolveSize(child.MinHeight, contentHeight, vpW, vpH);
                    maxCross = ResolveSize(child.MaxHeight, contentHeight, vpW, vpH);
                }
                else
                {
                    minMain = ResolveSize(child.MinHeight, contentHeight, vpW, vpH);
                    maxMain = ResolveSize(child.MaxHeight, contentHeight, vpW, vpH);
                    minCross = ResolveSize(child.MinWidth, contentWidth, vpW, vpH);
                    maxCross = ResolveSize(child.MaxWidth, contentWidth, vpW, vpH);
                }
                if (minMain.HasValue) item.MainBase = Math.Max(item.MainBase, minMain.Value);
                if (maxMain.HasValue) item.MainBase = Math.Min(item.MainBase, maxMain.Value);
                if (minCross.HasValue) item.CrossBase = Math.Max(item.CrossBase, minCross.Value);
                if (maxCross.HasValue) item.CrossBase = Math.Min(item.CrossBase, maxCross.Value);
            }

            // --- 第三遍：JustifyContent 定位 ---
            float actualUsedMain = totalGaps + normalItems.Sum(i =>
            {
                float marginMain = isRow
                    ? i.Margin.Left + i.Margin.Right
                    : i.Margin.Top + i.Margin.Bottom;
                return i.MainBase + marginMain;
            });

            float remainingSpace = Math.Max(0, mainSize - actualUsedMain);
            float mainOffset = 0;
            float spaceBetween = 0;

            switch (container.JustifyContent)
            {
                case JustifyContent.Start:
                    break;
                case JustifyContent.Center:
                    mainOffset = remainingSpace / 2;
                    break;
                case JustifyContent.End:
                    mainOffset = remainingSpace;
                    break;
                case JustifyContent.SpaceBetween:
                    if (normalCount > 1)
                        spaceBetween = remainingSpace / (normalCount - 1);
                    break;
                case JustifyContent.SpaceAround:
                    if (normalCount > 0)
                    {
                        float space = remainingSpace / normalCount;
                        mainOffset = space / 2;
                        spaceBetween = space;
                    }
                    break;
            }

            // --- 第四遍：放置子元素 ---
            float cursor = mainOffset;
            int normalIndex = 0;
            foreach (var item in items)
            {
                var child = item.Element;

                if (item.IsFloat)
                {
                    child.LayoutWidth = contentWidth;
                    child.LayoutHeight = 0;
                    child.LayoutX = padding.Left;
                    child.LayoutY = padding.Top + cursor;
                    child.AbsoluteX = container.AbsoluteX + child.LayoutX;
                    child.AbsoluteY = container.AbsoluteY + child.LayoutY;
                    LayoutChildren(child, vpW, vpH);
                    continue;
                }

                float marginMainStart = isRow ? item.Margin.Left : item.Margin.Top;
                float marginMainEnd = isRow ? item.Margin.Right : item.Margin.Bottom;
                float marginCrossStart = isRow ? item.Margin.Top : item.Margin.Left;
                float marginCrossEnd = isRow ? item.Margin.Bottom : item.Margin.Right;

                float mainPos = cursor + marginMainStart;

                // AlignItems 交叉轴定位
                float availableCross = crossSize - marginCrossStart - marginCrossEnd;
                float childCross = Math.Min(item.CrossBase, Math.Max(0, availableCross));

                float crossPos;
                switch (container.AlignItems)
                {
                    case AlignItems.Center:
                        crossPos = marginCrossStart + (availableCross - childCross) / 2;
                        break;
                    case AlignItems.End:
                        crossPos = marginCrossStart + availableCross - childCross;
                        break;
                    default:
                        crossPos = marginCrossStart;
                        break;
                }

                if (isRow)
                {
                    child.LayoutX = padding.Left + mainPos;
                    child.LayoutY = padding.Top + crossPos;
                    child.LayoutWidth = item.MainBase;
                    child.LayoutHeight = childCross;
                }
                else
                {
                    child.LayoutX = padding.Left + crossPos;
                    child.LayoutY = padding.Top + mainPos;
                    child.LayoutWidth = childCross;
                    child.LayoutHeight = item.MainBase;
                }

                child.AbsoluteX = container.AbsoluteX + child.LayoutX;
                child.AbsoluteY = container.AbsoluteY + child.LayoutY;

                if (container.ScrollOffsetY != 0)
                {
                    child.AbsoluteY -= container.ScrollOffsetY;
                }

                cursor = mainPos + item.MainBase + marginMainEnd + gap;
                if (normalIndex < normalCount - 1 &&
                    (container.JustifyContent == JustifyContent.SpaceBetween ||
                     container.JustifyContent == JustifyContent.SpaceAround))
                {
                    cursor += spaceBetween;
                }
                normalIndex++;

                // 递归布局子元素
                LayoutChildren(child, vpW, vpH);
            }
        }

        /// <summary>
        /// 计算元素内容的总高度（用于滚动）
        /// </summary>
        public static float MeasureContentHeight(Win32Element container, float vpW, float vpH)
        {
            if (container.Children.Count == 0) return 0;

            var padding = ResolveSpacing(container.Padding, container.LayoutWidth, vpW, vpH);
            bool isRow = container.Direction == LayoutDirection.Horizontal;
            float total = 0;
            int count = 0;

            foreach (var child in container.Children)
            {
                if (child.Float) continue;
                if (isRow)
                {
                    total = Math.Max(total, child.LayoutHeight);
                }
                else
                {
                    total += child.LayoutHeight;
                    count++;
                }
            }

            if (!isRow && count > 1)
                total += container.Gap * (count - 1);

            return total + padding.Top + padding.Bottom;
        }

        // --- 尺寸解析 ---

        private static float? ResolveSize(Dimension? dim, float parentSize, float vpW, float vpH)
        {
            if (!dim.HasValue) return null;
            return dim.Value.Unit switch
            {
                DimensionUnit.Pixels => dim.Value.Value,
                DimensionUnit.Percent => parentSize * dim.Value.Value / 100f,
                DimensionUnit.ViewportHeight => vpH * dim.Value.Value / 100f,
                _ => null
            };
        }

        // --- 固有尺寸测量（auto sizing） ---

        /// <summary>
        /// 测量元素的固有宽度。容器会递归测量子元素。
        /// </summary>
        private static float MeasureIntrinsicWidth(Win32Element element, float availableHeight, float vpW, float vpH)
        {
            if (element.ElementType == ElementCoreName.Text)
                return MeasureTextWidth(element);

            if (element.ElementType == ElementCoreName.Input)
                return 100; // Input 默认宽度

            // 容器：递归测量子元素
            if (element.Children.Count == 0) return 0;

            var padding = ResolveSpacing(element.Padding, 0, vpW, vpH);
            bool isRow = element.Direction == LayoutDirection.Horizontal;
            float gap = element.Gap;
            float result = 0;
            int count = 0;

            foreach (var child in element.Children)
            {
                if (child.Float) continue;
                float childW = ResolveSize(child.Width, 0, vpW, vpH)
                               ?? MeasureIntrinsicWidth(child, availableHeight, vpW, vpH);
                var margin = ResolveSpacing(child.Margin, 0, vpW, vpH);
                float totalChild = childW + margin.Left + margin.Right;

                if (isRow)
                {
                    result += totalChild;
                    count++;
                }
                else
                {
                    result = Math.Max(result, totalChild);
                    count++;
                }
            }

            if (isRow && count > 1)
                result += gap * (count - 1);

            return result + padding.Left + padding.Right;
        }

        /// <summary>
        /// 测量元素的固有高度。容器会递归测量子元素。
        /// </summary>
        private static float MeasureIntrinsicHeight(Win32Element element, float availableWidth, float vpW, float vpH)
        {
            if (element.ElementType == ElementCoreName.Text)
                return MeasureTextHeight(element);

            if (element.ElementType == ElementCoreName.Input)
                return 24; // Input 默认高度

            // 容器：递归测量子元素
            if (element.Children.Count == 0) return 0;

            var padding = ResolveSpacing(element.Padding, 0, vpW, vpH);
            bool isRow = element.Direction == LayoutDirection.Horizontal;
            float gap = element.Gap;
            float result = 0;
            int count = 0;

            foreach (var child in element.Children)
            {
                if (child.Float) continue;
                float childH = ResolveSize(child.Height, 0, vpW, vpH)
                               ?? MeasureIntrinsicHeight(child, availableWidth, vpW, vpH);
                var margin = ResolveSpacing(child.Margin, 0, vpW, vpH);
                float totalChild = childH + margin.Top + margin.Bottom;

                if (isRow)
                {
                    result = Math.Max(result, totalChild);
                    count++;
                }
                else
                {
                    result += totalChild;
                    count++;
                }
            }

            if (!isRow && count > 1)
                result += gap * (count - 1);

            return result + padding.Top + padding.Bottom;
        }

        private static float MeasureTextWidth(Win32Element element)
        {
            if (string.IsNullOrEmpty(element.Text)) return 0;
            float fontSize = element.FontSize > 0 ? element.FontSize : 14;
            float width = 0;
            foreach (char c in element.Text)
            {
                if (c > 0x7F)
                    width += fontSize;
                else
                    width += fontSize * 0.6f;
            }
            return width + 4;
        }

        private static float MeasureTextHeight(Win32Element element)
        {
            float fontSize = element.FontSize > 0 ? element.FontSize : 14;
            return fontSize * 1.4f;
        }

        private static (float Left, float Top, float Right, float Bottom) ResolveSpacing(
            Spacing? spacing, float parentSize, float vpW, float vpH)
        {
            if (!spacing.HasValue) return (0, 0, 0, 0);
            var s = spacing.Value;
            return (
                ResolveSize(s.Left, parentSize, vpW, vpH) ?? 0,
                ResolveSize(s.Top, parentSize, vpW, vpH) ?? 0,
                ResolveSize(s.Right, parentSize, vpW, vpH) ?? 0,
                ResolveSize(s.Bottom, parentSize, vpW, vpH) ?? 0
            );
        }

        private class FlexItem
        {
            public Win32Element Element { get; set; } = null!;
            public float MainBase { get; set; }
            public float CrossBase { get; set; }
            public (float Left, float Top, float Right, float Bottom) Margin { get; set; }
            public float Grow { get; set; }
            public float Shrink { get; set; }
            public bool IsFloat { get; set; }
        }
    }
}
