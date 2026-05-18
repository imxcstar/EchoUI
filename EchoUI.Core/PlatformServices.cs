namespace EchoUI.Core;

/// <summary>
/// 平台文本测量服务。Core 只声明需要测量文本，不关心平台如何实现。
/// </summary>
public interface ITextMeasurer
{
    TextMeasurementResult Measure(TextMeasurementRequest request);
}

/// <summary>
/// 平台剪贴板服务。Core 只关心文本读写语义。
/// </summary>
public interface IClipboardService
{
    Task<string> ReadTextAsync();
    Task WriteTextAsync(string text);
}

/// <summary>
/// 平台图片服务。图片句柄/资源类型由具体后端决定。
/// </summary>
public interface IImageService<TImage>
{
    bool TryLoad(string source, out TImage image);
    void Release(TImage image);
}

/// <summary>
/// 平台光标服务。Core 使用统一 cursor 名称，平台负责映射为原生光标。
/// </summary>
public interface ICursorService
{
    void SetCursor(string? cursor);
}

/// <summary>
/// 平台输入法定位服务。节点类型由运行时/后端适配层决定。
/// </summary>
public interface IInputMethodService<TNode>
{
    void UpdateInputMethodPosition(TNode? focusedNode);
}

/// <summary>
/// 平台原生输入控件服务。用于把 Input 语义挂接到平台原生控件。
/// </summary>
public interface INativeInputService<TNode>
{
    void CreateNativeInput(TNode node);
    void SyncNativeInput(TNode node);
    void DestroyNativeInput(TNode node);
}

/// <summary>
/// 平台服务集合。Renderer 作为宿主入口提供这些服务，Core 不直接依赖具体平台。
/// </summary>
public interface IPlatformServices
{
    ITextMeasurer TextMeasurer { get; }
    IClipboardService Clipboard { get; }
}
