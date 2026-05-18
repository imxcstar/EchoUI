using System.Text;

namespace EchoUI.Core;

/// <summary>
/// EchoUI 渲染 / 更新过程中抛出的诊断异常。
/// </summary>
public sealed class EchoUIRenderException : Exception
{
    public EchoUIRenderException(string operation, string elementStack, Exception innerException)
        : base(BuildMessage(operation, elementStack, innerException), innerException)
    {
        Operation = operation;
        ElementStack = elementStack;
    }

    public string Operation { get; }

    public string ElementStack { get; }

    private static string BuildMessage(string operation, string elementStack, Exception innerException)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"EchoUI UI error while {operation}.");
        sb.AppendLine($"Inner exception: {innerException.GetType().FullName}: {innerException.Message}");
        sb.AppendLine("Element stack:");
        sb.Append(elementStack);
        return sb.ToString();
    }
}
