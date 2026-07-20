using System.Security.Cryptography;
using Godot;
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

            RegisteredPack? existingPack;
            lock (Gate)
                Packs.TryGetValue(key, out existingPack);
            if (existingPack != null)
            {
                TryDeleteDirectory(stagingDirectory);
                if (!string.Equals(existingPack.ArchiveHash, archiveHash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        $"Mod '{ownerModId}' attempted to register different content with existing pack ID " +
                        $"'{package.Manifest.PackageId}'.");
                Live2DConfigStore.UpsertExternalPack(existingPack);
                return existingPack.Handle;
            }

            var models = BuildModels(package);
            if (models.Count == 0)
                throw new InvalidDataException("A registered Live2D pack must contain at least one model.");

            var pack = new RegisteredPack(
                key,
                string.IsNullOrWhiteSpace(package.Manifest.Name)
                    ? package.Manifest.PackageId
                    : package.Manifest.Name,
                archiveHash,
                stagingDirectory,
                models);
            pack.Handle = new RegisteredLive2DPackHandle(pack);

            lock (Gate)
                Packs.Add(key, pack);
            Live2DApi.NotifyPackRegistered(pack.Handle);
            try
            {
                Live2DConfigStore.UpsertExternalPack(pack);
            }
            catch
            {
                lock (Gate)
                    Packs.Remove(key);
                pack.Handle.MarkUnregistered();
                Live2DApi.NotifyPackUnregistered(pack.Handle);
                throw;
            }

            _infoLogger?.Invoke(
                $"[{Entry.ModId}] Registered Live2D pack '{pack.Name}' " +
                $"({ownerModId}/{pack.Key.PackId}) with {models.Count} model(s) in the model library.");
            return pack.Handle;
        }
        catch
        {
            TryDeleteDirectory(stagingDirectory);
            throw;
        }
    }

    internal static bool TryGetLibraryModelAsset(
        Live2DModelConfig config,
        out string assetPath)
    {
        assetPath = "";
        if (!config.IsExternalPackModel)
            return false;

        lock (Gate)
        {
            var key = new RegisteredPackKey(config.ExternalOwnerModId, config.ExternalPackId);
            if (!Packs.TryGetValue(key, out var pack) ||
                !pack.Models.TryGetValue(config.ExternalModelKey, out var model))
                return false;
            assetPath = model.AssetPath;
            return File.Exists(assetPath);
        }
    }

    internal static IReadOnlyList<ILive2DPackHandle> GetRegisteredPacks(string ownerModId)
    {
        lock (Gate)
            return Packs.Values
                .Where(pack => string.Equals(
                    pack.Key.OwnerModId,
                    ownerModId,
                    StringComparison.OrdinalIgnoreCase))
                .Select(pack => (ILive2DPackHandle)pack.Handle)
                .ToArray();
    }

    internal static IReadOnlyList<RegisteredPack> GetRegisteredPacksSnapshot()
    {
        lock (Gate)
            return Packs.Values.ToArray();
    }

    internal static void Unregister(RegisteredPack pack)
    {
        Live2DApi.EnsureMainThread();
        lock (Gate)
        {
            if (!pack.Handle.IsRegistered)
                return;
            pack.Handle.MarkUnregistered();
            Packs.Remove(pack.Key);
        }

        Live2DRuntimeManager.RefreshAll();
        Live2DHotkeyManager.Refresh();
        TryDeleteDirectory(pack.CacheDirectory);
        // RefreshAll queues scene rebuilds. Queue this callback afterward so every
        // available model reaches OnModelUnavailable before OnPackUnregistered.
        Callable.From(() => Live2DApi.NotifyPackUnregistered(pack.Handle)).CallDeferred();
        _infoLogger?.Invoke(
            $"[{Entry.ModId}] Unregistered Live2D pack {pack.Key.OwnerModId}/{pack.Key.PackId}.");
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
}
