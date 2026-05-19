using EchoUI.Core;

namespace EchoUI.Render.Win32;

/// <summary>
/// 全局文本测量覆盖钩子。允许外部渲染后端（如 WebGPU/SixLabors）替换 GDI 测量，
/// 使布局/编辑/绘制使用一致的字体度量。设置 <see cref="Override"/> 为 null 即恢复 GDI 默认实现。
/// 影响所有走 <c>GdiText.MeasureText</c> 的调用路径：布局引擎、ITextMeasurer、Win32CommandExecutor。
/// </summary>
public static class TextMeasurementHook
{
    /// <summary>
    /// 测量委托。返回 null 时回退到 GDI 默认实现。
    /// 参数：text, fontFamily, fontSize(px), fontWeight, widthConstraint, noWrap。
    /// </summary>
    public delegate TextMeasurementResult? MeasureDelegate(
        string? text,
        string? fontFamily,
        float fontSize,
        string? fontWeight,
        float? widthConstraint,
        bool noWrap);

    /// <summary>
    /// 若非 null，<c>GdiText.MeasureText</c> 会先调用此委托；委托返回非 null 即直接使用其结果（并缓存）。
    /// </summary>
    public static MeasureDelegate? Override { get; set; }
}
