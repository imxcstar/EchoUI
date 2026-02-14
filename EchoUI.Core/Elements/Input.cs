namespace EchoUI.Core
{
    /// <summary>
    /// Input (输入框) 组件的属性。
    /// </summary>
    public record class InputProps : Props
    {
        /// <summary>
        /// 输入框的当前值。
        /// </summary>
        public string Value { get; init; } = "";

        /// <summary>
        /// 当输入框的值改变时触发的回调。
        /// </summary>
        public Action<string>? OnValueChanged { get; init; }

        public Color? BackgroundColor { get; init; }
        public Color? TextColor { get; init; }
        public Color? BorderColor { get; init; }
        public Color? FocusedBorderColor { get; init; }
        public Spacing? Padding { get; init; }
    }

    public partial class Elements
    {
        /// <summary>
        /// Input (输入框) 组件。
        /// </summary>
        [Element]
        public static Element Input(InputProps props)
        {
            return new Element(ElementCoreName.Input, props);
        }
    }
}
