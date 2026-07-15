namespace Live2D.Api;

internal sealed class Live2DQueuedUpdateAccumulator
{
    private readonly object _lock = new();
    private readonly Action<Live2DModelUpdate> _apply;
    private Live2DModelUpdate? _pending;
    private bool _scheduled;

    internal Live2DQueuedUpdateAccumulator(Action<Live2DModelUpdate> apply)
    {
        ArgumentNullException.ThrowIfNull(apply);
        _apply = apply;
    }

    internal void Queue(Live2DModelUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);
        var copy = update.Copy();
        if (copy.IsEmpty)
            return;
        copy.Validate();

        lock (_lock)
        {
            if (!_scheduled)
            {
                // Queue before publishing the pending state. If the dispatcher is
                // unavailable, this submission fails without leaving stale data.
                Live2DMainThreadDispatcher.EnqueuePost(Flush);
                _scheduled = true;
            }

            _pending ??= new Live2DModelUpdate();
            _pending.MergeFrom(copy);
        }
    }

    private void Flush()
    {
        Live2DModelUpdate? update;
        lock (_lock)
        {
            update = _pending;
            _pending = null;
            _scheduled = false;
        }

        if (update is not null && !update.IsEmpty)
            _apply(update);
    }
}
