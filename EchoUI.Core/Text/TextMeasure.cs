namespace EchoUI.Core.Text;

public readonly record struct TextRunMeasurement(float Width, float Height, float Baseline);

public interface ITextRunMeasurer
{
    TextRunMeasurement Measure(string text, TextStyle style);
}

public sealed class CachingTextRunMeasurer : ITextRunMeasurer
{
    private const int MaxEntries = 8192;
    private readonly ITextRunMeasurer _inner;
    private readonly Dictionary<TextMeasureCacheKey, TextRunMeasurement> _cache = [];
    private readonly Queue<TextMeasureCacheKey> _order = [];
    private readonly object _lock = new();

    public CachingTextRunMeasurer(ITextRunMeasurer inner)
    {
        _inner = inner;
    }

    public TextRunMeasurement Measure(string text, TextStyle style)
    {
        text ??= string.Empty;
        var key = new TextMeasureCacheKey(text, style.LayoutFingerprint);
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var cached))
                return cached;
        }

        var measured = _inner.Measure(text, style);
        lock (_lock)
        {
            if (!_cache.ContainsKey(key))
            {
                if (_cache.Count >= MaxEntries)
                {
                    while (_order.Count > 0 && _cache.Count >= MaxEntries)
                        _cache.Remove(_order.Dequeue());
                }

                _cache[key] = measured;
                _order.Enqueue(key);
            }
        }

        return measured;
    }

    private readonly record struct TextMeasureCacheKey(string Text, string StyleFingerprint);
}
