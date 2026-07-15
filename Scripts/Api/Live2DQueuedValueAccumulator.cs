namespace Live2D.Api;

internal sealed class Live2DQueuedValueAccumulator
{
    private readonly object _lock = new();
    private readonly Action<IReadOnlyDictionary<string, float>> _apply;
    private Dictionary<string, float>? _pending;
    private bool _scheduled;

    internal Live2DQueuedValueAccumulator(
        Action<IReadOnlyDictionary<string, float>> apply)
    {
        ArgumentNullException.ThrowIfNull(apply);
        _apply = apply;
    }

    internal void Queue(string id, float value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        EnsureFinite(value);
        lock (_lock)
        {
            EnsureScheduled();
            _pending![id] = value;
        }
    }

    internal void Queue(IReadOnlyDictionary<string, float> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
            return;

        var copy = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in values)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pair.Key);
            EnsureFinite(pair.Value);
            copy[pair.Key] = pair.Value;
        }

        lock (_lock)
        {
            EnsureScheduled();
            foreach (var pair in copy)
                _pending![pair.Key] = pair.Value;
        }
    }

    private void EnsureScheduled()
    {
        if (_scheduled)
            return;
        Live2DMainThreadDispatcher.EnqueuePost(Flush);
        _scheduled = true;
        _pending = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
    }

    private void Flush()
    {
        Dictionary<string, float>? values;
        lock (_lock)
        {
            values = _pending;
            _pending = null;
            _scheduled = false;
        }

        if (values is { Count: > 0 })
            _apply(values);
    }

    private static void EnsureFinite(float value)
    {
        if (!float.IsFinite(value))
            throw new ArgumentOutOfRangeException(nameof(value), "Value must be finite.");
    }
}
