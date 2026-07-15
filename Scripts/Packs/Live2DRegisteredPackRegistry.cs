using System.Security.Cryptography;
using System.Text;
using Live2D.Api;
using Live2D.Scripts.Configuration;
using Live2D.Scripts.Runtime;

namespace Live2D.Scripts.Packs;

internal static class Live2DRegisteredPackRegistry
{
    private static readonly object Gate = new();
    private static readonly string SessionRoot = Path.Combine(
        Path.GetTempPath(),
        "Live2D",
        "registered-packs",
        Guid.NewGuid().ToString("N"));
    private static readonly Dictionary<RegisteredPackKey, RegisteredPack> Packs =
        new(RegisteredPackKeyComparer.Instance);
    private static readonly Dictionary<string, RegisteredInstanceRequest> Requests =
        new(StringComparer.OrdinalIgnoreCase);
    private static Action<string>? _infoLogger;

    static Live2DRegisteredPackRegistry()
    {
        AppDomain.CurrentDomain.ProcessExit += (_, _) => TryDeleteDirectory(SessionRoot);
    }

    internal static void ConfigureLogging(Action<string> infoLogger)
        => _infoLogger = infoLogger;

    internal static ILive2DPackHandle Register(string ownerModId, string packagePath)
    {
        Live2DApi.EnsureMainThread();
        ValidateIdentifier(ownerModId, nameof(ownerModId));
        var fullPackagePath = Path.GetFullPath(packagePath);
        if (!File.Exists(fullPackagePath))
            throw new FileNotFoundException("Live2D pack does not exist.", fullPackagePath);

        var archiveHash = ComputeFileHash(fullPackagePath);
        var stagingDirectory = Path.Combine(SessionRoot, Guid.NewGuid().ToString("N"));
        try
        {
            var package = Live2DPackArchive.ReadToStaging(fullPackagePath, stagingDirectory);
            ValidateIdentifier(package.Manifest.PackageId, nameof(package.Manifest.PackageId));
            var key = new RegisteredPackKey(ownerModId, package.Manifest.PackageId);

            lock (Gate)
            {
                if (Packs.TryGetValue(key, out var existing))
                {
                    TryDeleteDirectory(stagingDirectory);
                    if (!string.Equals(existing.ArchiveHash, archiveHash, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException(
                            $"Mod '{ownerModId}' attempted to register different content with existing pack ID " +
                            $"'{package.Manifest.PackageId}'.");
                    return existing.Handle;
                }
            }

            var models = BuildModels(package);
            if (models.Count == 0)
                throw new InvalidDataException("A registered Live2D pack must contain at least one model.");
            var registeredPack = new RegisteredPack(
                key,
                string.IsNullOrWhiteSpace(package.Manifest.Name)
                    ? package.Manifest.PackageId
                    : package.Manifest.Name,
                archiveHash,
                stagingDirectory,
                models);
            registeredPack.Handle = new RegisteredLive2DPackHandle(registeredPack);

            lock (Gate)
                Packs.Add(key, registeredPack);

            _infoLogger?.Invoke(
                $"[{Entry.ModId}] Registered Live2D pack '{registeredPack.Name}' " +
                $"({ownerModId}/{registeredPack.Key.PackId}) with {models.Count} model(s).");
            return registeredPack.Handle;
        }
        catch
        {
            TryDeleteDirectory(stagingDirectory);
            throw;
        }
    }

    internal static IReadOnlyList<Live2DRuntimeModelDefinition> GetDefinitions(Live2DScene scene)
    {
        lock (Gate)
            return Requests.Values
                .Where(request => request.Definition.Identity.Scene == scene)
                .Select(request => request.Definition)
                .ToArray();
    }

    internal static ILive2DModelHandle CreateModel(
        RegisteredPack pack,
        string modelKey,
        Live2DCreateOptions options)
    {
        Live2DApi.EnsureMainThread();
        if (!pack.Handle.IsRegistered)
            throw new InvalidOperationException(
                $"Live2D pack '{pack.Key.OwnerModId}/{pack.Key.PackId}' is no longer registered.");
        ArgumentException.ThrowIfNullOrWhiteSpace(modelKey);
        if (!pack.Models.TryGetValue(modelKey, out var model))
            throw new KeyNotFoundException(
                $"Live2D pack '{pack.Key.PackId}' does not contain model key '{modelKey}'.");

        options.InitialState ??= new Live2DModelUpdate();
        options.InitialState.Validate();
        var instanceId = string.IsNullOrWhiteSpace(options.InstanceId)
            ? Guid.NewGuid().ToString("N")
            : options.InstanceId;
        ValidateIdentifier(instanceId, nameof(options.InstanceId));
        var runtimeId = CreateRuntimeId(pack.Key, options.Scene, instanceId);

        RegisteredInstanceRequest request;
        lock (Gate)
        {
            if (Requests.TryGetValue(runtimeId, out request!))
            {
                if (!string.Equals(request.Definition.Identity.ModelKey, model.ModelKey,
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        $"Instance ID '{instanceId}' is already used by model " +
                        $"'{request.Definition.Identity.ModelKey}' in {options.Scene}.");
            }
            else
            {
                var identity = new Live2DRuntimeModelIdentity(
                    runtimeId,
                    pack.Key.OwnerModId,
                    pack.Key.PackId,
                    model.ModelKey,
                    instanceId,
                    options.Scene);
                var config = CloneForRuntime(model.Config, identity.RuntimeId, model.AssetPath);
                request = new RegisteredInstanceRequest(
                    new Live2DRuntimeModelDefinition(identity, config, model.AssetPath));
                Requests.Add(runtimeId, request);
            }
        }

        var handle = Live2DApi.ReserveModel(
            request.Definition,
            () => RemoveInstance(request.Definition.Identity.RuntimeId));
        handle.Apply(options.InitialState);
        Live2DRuntimeManager.RefreshAll();
        return handle;
    }

    internal static void Unregister(RegisteredPack pack)
    {
        Live2DApi.EnsureMainThread();
        Live2DRuntimeModelIdentity[] removedIdentities;
        lock (Gate)
        {
            if (!pack.Handle.IsRegistered)
                return;
            pack.Handle.MarkUnregistered();
            Packs.Remove(pack.Key);
            removedIdentities = Requests.Values
                .Select(request => request.Definition.Identity)
                .Where(identity =>
                    string.Equals(identity.OwnerModId, pack.Key.OwnerModId,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(identity.PackId, pack.Key.PackId,
                        StringComparison.OrdinalIgnoreCase))
                .ToArray();
            foreach (var identity in removedIdentities)
                Requests.Remove(identity.RuntimeId);
        }

        foreach (var identity in removedIdentities)
            Live2DApi.DisableDestroy(identity);

        Live2DRuntimeManager.RefreshAll();
        _infoLogger?.Invoke(
            $"[{Entry.ModId}] Unregistered Live2D pack {pack.Key.OwnerModId}/{pack.Key.PackId}.");
    }

    private static void RemoveInstance(string runtimeId)
    {
        lock (Gate)
            Requests.Remove(runtimeId);
        Live2DRuntimeManager.RefreshAll();
    }

    private static Dictionary<string, RegisteredPackModel> BuildModels(Live2DPackReadResult package)
    {
        var models = new Dictionary<string, RegisteredPackModel>(StringComparer.OrdinalIgnoreCase);
        foreach (var manifestModel in package.Manifest.Models)
        {
            var modelKey = string.IsNullOrWhiteSpace(manifestModel.ModelKey)
                ? manifestModel.OriginalId
                : manifestModel.ModelKey;
            ValidateIdentifier(modelKey, nameof(manifestModel.ModelKey));
            var config = package.Models.FirstOrDefault(model =>
                             string.Equals(model.Id, manifestModel.OriginalId, StringComparison.OrdinalIgnoreCase))
                         ?? throw new InvalidDataException(
                             $"Pack model configuration is missing: {manifestModel.OriginalId}");
            if (!package.ExtractedEntryPaths.TryGetValue(manifestModel.OriginalId, out var assetPath))
                throw new InvalidDataException(
                    $"Pack model assets are missing: {manifestModel.OriginalId}");
            if (!models.TryAdd(modelKey, new RegisteredPackModel(
                    modelKey,
                    config.DisplayName,
                    string.IsNullOrWhiteSpace(manifestModel.ContentHash)
                        ? config.ContentHash
                        : manifestModel.ContentHash,
                    config,
                    assetPath)))
                throw new InvalidDataException($"Pack contains duplicate model key: {modelKey}");
        }
        return models;
    }

    private static Live2DModelConfig CloneForRuntime(
        Live2DModelConfig source,
        string runtimeId,
        string assetPath)
        => new()
        {
            Id = runtimeId,
            DisplayName = source.DisplayName,
            ModelRelativePath = "",
            SourcePath = assetPath,
            ContentHash = source.ContentHash,
            ImportedAt = source.ImportedAt,
            DisplayOrder = source.DisplayOrder,
            Overrides = source.Overrides,
            AvailableActions = source.AvailableActions,
            ActionBindings = [],
        };

    private static string CreateRuntimeId(
        RegisteredPackKey key,
        Live2DScene scene,
        string instanceId)
    {
        var text = $"{key.OwnerModId.ToUpperInvariant()}\0" +
                   $"{key.PackId.ToUpperInvariant()}\0{scene}\0{instanceId.ToUpperInvariant()}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
        return "ext_" + hash[..24];
    }

    private static string ComputeFileHash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static void ValidateIdentifier(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Length > 128 || value.Any(char.IsControl))
            throw new ArgumentException(
                "Identifier must be at most 128 characters and cannot contain control characters.",
                parameterName);
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // The OS temp directory can clean up a cache still held by the native runtime.
        }
    }

    internal sealed record RegisteredPackKey(string OwnerModId, string PackId);

    private sealed class RegisteredPackKeyComparer : IEqualityComparer<RegisteredPackKey>
    {
        internal static readonly RegisteredPackKeyComparer Instance = new();

        public bool Equals(RegisteredPackKey? x, RegisteredPackKey? y)
            => x is not null && y is not null &&
               string.Equals(x.OwnerModId, y.OwnerModId, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(x.PackId, y.PackId, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(RegisteredPackKey value)
            => HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(value.OwnerModId),
                StringComparer.OrdinalIgnoreCase.GetHashCode(value.PackId));
    }

    internal sealed class RegisteredPack
    {
        internal RegisteredPack(
            RegisteredPackKey key,
            string name,
            string archiveHash,
            string cacheDirectory,
            Dictionary<string, RegisteredPackModel> models)
        {
            Key = key;
            Name = name;
            ArchiveHash = archiveHash;
            CacheDirectory = cacheDirectory;
            Models = models;
            Handle = null!;
        }

        internal RegisteredPackKey Key { get; }
        internal string Name { get; }
        internal string ArchiveHash { get; }
        internal string CacheDirectory { get; }
        internal Dictionary<string, RegisteredPackModel> Models { get; }
        internal RegisteredLive2DPackHandle Handle { get; set; }
    }

    internal sealed record RegisteredPackModel(
        string ModelKey,
        string DisplayName,
        string ContentHash,
        Live2DModelConfig Config,
        string AssetPath);

    private sealed record RegisteredInstanceRequest(Live2DRuntimeModelDefinition Definition);
}
