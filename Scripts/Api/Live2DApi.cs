using Godot;
using Live2D.Scripts.Configuration;
using Live2D.Scripts.Packs;
using Live2D.Scripts.Runtime;
using MegaCrit.Sts2.Core.Nodes;

namespace Live2D.Api;

/// <summary>
/// Public runtime entry point for other Slay the Spire 2 mods.
/// <para>中文：其他 Mod 接入 Live2D 运行时的统一入口。</para>
/// </summary>
public static class Live2DApi
{
    /// <summary>Compile-time API capability number.</summary>
    public const int ApiVersion = 9;

    /// <summary>The only supported filename extension for a Live2D package.</summary>
    public const string PackageFileExtension = ".live2dpack";

    /// <summary>
    /// Runtime API capability version. Prefer this property over the compile-time
    /// <see cref="ApiVersion"/> constant when diagnosing a loaded runtime.
    /// </summary>
    public static int RuntimeApiVersion => ApiVersion;

    /// <summary>Version of the currently loaded Live2D runtime Mod.</summary>
    public static string RuntimeVersion => Entry.ModVersion;

    private static readonly Dictionary<Live2DModelKey, Live2DModelHandle> Handles = new();
    private static readonly List<ProviderLifecycleSubscription> ProviderHooks = [];

    /// <summary>Whether the Live2D Godot main-thread dispatcher is ready.</summary>
    public static bool IsDispatcherReady => Live2DMainThreadDispatcher.IsReady;

    /// <summary>Whether the current caller is running on the Live2D Godot main thread.</summary>
    public static bool IsMainThread => Live2DMainThreadDispatcher.IsCurrentThread;

    /// <summary>
    /// Schedules fire-and-forget work on the Godot main thread. Callback exceptions
    /// are written to the Live2D log. Use <see cref="InvokeAsync(Action, CancellationToken)"/>
    /// when the caller must observe completion or exceptions.
    /// </summary>
    public static void Post(Action callback)
        => Live2DMainThreadDispatcher.Post(callback);

    /// <summary>
    /// Runs work on the Godot main thread and returns a task that represents its
    /// completion. Cancellation prevents a callback that has not started yet.
    /// </summary>
    public static Task InvokeAsync(
        Action callback,
        CancellationToken cancellationToken = default)
        => Live2DMainThreadDispatcher.InvokeAsync(callback, cancellationToken);

    /// <summary>
    /// Runs a function on the Godot main thread and returns its result. Cancellation
    /// prevents a callback that has not started yet.
    /// </summary>
    public static Task<T> InvokeAsync<T>(
        Func<T> callback,
        CancellationToken cancellationToken = default)
        => Live2DMainThreadDispatcher.InvokeAsync(callback, cancellationToken);

    /// <summary>Returns all handles that have been observed during this game session.</summary>
    public static IReadOnlyList<ILive2DModelHandle> GetModels()
    {
        lock (Handles)
            return Handles.Values.Cast<ILive2DModelHandle>().ToArray();
    }

    /// <summary>Returns handles owned by one mod, including currently unavailable handles.</summary>
    public static IReadOnlyList<ILive2DModelHandle> GetModels(string ownerModId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerModId);
        lock (Handles)
            return Handles.Values
                .Where(handle => string.Equals(
                    handle.OwnerModId,
                    ownerModId,
                    StringComparison.OrdinalIgnoreCase))
                .Cast<ILive2DModelHandle>()
                .ToArray();
    }

    /// <summary>
    /// Registers provider-scoped lifecycle hooks for custom character behavior.
    /// Existing packs and available models are replayed immediately in lifecycle
    /// order. Call on the Godot main thread; callbacks also run on that thread.
    /// Dispose the returned subscription to stop receiving callbacks.
    /// </summary>
    public static IDisposable RegisterProviderHook(
        string ownerModId,
        ILive2DProviderLifecycleHook hook)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerModId);
        ArgumentNullException.ThrowIfNull(hook);
        EnsureMainThread();

        var subscription = new ProviderLifecycleSubscription(ownerModId, hook);
        lock (ProviderHooks)
            ProviderHooks.Add(subscription);

        foreach (var pack in Live2DRegisteredPackRegistry.GetRegisteredPacks(ownerModId))
            subscription.NotifyPackRegistered(pack);
        foreach (var model in GetModels(ownerModId).Where(model => model.IsAvailable))
            subscription.NotifyModelAvailable(model);
        return subscription;
    }

    /// <summary>Returns a stable handle, or null if this model/scene has not been created yet.</summary>
    public static ILive2DModelHandle? GetModel(string modelId, Live2DScene scene)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        lock (Handles)
            return Handles.GetValueOrDefault(new Live2DModelKey(modelId, scene));
    }

    /// <summary>Tries to find a stable handle by runtime model ID and scene.</summary>
    public static bool TryGetModel(
        string modelId,
        Live2DScene scene,
        out ILive2DModelHandle? handle)
    {
        handle = GetModel(modelId, scene);
        return handle is not null;
    }

    /// <summary>
    /// Imports a user-managed <c>.live2dpack</c> from an operating-system path
    /// or a Godot <c>res://</c>/<c>user://</c> path.
    /// </summary>
    public static Live2DPackImportResult ImportPack(string packagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        EnsureMainThread();
        Live2DPackArchive.ValidatePackageExtension(packagePath);
        return WithPackPath(
            packagePath,
            static path => ConvertResult(Live2DPackService.Import(path)));
    }

    /// <summary>Imports a user-managed Live2D pack from in-memory archive data.</summary>
    public static Live2DPackImportResult ImportPack(ReadOnlyMemory<byte> packageData)
    {
        EnsureMainThread();
        return WithPackData(
            packageData,
            static path => ConvertResult(Live2DPackService.Import(path)));
    }

    /// <summary>
    /// Registers a provider-owned pack in the central Live2D model library. Live2D
    /// owns visibility, layout, actions, hotkeys and scene instances.
    /// </summary>
    public static ILive2DPackHandle RegisterPack(string ownerModId, string packagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerModId);
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        EnsureMainThread();
        Live2DPackArchive.ValidatePackageExtension(packagePath);
        return WithPackPath(
            packagePath,
            path => Live2DRegisteredPackRegistry.Register(ownerModId, path));
    }

    /// <summary>Registers an in-memory provider-owned pack in the central model library.</summary>
    public static ILive2DPackHandle RegisterPack(string ownerModId, ReadOnlyMemory<byte> packageData)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerModId);
        EnsureMainThread();
        return WithPackData(
            packageData,
            path => Live2DRegisteredPackRegistry.Register(ownerModId, path));
    }

    internal static void UnbindScene(Live2DSceneKind scene)
    {
        EnsureMainThread();
        var apiScene = ToApiScene(scene);
        Live2DModelHandle[] sceneHandles;
        lock (Handles)
            sceneHandles = Handles.Where(pair => pair.Key.Scene == apiScene).Select(pair => pair.Value).ToArray();
        foreach (var handle in sceneHandles)
        {
            var wasAvailable = handle.IsAvailable;
            handle.Unbind();
            if (wasAvailable)
                NotifyModelUnavailable(handle);
        }
    }

    internal static void Bind(Live2DRuntimeModelDefinition definition, Live2DModelInstance instance)
    {
        EnsureMainThread();
        var identity = definition.Identity;
        var key = new Live2DModelKey(identity.RuntimeId, identity.Scene);
        Live2DModelHandle handle;
        lock (Handles)
        {
            if (!Handles.TryGetValue(key, out handle!))
            {
                handle = new Live2DModelHandle(definition);
                Handles.Add(key, handle);
            }
            else
            {
                handle.EnsureIdentity(identity);
                handle.UpdateDefinition(definition);
            }
        }

        var wasAvailable = handle.IsAvailable;
        handle.Bind(instance);
        if (!wasAvailable)
            NotifyModelAvailable(handle);
    }

    internal static void NotifyPackRegistered(ILive2DPackHandle pack)
    {
        EnsureMainThread();
        foreach (var subscription in GetProviderHooks(pack.OwnerModId))
            subscription.NotifyPackRegistered(pack);
    }

    internal static void NotifyModelAvailable(ILive2DModelHandle model)
    {
        EnsureMainThread();
        foreach (var subscription in GetProviderHooks(model.OwnerModId))
            subscription.NotifyModelAvailable(model);
    }

    internal static void NotifyModelUnavailable(ILive2DModelHandle model)
    {
        EnsureMainThread();
        foreach (var subscription in GetProviderHooks(model.OwnerModId))
            subscription.NotifyModelUnavailable(model);
    }

    internal static void NotifyPackUnregistered(ILive2DPackHandle pack)
    {
        EnsureMainThread();
        foreach (var subscription in GetProviderHooks(pack.OwnerModId))
            subscription.NotifyPackUnregistered(pack);
    }

    private static ProviderLifecycleSubscription[] GetProviderHooks(string ownerModId)
    {
        lock (ProviderHooks)
            return ProviderHooks
                .Where(subscription => subscription.Matches(ownerModId))
                .ToArray();
    }

    internal static void EnsureMainThread()
    {
        if (Live2DMainThreadDispatcher.IsReady
                ? !Live2DMainThreadDispatcher.IsCurrentThread
                : !NGame.IsMainThread())
            throw new InvalidOperationException(
                "Live2D runtime operations must be called from the Godot main thread.");
    }

    internal static void InitializeDispatcher(Action<Exception> unhandledExceptionHandler)
        => Live2DMainThreadDispatcher.Install(unhandledExceptionHandler);

    private static T WithPackPath<T>(string packagePath, Func<string, T> action)
    {
        if (!IsGodotPath(packagePath))
            return action(packagePath);
        return WithTemporaryPackFile(MaterializeGodotFile(packagePath), action);
    }

    private static T WithPackData<T>(ReadOnlyMemory<byte> packageData, Func<string, T> action)
    {
        if (packageData.IsEmpty)
            throw new InvalidDataException("Live2D pack data is empty.");

        var temporaryPath = CreateTemporaryPackPath();
        return WithTemporaryPackFile(temporaryPath, path =>
        {
            File.WriteAllBytes(path, packageData.Span);
            return action(path);
        });
    }

    private static T WithTemporaryPackFile<T>(string path, Func<string, T> action)
    {
        try
        {
            return action(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static bool IsGodotPath(string path)
        => path.StartsWith("res://", StringComparison.OrdinalIgnoreCase) ||
           path.StartsWith("user://", StringComparison.OrdinalIgnoreCase);

    private static string MaterializeGodotFile(string packagePath)
    {
        using var source = Godot.FileAccess.Open(packagePath, Godot.FileAccess.ModeFlags.Read);
        if (source is null)
            throw new IOException(
                $"Unable to open Live2D pack '{packagePath}': {Godot.FileAccess.GetOpenError()}");

        var temporaryPath = CreateTemporaryPackPath();
        try
        {
            using var destination = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                System.IO.FileAccess.Write,
                FileShare.None);
            while (!source.EofReached())
            {
                var buffer = source.GetBuffer(1024 * 1024);
                if (buffer.Length == 0)
                    break;
                destination.Write(buffer);
            }
            return temporaryPath;
        }
        catch
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
            throw;
        }
    }

    private static string CreateTemporaryPackPath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Live2D", "api-imports");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"{Guid.NewGuid():N}{PackageFileExtension}");
    }

    private static Live2DPackImportResult ConvertResult(Live2DPackImportSummary summary)
        => new(summary.ImportedModels, summary.SkippedDuplicates);

    internal static Live2DScene ToApiScene(Live2DSceneKind scene) => scene switch
    {
        Live2DSceneKind.MainMenu => Live2DScene.MainMenu,
        Live2DSceneKind.InGame => Live2DScene.InGame,
        _ => throw new ArgumentOutOfRangeException(nameof(scene), scene, null),
    };

    internal readonly record struct Live2DModelKey(string ModelId, Live2DScene Scene);

    private sealed class ProviderLifecycleSubscription : IDisposable
    {
        private readonly string _ownerModId;
        private readonly ILive2DProviderLifecycleHook _hook;
        private bool _disposed;

        internal ProviderLifecycleSubscription(
            string ownerModId,
            ILive2DProviderLifecycleHook hook)
        {
            _ownerModId = ownerModId;
            _hook = hook;
        }

        public void Dispose()
        {
            lock (ProviderHooks)
            {
                if (_disposed)
                    return;
                _disposed = true;
                ProviderHooks.Remove(this);
            }
        }

        internal bool Matches(string ownerModId)
            => !_disposed && string.Equals(
                ownerModId,
                _ownerModId,
                StringComparison.OrdinalIgnoreCase);

        internal void NotifyPackRegistered(ILive2DPackHandle pack)
            => Invoke("pack registered", () => _hook.OnPackRegistered(pack));

        internal void NotifyModelAvailable(ILive2DModelHandle model)
            => Invoke("model available", () => _hook.OnModelAvailable(model));

        internal void NotifyModelUnavailable(ILive2DModelHandle model)
            => Invoke("model unavailable", () => _hook.OnModelUnavailable(model));

        internal void NotifyPackUnregistered(ILive2DPackHandle pack)
            => Invoke("pack unregistered", () => _hook.OnPackUnregistered(pack));

        private void Invoke(string stage, Action callback)
        {
            if (_disposed)
                return;
            try
            {
                callback();
            }
            catch (Exception ex)
            {
                Entry.Logger.Error(
                    $"[{Entry.ModId}] Provider lifecycle hook '{stage}' for '{_ownerModId}' failed: {ex}");
            }
        }
    }
}
