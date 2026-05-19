using EchoUI.Core;
using EchoUI.Render.WebGPU.Internal;

namespace EchoUI.Render.Win32.WebGpu;

/// <summary>
/// WebGPU 渲染入口：创建配置成 WebGPU 后端的 <see cref="Win32Renderer"/>。
/// 文本由 SixLabors.Fonts + ImageSharp.Drawing 栅格化到 R8 atlas 后由 WebGPU 合成；
/// 布局测量也走同一份 SixLabors metrics（FontAtlas.MeasureText），保证测量 ↔ 绘制对齐。
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

        var renderer = new Win32Renderer(window) { UseNativeInput = false };
        var backend = new WebGpuPaintBackend();
        backend.Attach(window.Hwnd, renderer);

        // 把测量也切到 FontAtlas（SixLabors）—— 让 layout 使用与栅格化完全相同的 metrics。
        TextMeasurementHook.Override = (text, family, size, weight, widthConstraint, noWrap) =>
        {
            var atlas = backend.TextAtlas;
            if (atlas is null) return null;
            var (w, h) = atlas.MeasureText(text, family, size, weight);
            return new TextMeasurementResult(w, h);
        };

        window.PaintBackend = backend;
        window.NotifyPaintBackendReady();
        return renderer;
    }
}
