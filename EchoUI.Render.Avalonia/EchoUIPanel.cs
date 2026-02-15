using Avalonia;
using Avalonia.Controls;
using EchoUI.Core;

namespace EchoUI.Render.Avalonia;

/// <summary>
/// 自定义 Flexbox 布局面板，支持 Direction、JustifyContent、AlignItems、Gap、FlexGrow、FlexShrink。
/// 通过 AttachedProperty 存储每个子元素的 flex 属性。
/// </summary>
public class EchoUIPanel : Panel
{
    // --- 面板自身属性 ---

    public static readonly StyledProperty<LayoutDirection> DirectionProperty =
        AvaloniaProperty.Register<EchoUIPanel, LayoutDirection>(nameof(Direction), LayoutDirection.Vertical);

    public static readonly StyledProperty<JustifyContent> JustifyContentProperty =
        AvaloniaProperty.Register<EchoUIPanel, JustifyContent>(nameof(JustifyContent), JustifyContent.Start);

    public static readonly StyledProperty<AlignItems> AlignItemsProperty =
        AvaloniaProperty.Register<EchoUIPanel, AlignItems>(nameof(AlignItems), AlignItems.Start);

    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<EchoUIPanel, double>(nameof(Gap), 0);

    public LayoutDirection Direction
    {
        get => GetValue(DirectionProperty);
        set => SetValue(DirectionProperty, value);
    }

    public JustifyContent JustifyContent
    {
        get => GetValue(JustifyContentProperty);
        set => SetValue(JustifyContentProperty, value);
    }

    public AlignItems AlignItems
    {
        get => GetValue(AlignItemsProperty);
        set => SetValue(AlignItemsProperty, value);
    }

    public double Gap
    {
        get => GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    // --- 子元素附加属性 ---

    public static readonly AttachedProperty<float> FlexGrowProperty =
        AvaloniaProperty.RegisterAttached<EchoUIPanel, Control, float>("FlexGrow", 0);

    public static readonly AttachedProperty<float> FlexShrinkProperty =
        AvaloniaProperty.RegisterAttached<EchoUIPanel, Control, float>("FlexShrink", 0);

    public static readonly AttachedProperty<bool> IsFloatProperty =
        AvaloniaProperty.RegisterAttached<EchoUIPanel, Control, bool>("IsFloat", false);

    /// <summary>
    /// 存储原始的 EchoUI Dimension，用于在布局时解析百分比等单位。
    /// </summary>
    public static readonly AttachedProperty<Dimension?> EchoWidthProperty =
        AvaloniaProperty.RegisterAttached<EchoUIPanel, Control, Dimension?>("EchoWidth");

    public static readonly AttachedProperty<Dimension?> EchoHeightProperty =
        AvaloniaProperty.RegisterAttached<EchoUIPanel, Control, Dimension?>("EchoHeight");

    public static float GetFlexGrow(Control control) => control.GetValue(FlexGrowProperty);
    public static void SetFlexGrow(Control control, float value) => control.SetValue(FlexGrowProperty, value);

    public static float GetFlexShrink(Control control) => control.GetValue(FlexShrinkProperty);
    public static void SetFlexShrink(Control control, float value) => control.SetValue(FlexShrinkProperty, value);

    public static bool GetIsFloat(Control control) => control.GetValue(IsFloatProperty);
    public static void SetIsFloat(Control control, bool value) => control.SetValue(IsFloatProperty, value);

    public static Dimension? GetEchoWidth(Control control) => control.GetValue(EchoWidthProperty);
    public static void SetEchoWidth(Control control, Dimension? value) => control.SetValue(EchoWidthProperty, value);

    public static Dimension? GetEchoHeight(Control control) => control.GetValue(EchoHeightProperty);
    public static void SetEchoHeight(Control control, Dimension? value) => control.SetValue(EchoHeightProperty, value);

    static EchoUIPanel()
    {
        AffectsMeasure<EchoUIPanel>(DirectionProperty, JustifyContentProperty, AlignItemsProperty, GapProperty);
        AffectsArrange<EchoUIPanel>(DirectionProperty, JustifyContentProperty, AlignItemsProperty, GapProperty);
    }

    /// <summary>
    /// 解析 Dimension 到实际像素值。parentSize 是父容器在该轴上的可用空间。
    /// </summary>
    private static double? ResolveDim(Dimension? dim, double parentSize, double viewportHeight)
    {
        if (!dim.HasValue) return null;
        return dim.Value.Unit switch
        {
            DimensionUnit.Pixels => dim.Value.Value,
            DimensionUnit.Percent => parentSize * dim.Value.Value / 100.0,
            DimensionUnit.ViewportHeight => viewportHeight * dim.Value.Value / 100.0,
            _ => null
        };
    }

    private double GetViewportHeight()
    {
        // 向上遍历找到窗口高度
        var root = this.VisualRoot;
        if (root is global::Avalonia.Controls.Window window)
            return window.ClientSize.Height;
        if (root is global::Avalonia.Controls.TopLevel topLevel)
            return topLevel.ClientSize.Height;
        return 800; // fallback
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        bool isRow = Direction == LayoutDirection.Horizontal;
        double gap = Gap;
        double mainTotal = 0;
        double crossMax = 0;
        int normalCount = 0;
        double vpH = GetViewportHeight();

        double availMain = isRow ? availableSize.Width : availableSize.Height;
        double availCross = isRow ? availableSize.Height : availableSize.Width;

        foreach (var child in Children)
        {
            if (!child.IsVisible) continue;
            if (GetIsFloat(child))
            {
                // Float 元素用完整可用空间测量
                child.Measure(availableSize);
                continue;
            }

            // 解析子元素的 EchoUI 尺寸
            var echoW = GetEchoWidth(child);
            var echoH = GetEchoHeight(child);

            double? resolvedW = ResolveDim(echoW, availableSize.Width, vpH);
            double? resolvedH = ResolveDim(echoH, availableSize.Height, vpH);

            // 构建子元素的测量约束
            double constraintW = resolvedW ?? availableSize.Width;
            double constraintH = resolvedH ?? availableSize.Height;

            if (double.IsInfinity(constraintW)) constraintW = availableSize.Width;
            if (double.IsInfinity(constraintH)) constraintH = availableSize.Height;

            child.Measure(new Size(constraintW, constraintH));
            var desired = child.DesiredSize;

            // 如果有显式尺寸，使用解析后的值
            double childMain, childCross;
            if (isRow)
            {
                childMain = resolvedW ?? desired.Width;
                childCross = resolvedH ?? desired.Height;
            }
            else
            {
                childMain = resolvedH ?? desired.Height;
                childCross = resolvedW ?? desired.Width;
            }

            mainTotal += childMain;
            crossMax = Math.Max(crossMax, childCross);
            normalCount++;
        }

        if (normalCount > 1)
            mainTotal += gap * (normalCount - 1);

        return isRow
            ? new Size(mainTotal, crossMax)
            : new Size(crossMax, mainTotal);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        bool isRow = Direction == LayoutDirection.Horizontal;
        double gap = Gap;
        double mainSize = isRow ? finalSize.Width : finalSize.Height;
        double crossSize = isRow ? finalSize.Height : finalSize.Width;
        double vpH = GetViewportHeight();

        // 收集非 float 子元素信息
        var items = new List<FlexItem>();
        foreach (var child in Children)
        {
            if (!child.IsVisible) continue;
            if (GetIsFloat(child))
            {
                // Float 元素：给它完整的父容器宽度，但不限制高度（让内容撑开）
                // 先用完整宽度重新测量以获取内容高度
                child.Measure(new Size(finalSize.Width, double.PositiveInfinity));
                var floatDesired = child.DesiredSize;
                child.Arrange(new Rect(0, 0, finalSize.Width, Math.Max(floatDesired.Height, finalSize.Height)));
                continue;
            }

            var echoW = GetEchoWidth(child);
            var echoH = GetEchoHeight(child);

            double? resolvedW = ResolveDim(echoW, finalSize.Width, vpH);
            double? resolvedH = ResolveDim(echoH, finalSize.Height, vpH);

            // 主轴基础尺寸
            double mainBase;
            if (isRow)
                mainBase = resolvedW ?? child.DesiredSize.Width;
            else
                mainBase = resolvedH ?? child.DesiredSize.Height;

            // 交叉轴基础尺寸
            double crossBase;
            if (isRow)
                crossBase = resolvedH ?? child.DesiredSize.Height;
            else
                crossBase = resolvedW ?? child.DesiredSize.Width;

            items.Add(new FlexItem
            {
                Control = child,
                MainBase = mainBase,
                CrossBase = crossBase,
                Grow = GetFlexGrow(child),
                Shrink = GetFlexShrink(child),
                HasExplicitCross = isRow ? resolvedH.HasValue : resolvedW.HasValue,
            });
        }

        if (items.Count == 0) return finalSize;

        int normalCount = items.Count;
        double totalGaps = normalCount > 1 ? gap * (normalCount - 1) : 0;

        // 计算已用主轴空间
        double usedMain = totalGaps;
        foreach (var item in items)
            usedMain += item.MainBase;

        double freeSpace = mainSize - usedMain;

        // FlexGrow / FlexShrink 分配
        if (freeSpace > 0)
        {
            double totalGrow = 0;
            foreach (var item in items) totalGrow += item.Grow;
            if (totalGrow > 0)
            {
                foreach (var item in items)
                {
                    if (item.Grow > 0)
                        item.MainBase += freeSpace * (item.Grow / totalGrow);
                }
            }
        }
        else if (freeSpace < 0)
        {
            double totalShrink = 0;
            foreach (var item in items) totalShrink += item.Shrink * item.MainBase;
            if (totalShrink > 0)
            {
                foreach (var item in items)
                {
                    if (item.Shrink > 0)
                    {
                        double shrinkAmount = (-freeSpace) * (item.Shrink * item.MainBase / totalShrink);
                        item.MainBase = Math.Max(0, item.MainBase - shrinkAmount);
                    }
                }
            }
        }

        // 应用子元素自身的 Min/Max 约束
        foreach (var item in items)
        {
            var c = item.Control;
            if (isRow)
            {
                if (!double.IsNaN(c.MinWidth) && c.MinWidth > 0) item.MainBase = Math.Max(item.MainBase, c.MinWidth);
                if (!double.IsNaN(c.MaxWidth) && !double.IsInfinity(c.MaxWidth)) item.MainBase = Math.Min(item.MainBase, c.MaxWidth);
                if (!double.IsNaN(c.MinHeight) && c.MinHeight > 0) item.CrossBase = Math.Max(item.CrossBase, c.MinHeight);
                if (!double.IsNaN(c.MaxHeight) && !double.IsInfinity(c.MaxHeight)) item.CrossBase = Math.Min(item.CrossBase, c.MaxHeight);
            }
            else
            {
                if (!double.IsNaN(c.MinHeight) && c.MinHeight > 0) item.MainBase = Math.Max(item.MainBase, c.MinHeight);
                if (!double.IsNaN(c.MaxHeight) && !double.IsInfinity(c.MaxHeight)) item.MainBase = Math.Min(item.MainBase, c.MaxHeight);
                if (!double.IsNaN(c.MinWidth) && c.MinWidth > 0) item.CrossBase = Math.Max(item.CrossBase, c.MinWidth);
                if (!double.IsNaN(c.MaxWidth) && !double.IsInfinity(c.MaxWidth)) item.CrossBase = Math.Min(item.CrossBase, c.MaxWidth);
            }
        }

        // JustifyContent 计算起始偏移和间距
        double actualUsedMain = totalGaps;
        foreach (var item in items) actualUsedMain += item.MainBase;

        double remainingSpace = Math.Max(0, mainSize - actualUsedMain);
        double mainOffset = 0;
        double spaceBetween = 0;

        switch (JustifyContent)
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
                    double space = remainingSpace / normalCount;
                    mainOffset = space / 2;
                    spaceBetween = space;
                }
                break;
        }

        // 放置子元素
        double cursor = mainOffset;
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            double mainPos = cursor;

            // 交叉轴尺寸：非 TextBlock 且没有显式交叉轴尺寸的容器，默认拉伸填满
            double childCross = item.CrossBase;
            bool isContainer = item.Control is global::Avalonia.Controls.Border || item.Control is EchoUIPanel || item.Control is TextBox;
            if (isContainer && !item.HasExplicitCross)
            {
                childCross = crossSize;
            }

            // AlignItems 交叉轴定位
            double crossPos;
            switch (AlignItems)
            {
                case AlignItems.Center:
                    crossPos = (crossSize - childCross) / 2;
                    break;
                case AlignItems.End:
                    crossPos = crossSize - childCross;
                    break;
                default: // Start
                    crossPos = 0;
                    break;
            }

            // 重新测量子元素以确保它知道最终分配的尺寸
            double arrangeW, arrangeH;
            if (isRow)
            {
                arrangeW = item.MainBase;
                arrangeH = childCross;
            }
            else
            {
                arrangeW = childCross;
                arrangeH = item.MainBase;
            }

            item.Control.Measure(new Size(arrangeW, arrangeH));

            Rect rect;
            if (isRow)
                rect = new Rect(mainPos, crossPos, arrangeW, arrangeH);
            else
                rect = new Rect(crossPos, mainPos, arrangeW, arrangeH);

            item.Control.Arrange(rect);

            cursor = mainPos + item.MainBase + gap;
            if (i < normalCount - 1 &&
                (JustifyContent == JustifyContent.SpaceBetween || JustifyContent == JustifyContent.SpaceAround))
            {
                cursor += spaceBetween;
            }
        }

        return finalSize;
    }

    private class FlexItem
    {
        public Control Control { get; set; } = null!;
        public double MainBase { get; set; }
        public double CrossBase { get; set; }
        public float Grow { get; set; }
        public float Shrink { get; set; }
        public bool HasExplicitCross { get; set; }
    }
}
