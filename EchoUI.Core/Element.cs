namespace EchoUI.Core
{
    [AttributeUsageAttribute(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class ElementAttribute : Attribute
    {
        public string? DefaultProperty { get; set; }
    }

    /// <summary>
    /// 同步组件委托
    /// </summary>
    public delegate Element? Component(Props props);

    /// <summary>
    /// 异步组件委托，原生支持 async/await
    /// </summary>
    public delegate Task<Element?> AsyncComponent(Props props);

    /// <summary>
    /// 类型安全的Element Type包装器
    /// </summary>
    public readonly record struct ElementType
    {
        private readonly object _type;
        public bool IsNative => _type is string;
        public bool IsComponent => _type is Component;
        public bool IsAsyncComponent => _type is AsyncComponent;
        public string AsNativeType => (string)_type;
        public Delegate AsComponentDelegate => (Delegate)_type;

        private ElementType(object type) => _type = type;

        public static implicit operator ElementType(string nativeType) => new(nativeType);
        public static implicit operator ElementType(Component component) => new(component);
        public static implicit operator ElementType(AsyncComponent asyncComponent) => new(asyncComponent);
    }

    /// <summary>
    /// UI元素的声明式描述，使用record实现不可变性
    /// </summary>
    public record Element(ElementType Type, Props Props);

    /// <summary>
    /// 所有Props的基类
    /// </summary>
    public record class Props
    {
        /// <summary>
        /// Key是实现高效Diff算法的基石
        /// </summary>
        public string? Key { get; init; }

        public IReadOnlyList<Element> Children { get; init; } = [];

        /// <summary>
        /// 用于异步组件，在等待期间显示的UI
        /// </summary>
        public Element? Fallback { get; init; }

        /// <summary>
        /// 用于跳过不必要重渲染的比较函数
        /// </summary>
        public Func<Props, Props, bool>? AreEqual { get; init; }

        // record struct 需要显式构造函数来初始化集合
        public Props()
        {
            Children = Array.Empty<Element>();
            Key = null;
            Fallback = null;
            AreEqual = null;
        }
    }
}