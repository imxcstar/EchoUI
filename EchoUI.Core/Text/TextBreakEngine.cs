namespace EchoUI.Core.Text;

internal static class TextBreakEngine
{
    public static bool IsPreferredBreakAfter(string text, int charIndex)
    {
        if (charIndex < 0 || charIndex >= text.Length)
            return false;

        var c = text[charIndex];
        if (char.IsWhiteSpace(c))
            return true;

        if (IsCjk(c))
            return true;

        return c is '-' or '/' or '\\' or ',' or '.' or ';' or ':' or ')' or ']' or '}' or '、' or '。' or '，' or '；' or '：' or '）' or '】' or '》';
    }

    public static bool IsWordBoundaryBefore(string text, int charIndex)
    {
        if (charIndex <= 0 || charIndex > text.Length)
            return true;

        return char.IsWhiteSpace(text[charIndex - 1]) || IsPreferredBreakAfter(text, charIndex - 1);
    }

    private static bool IsCjk(char c)
    {
        return c is >= '\u4E00' and <= '\u9FFF'
            or >= '\u3400' and <= '\u4DBF'
            or >= '\u3040' and <= '\u30FF'
            or >= '\uAC00' and <= '\uD7AF';
    }
}
