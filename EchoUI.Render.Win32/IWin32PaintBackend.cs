using EchoUI.Core;

namespace EchoUI.Render.Win32
{
    /// <summary>
    /// Win32 窗口绘制后端抽象。允许 EchoUI 替换默认 GDI+ 绘制，例如使用 WebGPU。
    /// 实现方在 <see cref="Win32Window.PaintBackend"/> 注入后，会接管 WM_PAINT 与 WM_SIZE 中
    /// 与图形相关的工作；输入事件、布局、命中测试等仍由 Win32Renderer 处理。
    /// </summary>
    public interface IWin32PaintBackend : IDisposable
    {
        /// <summary>
        /// 后端被附加到窗口时调用。该回调发生在窗口已创建之后。
        /// </summary>
        void Attach(nint hwnd, Win32Renderer renderer);

        /// <summary>
        /// 客户区大小变化时调用，单位是像素。
        /// </summary>
        void Resize(int width, int height);

        /// <summary>
        /// WM_PAINT 处理：实现方需要自行确保布局已计算（典型调用 renderer.EnsureLayout(w, h)），
        /// 然后绘制到窗口并呈现一帧。无需调用 BeginPaint / EndPaint，宿主已处理。
        /// </summary>
        void Paint(int width, int height);
    }
}
