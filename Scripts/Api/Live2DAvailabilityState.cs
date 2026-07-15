namespace Live2D.Api;

internal sealed class Live2DAvailabilityState<T>(T value)
    where T : class
{
    private readonly object _lock = new();
    private readonly List<Waiter> _availableWaiters = [];
    private readonly List<Waiter> _unavailableWaiters = [];
    private int _isAvailable;

    internal bool IsAvailable => Volatile.Read(ref _isAvailable) != 0;

    internal Task<T> WaitAsync(bool available, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled<T>(cancellationToken);

        Waiter waiter;
        lock (_lock)
        {
            if (IsAvailable == available)
                return Task.FromResult(value);
            waiter = new Waiter(this, available, cancellationToken);
            GetWaiters(available).Add(waiter);
        }

        waiter.RegisterCancellation();
        return waiter.Task;
    }

    internal void Set(bool available)
    {
        Waiter[] waiters;
        lock (_lock)
        {
            if (IsAvailable == available)
                return;
            Volatile.Write(ref _isAvailable, available ? 1 : 0);
            var source = GetWaiters(available);
            waiters = source.ToArray();
            source.Clear();
        }

        foreach (var waiter in waiters)
            waiter.Complete(value);
    }

    private List<Waiter> GetWaiters(bool available)
        => available ? _availableWaiters : _unavailableWaiters;

    private void Cancel(Waiter waiter, bool available, CancellationToken cancellationToken)
    {
        lock (_lock)
            GetWaiters(available).Remove(waiter);
        waiter.Cancel(cancellationToken);
    }

    private sealed class Waiter(
        Live2DAvailabilityState<T> owner,
        bool available,
        CancellationToken cancellationToken)
    {
        private readonly TaskCompletionSource<T> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private CancellationTokenRegistration _registration;

        internal Task<T> Task => _completion.Task;

        internal void RegisterCancellation()
        {
            if (!cancellationToken.CanBeCanceled)
                return;
            var registration = cancellationToken.UnsafeRegister(
                static state => ((Waiter)state!).OnCancellation(),
                this);
            _registration = registration;
            if (Task.IsCompleted)
                registration.Dispose();
        }

        internal void Complete(T result)
        {
            if (_completion.TrySetResult(result))
                _registration.Dispose();
        }

        internal void Cancel(CancellationToken token)
        {
            if (_completion.TrySetCanceled(token))
                _registration.Unregister();
        }

        private void OnCancellation()
            => owner.Cancel(this, available, cancellationToken);
    }
}
