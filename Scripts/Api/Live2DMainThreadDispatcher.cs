using System.Collections.Concurrent;
using Godot;

namespace Live2D.Api;

internal sealed partial class Live2DMainThreadDispatcher : Node
{
    private const int MaxWorkItemsPerFrame = 512;
    private const string NodeName = "Live2DMainThreadDispatcher";

    private static readonly object LifecycleLock = new();
    private static readonly ConcurrentQueue<IDispatchWorkItem> Queue = new();
    private static Live2DMainThreadDispatcher? _instance;
    private static Action<Exception>? _unhandledExceptionHandler;
    private static int _mainThreadId;

    private Live2DMainThreadDispatcher()
    {
        Name = NodeName;
        ProcessMode = ProcessModeEnum.Always;
    }

    internal static bool IsReady => Volatile.Read(ref _instance) is not null;

    internal static bool IsCurrentThread =>
        IsReady && System.Environment.CurrentManagedThreadId == Volatile.Read(ref _mainThreadId);

    internal static void Install(Action<Exception> unhandledExceptionHandler)
    {
        ArgumentNullException.ThrowIfNull(unhandledExceptionHandler);
        lock (LifecycleLock)
        {
            if (_instance is not null)
            {
                if (!IsCurrentThread)
                    throw new InvalidOperationException(
                        "The Live2D main-thread dispatcher can only be configured from its Godot main thread.");
                _unhandledExceptionHandler = unhandledExceptionHandler;
                return;
            }

            if (Engine.GetMainLoop() is not SceneTree tree)
                throw new InvalidOperationException("The Godot SceneTree is not available.");

            var dispatcher = new Live2DMainThreadDispatcher();
            _mainThreadId = System.Environment.CurrentManagedThreadId;
            _unhandledExceptionHandler = unhandledExceptionHandler;
            Volatile.Write(ref _instance, dispatcher);
            tree.Root.CallDeferred(Node.MethodName.AddChild, dispatcher);
        }
    }

    internal static void Post(Action callback)
        => DispatchPost(callback, alwaysQueue: false);

    internal static void EnqueuePost(Action callback)
        => DispatchPost(callback, alwaysQueue: true);

    private static void DispatchPost(Action callback, bool alwaysQueue)
    {
        ArgumentNullException.ThrowIfNull(callback);
        if (!alwaysQueue)
        {
            RequireReady();
            if (IsCurrentThread)
            {
                ExecutePost(callback);
                return;
            }
        }
        Enqueue(new PostWorkItem(callback));
    }

    internal static Task InvokeAsync(Action callback, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(callback);
        return InvokeAsync<object?>(() =>
        {
            callback();
            return null;
        }, cancellationToken);
    }

    internal static Task<T> InvokeAsync<T>(Func<T> callback, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(callback);
        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled<T>(cancellationToken);

        RequireReady();
        if (IsCurrentThread)
        {
            try
            {
                return Task.FromResult(callback());
            }
            catch (Exception exception)
            {
                return Task.FromException<T>(exception);
            }
        }

        var item = new TaskWorkItem<T>(callback, cancellationToken);
        try
        {
            Enqueue(item);
        }
        catch (Exception exception)
        {
            item.Fail(exception);
        }
        return item.Task;
    }

    public override void _Process(double delta)
    {
        for (var processed = 0;
             processed < MaxWorkItemsPerFrame && Queue.TryDequeue(out var item);
             processed++)
            item.Execute();
    }

    public override void _ExitTree()
    {
        lock (LifecycleLock)
        {
            if (!ReferenceEquals(_instance, this))
                return;
            Volatile.Write(ref _instance, null);
            _mainThreadId = 0;
        }

        var exception = new ObjectDisposedException(
            nameof(Live2DMainThreadDispatcher),
            "The Live2D main-thread dispatcher has stopped.");
        while (Queue.TryDequeue(out var item))
            item.Fail(exception);
        _unhandledExceptionHandler = null;
    }

    private static void RequireReady()
    {
        if (!IsReady)
            throw new InvalidOperationException(
                "The Live2D main-thread dispatcher is not initialized. " +
                "Wait until the Live2D mod has finished initialization before scheduling work.");
    }

    private static void Enqueue(IDispatchWorkItem item)
    {
        lock (LifecycleLock)
        {
            RequireReady();
            Queue.Enqueue(item);
        }
    }

    private static void ExecutePost(Action callback)
    {
        try
        {
            callback();
        }
        catch (Exception exception)
        {
            try
            {
                _unhandledExceptionHandler?.Invoke(exception);
            }
            catch
            {
                // A fire-and-forget callback must not break the dispatch loop,
                // even if the host's logging callback also fails.
            }
        }
    }

    private interface IDispatchWorkItem
    {
        void Execute();
        void Fail(Exception exception);
    }

    private sealed class PostWorkItem(Action callback) : IDispatchWorkItem
    {
        public void Execute() => ExecutePost(callback);

        public void Fail(Exception exception)
            => ExecutePost(() => throw exception);
    }

    private sealed class TaskWorkItem<T> : IDispatchWorkItem
    {
        private readonly Func<T> _callback;
        private readonly CancellationToken _cancellationToken;
        private readonly TaskCompletionSource<T> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly CancellationTokenRegistration _cancellationRegistration;
        private int _state;

        internal TaskWorkItem(Func<T> callback, CancellationToken cancellationToken)
        {
            _callback = callback;
            _cancellationToken = cancellationToken;
            if (cancellationToken.CanBeCanceled)
                _cancellationRegistration = cancellationToken.UnsafeRegister(
                    static state => ((TaskWorkItem<T>)state!).Cancel(),
                    this);
        }

        internal Task<T> Task => _completion.Task;

        public void Execute()
        {
            if (Interlocked.CompareExchange(ref _state, 1, 0) != 0)
            {
                _cancellationRegistration.Dispose();
                return;
            }

            try
            {
                _completion.TrySetResult(_callback());
            }
            catch (Exception exception)
            {
                _completion.TrySetException(exception);
            }
            finally
            {
                Volatile.Write(ref _state, 2);
                _cancellationRegistration.Dispose();
            }
        }

        public void Fail(Exception exception)
        {
            if (Interlocked.CompareExchange(ref _state, 2, 0) == 0)
                _completion.TrySetException(exception);
            _cancellationRegistration.Dispose();
        }

        private void Cancel()
        {
            if (Interlocked.CompareExchange(ref _state, 2, 0) == 0)
                _completion.TrySetCanceled(_cancellationToken);
        }
    }
}
