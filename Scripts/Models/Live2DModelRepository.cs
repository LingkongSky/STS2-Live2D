using Godot;
using Live2D.Scripts.Configuration;
using Live2D.Scripts.Packs;

namespace Live2D.Scripts.Models;

internal static class Live2DModelRepository
{
    public static string ModelsDirectory => Path.Combine(OS.GetUserDataDir(), "mods", Entry.ModId, "models");

    public static Live2DModelConfig Import(string modelJsonPath)
        => Import(Inspect(modelJsonPath));

    public static ParsedLive2DModel Inspect(string modelJsonPath)
        => Live2DModelManifestParser.Parse(modelJsonPath);

    public static Live2DModelConfig Import(ParsedLive2DModel parsed)
    {
        ArgumentNullException.ThrowIfNull(parsed);
        var modelId = Guid.NewGuid().ToString("N");
        var destinationRoot = Path.Combine(ModelsDirectory, modelId);
        Directory.CreateDirectory(destinationRoot);

        try
        {
            var sourceRoot = Path.GetDirectoryName(parsed.EntryPath)!;
            foreach (var relative in parsed.RelativeFiles)
            {
                var source = Live2DModelManifestParser.ResolveContainedExistingFile(sourceRoot, relative);
                var destination = Path.GetFullPath(Path.Combine(destinationRoot, relative));
                EnsureContained(destinationRoot, destination);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                if (parsed.GeneratedEntryContents != null
                    && source.Equals(parsed.EntryPath, StringComparison.OrdinalIgnoreCase))
                    File.WriteAllBytes(destination, parsed.GeneratedEntryContents);
                else
                    File.Copy(source, destination, overwrite: true);
            }

            return new Live2DModelConfig
            {
                Id = modelId,
                DisplayName = GetModelDisplayName(parsed.EntryPath),
                ModelRelativePath = Path.Combine(modelId, Path.GetFileName(parsed.EntryPath)).Replace('\\', '/'),
                SourcePath = parsed.EntryPath,
                ContentHash = parsed.ContentHash,
                ImportedAt = DateTimeOffset.UtcNow,
                AvailableActions = parsed.Actions.ToList(),
            };
        }
        catch
        {
            if (Directory.Exists(destinationRoot))
                Directory.Delete(destinationRoot, recursive: true);
            throw;
        }
    }

    public static string GetAbsoluteModelPath(Live2DModelConfig model)
    {
        if (model.IsExternalPackModel)
        {
            if (Live2DRegisteredPackRegistry.TryGetLibraryModelAsset(model, out var assetPath))
                return assetPath;
            throw new FileNotFoundException(
                $"External Live2D pack model is not registered: " +
                $"{model.ExternalOwnerModId}/{model.ExternalPackId}/{model.ExternalModelKey}");
        }

        var fullPath = Path.GetFullPath(Path.Combine(ModelsDirectory, model.ModelRelativePath));
        EnsureContained(ModelsDirectory, fullPath);
        return fullPath;
    }

    public static bool IsManagedModelAvailable(Live2DModelConfig model, out string reason)
    {
        try
        {
            var path = GetAbsoluteModelPath(model);
            if (File.Exists(path))
            {
                reason = "";
                return true;
            }
            reason = $"entry file is missing: {path}";
            return false;
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException or NotSupportedException)
        {
            reason = $"managed path is invalid: {ex.Message}";
            return false;
        }
    }

    public static bool IsModelAvailable(Live2DModelConfig model, out string reason)
    {
        if (!model.IsExternalPackModel)
            return IsManagedModelAvailable(model, out reason);

        if (Live2DRegisteredPackRegistry.TryGetLibraryModelAsset(model, out _))
        {
            reason = "";
            return true;
        }

        reason = $"provider model is not registered: " +
                 $"{model.ExternalOwnerModId}/{model.ExternalPackId}/{model.ExternalModelKey}";
        return false;
    }

    public static void DeleteFiles(string modelId)
    {
        var directory = Path.GetFullPath(Path.Combine(ModelsDirectory, modelId));
        EnsureContained(ModelsDirectory, directory);
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }

    private static string GetModelDisplayName(string path)
    {
        const string suffix = ".model3.json";
        var name = Path.GetFileName(path);
        return name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) ? name[..^suffix.Length] : name;
    }

    private static void EnsureContained(string rootDirectory, string candidate)
    {
        var root = Path.GetFullPath(rootDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                   + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(candidate);
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Path escapes the Live2D model repository: {candidate}");
    }
}
