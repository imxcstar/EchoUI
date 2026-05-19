using EchoUI.Render.Win32;
using EchoUI.Render.WebGPU.Internal;

namespace EchoUI.Render.WebGPU;

/// <summary>
/// WebGPU 渲染入口：创建配置成 WebGPU 后端的 <see cref="Win32Renderer"/>。
/// 文本由 GDI（CLEARTYPE_QUALITY + TrueType hinting）栅格化到纹理后由 WebGPU 合成，
/// 因此布局测量与字形渲染都使用 GDI，外观与 <see cref="GdiPainter"/> 100% 一致。
/// </summary>
public static class WebGpuRenderer
{
    /// <summary>
    /// 在指定 <see cref="Win32Window"/> 上启用 WebGPU 绘制后端，返回已配置的 Win32Renderer。
    /// 调用方应在 window.Create() 之后、消息循环之前调用本方法。
    /// </summary>
    public static Win32Renderer Create(Win32Window window)
    {
        if (window.Hwnd == 0)
            throw new InvalidOperationException("Win32Window must be created before attaching WebGPU backend.");

        // 文本测量与字形栅格化都走 GDI，无需任何度量覆盖：默认 GDI 测量结果就是绘制结果。
        TextMeasurementHook.Override = null;

        var renderer = new Win32Renderer(window) { UseNativeInput = false };
        var backend = new WebGpuPaintBackend();
        backend.Attach(window.Hwnd, renderer);
        window.PaintBackend = backend;
        window.NotifyPaintBackendReady();
        return renderer;
    }
}
