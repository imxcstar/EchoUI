using EchoUI.Core;

namespace EchoUI.Render.Win32
{
    internal readonly record struct RectF(float X, float Y, float Width, float Height)
    {
        public float Left => X;
        public float Top => Y;
        public float Right => X + Width;
        public float Bottom => Y + Height;

        public static RectF Intersect(RectF a, RectF b)
        {
            var left = Math.Max(a.Left, b.Left);
            var top = Math.Max(a.Top, b.Top);
            var right = Math.Min(a.Right, b.Right);
            var bottom = Math.Min(a.Bottom, b.Bottom);
            return new RectF(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
        }
    }

    /// <summary>
    /// Win32 平台渲染节点：平台无关状态继承自 Core RenderNode，只保留 Win32 原生资源。
    /// </summary>
    internal class Win32Element : RenderNode<Win32Element>
    {
        public Win32Element(string elementType) : base(elementType)
        {
        }

        // --- Win32 原生 Input 资源 ---
        public nint EditHwnd { get; set; }
        public nint NativeFontHandle { get; set; }
        public nint NativeBrushHandle { get; set; }

        public override nint EditHandle => EditHwnd;

        // --- Win32 图片资源 ---
        public nint NativeImageHandle { get; set; }
        public int NativeImageWidth { get; set; }
        public int NativeImageHeight { get; set; }

        /// <summary>当前 img 元素的 src 路径（保留原始字符串，供非 GDI 后端使用）。</summary>
        public string? ImageSrc { get; set; }

        // --- GDI/GDI+ 路径缓存 ---
        public nint RoundedFillPath { get; set; }
        public RectF RoundedFillPathBounds { get; set; }
        public float RoundedFillPathRadius { get; set; } = -1;
        public nint RoundedBorderPath { get; set; }
        public RectF RoundedBorderPathBounds { get; set; }
        public float RoundedBorderPathRadius { get; set; } = -1;

        public RectF GetAbsoluteBounds()
        {
            var bounds = AbsoluteBounds;
            return new RectF(bounds.X, bounds.Y, bounds.Width, bounds.Height);
        }
    }
}
