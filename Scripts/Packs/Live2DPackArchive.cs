using System.IO.Compression;
using System.Text.Json;
using Live2D.Api;
using Live2D.Scripts.Configuration;

namespace Live2D.Scripts.Packs;

internal sealed class Live2DPackManifest
{
    public const int CurrentFormatVersion = 1;

    public int FormatVersion { get; set; } = CurrentFormatVersion;
    public string PackageId { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Live2D Package";
    public string Author { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string MinimumModVersion { get; set; } = Entry.ModVersion;
    public int SettingsSchemaVersion { get; set; } = Live2DSettings.CurrentSchemaVersion;
    public bool IncludesGlobalConfig { get; set; }
    public List<Live2DPackModelEntry> Models { get; set; } = [];
}

internal sealed class Live2DPackModelEntry
{
    public string OriginalId { get; set; } = "";
    public string ModelKey { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string EntryPath { get; set; } = "";
    public string ContentHash { get; set; } = "";
}

internal sealed record Live2DPackReadResult(
    Live2DPackManifest Manifest,
    GlobalLive2DConfig? Global,
    IReadOnlyList<Live2DModelConfig> Models,
    IReadOnlyDictionary<string, string> ExtractedEntryPaths);

internal static class Live2DPackArchive
{
    private const long MaximumEntryBytes = 512L * 1024 * 1024;
    private const long MaximumTotalBytes = 2L * 1024 * 1024 * 1024;
    private const int MaximumEntryCount = 10_000;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public static void Write(
        string destinationPath,
        Live2DSettings settings,
        string modelsDirectory,
        bool includeGlobalConfig,
        string? packageName = null,
        string? packageAuthor = null,
        string? packageDescription = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        ArgumentNullException.ThrowIfNull(settings);
        ValidatePackageExtension(destinationPath);
        var fullDestination = Path.GetFullPath(destinationPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullDestination)!);
        var temporaryPath = fullDestination + ".tmp-" + Guid.NewGuid().ToString("N");

        var manifest = new Live2DPackManifest
        {
            Name = string.IsNullOrWhiteSpace(packageName) ? Path.GetFileNameWithoutExtension(fullDestination) : packageName,
            Author = packageAuthor ?? "",
            Description = packageDescription ?? "",
            IncludesGlobalConfig = includeGlobalConfig,
            SettingsSchemaVersion = settings.SchemaVersion,
            Models = settings.Models.Select(model => new Live2DPackModelEntry
            {
                OriginalId = model.Id,
                ModelKey = model.Id,
                DisplayName = model.DisplayName,
                EntryPath = $"models/{model.Id}/{Path.GetFileName(model.ModelRelativePath)}",
                ContentHash = model.ContentHash,
            }).ToList(),
        };

        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false))
            {
                WriteJson(archive, "manifest.json", manifest);
                WriteJson(archive, "settings/models.json", settings.Models);
                if (includeGlobalConfig)
                    WriteJson(archive, "settings/global.json", settings.Global);

                foreach (var model in settings.Models)
                {
                    var modelRoot = GetContainedPath(modelsDirectory, model.Id);
                    if (!Directory.Exists(modelRoot))
                        throw new DirectoryNotFoundException($"Managed model directory is missing: {modelRoot}");

                    foreach (var file in Directory.EnumerateFiles(modelRoot, "*", SearchOption.AllDirectories))
                    {
                        if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                            throw new InvalidDataException($"Symbolic links/reparse points cannot be exported: {file}");
                        var relative = Path.GetRelativePath(modelRoot, file).Replace('\\', '/');
                        var entryName = ValidateArchivePath($"models/{model.Id}/{relative}");
                        archive.CreateEntryFromFile(file, entryName, CompressionLevel.Optimal);
                    }
                }
            }

            File.Move(temporaryPath, fullDestination, overwrite: true);
        }
        catch
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
            throw;
        }
    }

    public static Live2DPackManifest ReadManifest(string packagePath)
    {
        ValidatePackageExtension(packagePath);
        var fullPackagePath = Path.GetFullPath(packagePath);
        if (!File.Exists(fullPackagePath))
            throw new FileNotFoundException("Live2D package does not exist.", fullPackagePath);
        using var archive = ZipFile.OpenRead(fullPackagePath);
        var matches = archive.Entries
            .Where(entry => string.Equals(ValidateArchivePath(entry.FullName), "manifest.json",
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (matches.Length != 1)
            throw new InvalidDataException("Package must contain exactly one manifest.json entry.");
        var manifest = ReadJson<Live2DPackManifest>(matches[0]);
        if (manifest.FormatVersion != Live2DPackManifest.CurrentFormatVersion)
            throw new InvalidDataException($"Unsupported Live2D package version: {manifest.FormatVersion}");
        return manifest;
    }

    public static Live2DPackReadResult ReadToStaging(string packagePath, string stagingDirectory)
    {
        ValidatePackageExtension(packagePath);
        var fullPackagePath = Path.GetFullPath(packagePath);
        if (!File.Exists(fullPackagePath))
            throw new FileNotFoundException("Live2D package does not exist.", fullPackagePath);

        Directory.CreateDirectory(stagingDirectory);
        using var archive = ZipFile.OpenRead(fullPackagePath);
        if (archive.Entries.Count > MaximumEntryCount)
            throw new InvalidDataException($"Package contains too many entries ({archive.Entries.Count}).");

        var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
        long totalBytes = 0;
        foreach (var entry in archive.Entries)
        {
            var name = ValidateArchivePath(entry.FullName);
            if (!entries.TryAdd(name, entry))
                throw new InvalidDataException($"Package contains a duplicate entry: {name}");
            if (IsSymbolicLink(entry))
                throw new InvalidDataException($"Package contains a symbolic link: {name}");
            if (entry.Length > MaximumEntryBytes)
                throw new InvalidDataException($"Package entry is too large: {name}");
            totalBytes = checked(totalBytes + entry.Length);
            if (totalBytes > MaximumTotalBytes)
                throw new InvalidDataException("Package expands beyond the permitted size.");
            if (entry.CompressedLength > 0 && entry.Length / Math.Max(1d, entry.CompressedLength) > 1000d)
                throw new InvalidDataException($"Package entry has a suspicious compression ratio: {name}");
        }

        var manifest = ReadJson<Live2DPackManifest>(GetRequired(entries, "manifest.json"));
        if (manifest.FormatVersion != Live2DPackManifest.CurrentFormatVersion)
            throw new InvalidDataException($"Unsupported Live2D package version: {manifest.FormatVersion}");
        if (manifest.SettingsSchemaVersion != Live2DSettings.CurrentSchemaVersion)
            throw new InvalidDataException(
                $"Unsupported package settings schema: {manifest.SettingsSchemaVersion}. " +
                $"Expected {Live2DSettings.CurrentSchemaVersion}.");

        var models = ReadJson<List<Live2DModelConfig>>(GetRequired(entries, "settings/models.json"));
        var modelById = models.ToDictionary(model => model.Id, StringComparer.OrdinalIgnoreCase);
        GlobalLive2DConfig? global = null;
        if (manifest.IncludesGlobalConfig)
            global = ReadJson<GlobalLive2DConfig>(GetRequired(entries, "settings/global.json"));

        var normalizedSettings = new Live2DSettings
        {
            SchemaVersion = manifest.SettingsSchemaVersion,
            Global = global ?? new GlobalLive2DConfig(),
            Models = models,
        };
        Live2DConfigNormalizer.NormalizeInPlace(normalizedSettings);
        models = normalizedSettings.Models;
        if (manifest.IncludesGlobalConfig)
            global = normalizedSettings.Global;

        var extractedEntries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var modelEntry in manifest.Models)
        {
            if (string.IsNullOrWhiteSpace(modelEntry.OriginalId) || !modelById.ContainsKey(modelEntry.OriginalId))
                throw new InvalidDataException($"Package model metadata is missing for id '{modelEntry.OriginalId}'.");
            var expectedPrefix = $"models/{modelEntry.OriginalId}/";
            var normalizedEntryPath = ValidateArchivePath(modelEntry.EntryPath);
            if (!normalizedEntryPath.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase)
                || !normalizedEntryPath.EndsWith(".model3.json", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Invalid model entry path: {modelEntry.EntryPath}");

            foreach (var (name, entry) in entries.Where(pair =>
                         pair.Key.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase)))
            {
                if (name.EndsWith('/'))
                    continue;
                var destination = GetContainedPath(stagingDirectory, name);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                using var source = entry.Open();
                using var target = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                source.CopyTo(target);
            }

            var extractedEntry = GetContainedPath(stagingDirectory, normalizedEntryPath);
            if (!File.Exists(extractedEntry))
                throw new InvalidDataException($"Package model entry is missing: {normalizedEntryPath}");
            extractedEntries[modelEntry.OriginalId] = extractedEntry;
        }

        return new Live2DPackReadResult(manifest, global, models, extractedEntries);
    }

    internal static void ValidatePackageExtension(string packagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        if (!packagePath.EndsWith(Live2DApi.PackageFileExtension, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(
                $"Live2D packages must use the '{Live2DApi.PackageFileExtension}' extension.");
    }

    private static void WriteJson<T>(ZipArchive archive, string name, T value)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = entry.Open();
        JsonSerializer.Serialize(stream, value, JsonOptions);
    }

    private static T ReadJson<T>(ZipArchiveEntry entry)
    {
        if (entry.Length > 16 * 1024 * 1024)
            throw new InvalidDataException($"JSON metadata entry is too large: {entry.FullName}");
        using var stream = entry.Open();
        return JsonSerializer.Deserialize<T>(stream, JsonOptions)
               ?? throw new InvalidDataException($"Unable to deserialize package metadata: {entry.FullName}");
    }

    private static ZipArchiveEntry GetRequired(
        IReadOnlyDictionary<string, ZipArchiveEntry> entries,
        string name)
        => entries.TryGetValue(name, out var entry)
            ? entry
            : throw new InvalidDataException($"Package is missing required entry: {name}");

    private static string ValidateArchivePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidDataException("Package contains an empty entry path.");
        var normalized = path.Replace('\\', '/');
        if (normalized.StartsWith('/') || Path.IsPathRooted(normalized))
            throw new InvalidDataException($"Package contains an absolute path: {path}");
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => segment is "." or ".."))
            throw new InvalidDataException($"Package path escapes its root: {path}");
        return string.Join('/', segments) + (normalized.EndsWith('/') ? "/" : "");
    }

    private static string GetContainedPath(string rootDirectory, string relativePath)
    {
        var root = Path.GetFullPath(rootDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                   + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Path escapes the intended directory: {relativePath}");
        return candidate;
    }

    private static bool IsSymbolicLink(ZipArchiveEntry entry)
    {
        const int unixFileTypeMask = 0xF000;
        const int unixSymbolicLink = 0xA000;
        return ((entry.ExternalAttributes >> 16) & unixFileTypeMask) == unixSymbolicLink;
    }
}
