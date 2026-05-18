using System.Globalization;

namespace EchoUI.Core.Text;

public static class TextGraphemeEnumerator
{
    public static int Count(string? text)
    {
        return string.IsNullOrEmpty(text) ? 0 : StringInfo.ParseCombiningCharacters(text).Length;
    }

    public static IReadOnlyList<TextGrapheme> Enumerate(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return Array.Empty<TextGrapheme>();

        var indexes = StringInfo.ParseCombiningCharacters(text);
        var graphemes = new TextGrapheme[indexes.Length];
        for (var i = 0; i < indexes.Length; i++)
        {
            var start = indexes[i];
            var end = i + 1 < indexes.Length ? indexes[i + 1] : text.Length;
            graphemes[i] = new TextGrapheme(start, end - start, text[start..end]);
        }
        return graphemes;
    }

    public static string RemoveLastGrapheme(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var indexes = StringInfo.ParseCombiningCharacters(text);
        return indexes.Length <= 1 ? string.Empty : text[..indexes[^1]];
    }

    public static string TrimEndGraphemes(string? text, int graphemeCount)
    {
        if (string.IsNullOrEmpty(text) || graphemeCount <= 0)
            return string.Empty;

        var indexes = StringInfo.ParseCombiningCharacters(text);
        return graphemeCount >= indexes.Length ? text : text[..indexes[graphemeCount]];
    }
}

public readonly record struct TextGrapheme(int Start, int Length, string Value)
{
    public int End => Start + Length;
}
