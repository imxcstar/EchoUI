namespace EchoUI.Core
{
    /// <summary>
    /// 颜色
    /// </summary>
    public readonly record struct Color(byte R, byte G, byte B, byte A = 255)
    {
        public static Color White => new(255, 255, 255);
        public static Color Black => new(0, 0, 0);
        public static Color Red => new(255, 0, 0);
        public static Color Green => new(0, 255, 0);
        public static Color Blue => new(0, 0, 255);
        public static Color Gray => new(128, 128, 128);
        public static Color LightGray => new(211, 211, 211);
        public static Color Gainsboro => new(220, 220, 220);

        public static Color FromHex(string hex)
        {
            hex = hex.TrimStart('#');
            return new Color(
                Convert.ToByte(hex.Substring(0, 2), 16),
                Convert.ToByte(hex.Substring(2, 2), 16),
                Convert.ToByte(hex.Substring(4, 2), 16),
                hex.Length >= 8 ? Convert.ToByte(hex.Substring(6, 2), 16) : (byte)255
            );
        }

        public static Color FromRgb(byte r, byte g, byte b)
        {
            return new Color(
                r,
                g,
                b,
                255
            );
        }
    }

    /// <summary>
    /// 代表一个尺寸值。它可以是固定的像素值、父容器的百分比，或是自动计算。
    /// </summary>
    public readonly record struct Dimension(float Value, DimensionUnit Unit)
    {
        /// <summary>
        /// 0像素
        /// </summary>
        public static Dimension ZeroPixels => new(0, DimensionUnit.Pixels);
        /// <summary>
        /// 创建一个以像素为单位的尺寸。
        /// </summary>
        public static Dimension Pixels(float value) => new(value, DimensionUnit.Pixels);
        /// <summary>
        /// 创建一个以百分比为单位的尺寸。
        /// </summary>
        public static Dimension Percent(float value) => new(value, DimensionUnit.Percent);
    }

    /// <summary>
    /// 尺寸单位的枚举。
    /// </summary>
    public enum DimensionUnit { Pixels, Percent }

    /// <summary>
    /// 代表边距（Margin）或填充（Padding）的间距值，可以独立控制四个方向。
    /// </summary>
    public readonly record struct Spacing(Dimension Left, Dimension Top, Dimension Right, Dimension Bottom)
    {
        /// <summary>
        /// 为四个方向应用相同的间距值。
        /// </summary>
        public Spacing(Dimension all) : this(all, all, all, all) { }
        /// <summary>
        /// 分别为水平和垂直方向应用不同的间距值。
        /// </summary>
        public Spacing(Dimension horizontal, Dimension vertical) : this(horizontal, vertical, horizontal, vertical) { }
    }

    /// <summary>
    /// 代表一个二维坐标点。
    /// </summary>
    public readonly record struct Point(int X, int Y);

    /// <summary>
    /// 定义一个属性过渡动画。
    /// </summary>
    /// <param name="DurationMs">动画的持续时间（毫秒）。</param>
    /// <param name="Easing">动画使用的缓动函数。</param>
    public readonly record struct Transition(int DurationMs, Easing Easing = Easing.Linear);

    /// <summary>
    /// 动画缓动函数的通用枚举。
    /// </summary>
    public enum Easing
    {
        Linear,
        Ease,
        EaseIn,
        EaseOut,
        EaseInOut
    }

    /// <summary>
    /// 布局方向（用于容器）。
    /// </summary>
    public enum LayoutDirection { Vertical, Horizontal }
    /// <summary>
    /// 主轴对齐方式（决定子元素在主轴上的排列）。
    /// </summary>
    public enum JustifyContent { Start, Center, End, SpaceBetween, SpaceAround }
    /// <summary>
    /// 交叉轴对齐方式（决定子元素在交叉轴上的排列）。
    /// </summary>
    public enum AlignItems { Start, Center, End }
    /// <summary>
    /// 边框样式。
    /// </summary>
    public enum BorderStyle { None, Solid, Dashed, Dotted }

    /// <summary>
    /// 定义鼠标按键的类型。
    /// </summary>
    public enum MouseButton { Left, Right, Middle }

    public readonly record struct ValueDictionary<TKey, TValue>(IReadOnlyDictionary<TKey, TValue> Inner)
    {
        public IReadOnlyDictionary<TKey, TValue> Data => Inner;

        public bool Equals(ValueDictionary<TKey, TValue> other) =>
            Inner.Count == other.Inner.Count &&
            Inner.All(kvp =>
                other.Inner.TryGetValue(kvp.Key, out var v) &&
                EqualityComparer<TValue>.Default.Equals(kvp.Value, v));

        public override int GetHashCode() =>
            Inner.Aggregate(0, (hash, kvp) => hash ^ HashCode.Combine(kvp.Key, kvp.Value));
    }

}
