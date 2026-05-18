namespace EchoUI.Core.Text;

public sealed class TextLayoutCache
{
    private const int MaxEntries = 512;
    private readonly Dictionary<TextLayoutCacheKey, TextLayoutResult> _cache = [];
    private readonly Queue<TextLayoutCacheKey> _order = [];
    private readonly object _lock = new();

    public static TextLayoutCache Shared { get; } = new();

    public bool TryGet(TextContent content, TextLayoutOptions options, out TextLayoutResult result)
    {
        var key = CreateKey(content, options);
        lock (_lock)
            return _cache.TryGetValue(key, out result!);
    }

    public void Set(TextContent content, TextLayoutOptions options, TextLayoutResult result)
    {
        var key = CreateKey(content, options);
        lock (_lock)
        {
            if (_cache.ContainsKey(key))
            {
                _cache[key] = result;
                return;
            }

            if (_cache.Count >= MaxEntries)
            {
                while (_order.Count > 0 && _cache.Count >= MaxEntries)
                    _cache.Remove(_order.Dequeue());
            }

            _cache[key] = result;
            _order.Enqueue(key);
        }
    }

    private static TextLayoutCacheKey CreateKey(TextContent content, TextLayoutOptions options)
    {
        return new TextLayoutCacheKey(content.LayoutFingerprint, options.Fingerprint);
    }

    private readonly record struct TextLayoutCacheKey(string ContentFingerprint, string OptionsFingerprint);
}
