using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Live2D.Scripts.Configuration;

namespace Live2D.Scripts.Models;

internal sealed record ParsedLive2DModel(
    string EntryPath,
    IReadOnlyList<string> RelativeFiles,
    IReadOnlyList<Live2DActionDescriptor> Actions,
    string ContentHash,
    byte[]? GeneratedEntryContents);

internal static class Live2DModelManifestParser
{
    public static ParsedLive2DModel Parse(string modelJsonPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelJsonPath);
        var entryPath = Path.GetFullPath(modelJsonPath);
        if (!File.Exists(entryPath))
            throw new FileNotFoundException("Live2D model entry file does not exist.", entryPath);
        if (!entryPath.EndsWith(".model3.json", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The selected file must use the .model3.json suffix.");

        var root = Path.GetDirectoryName(entryPath)
                   ?? throw new InvalidDataException("Unable to resolve the model source directory.");
        var sourceContents = File.ReadAllBytes(entryPath);
        using var document = JsonDocument.Parse(sourceContents, new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip,
        });

        if (!document.RootElement.TryGetProperty("FileReferences", out var references)
            || references.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("The model does not contain a valid FileReferences object.");

        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.GetFileName(entryPath),
        };
        var actions = new List<Live2DActionDescriptor>();
        var declaredExpressionFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var declaredMotionFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        byte[]? generatedEntryContents = null;

        AddOptionalFile(references, "Moc", files);
        AddOptionalFile(references, "Physics", files);
        AddOptionalFile(references, "Pose", files);
        AddOptionalFile(references, "DisplayInfo", files);
        AddOptionalFile(references, "UserData", files);

        if (references.TryGetProperty("Textures", out var textures) && textures.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in textures.EnumerateArray())
                AddStringValue(item, files);
        }

        if (references.TryGetProperty("Expressions", out var expressions)
            && expressions.ValueKind == JsonValueKind.Array)
        {
            foreach (var expression in expressions.EnumerateArray())
            {
                var name = GetString(expression, "Name");
                var file = GetString(expression, "File");
                AddRelative(file, files);
                AddCanonicalRelative(file, declaredExpressionFiles);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    actions.Add(new Live2DActionDescriptor
                    {
                        Kind = Live2DActionKind.Expression,
                        DisplayName = name,
                        ExpressionId = name,
                    });
                }
            }
        }

        if (references.TryGetProperty("Motions", out var motions) && motions.ValueKind == JsonValueKind.Object)
        {
            foreach (var group in motions.EnumerateObject())
            {
                if (group.Value.ValueKind != JsonValueKind.Array)
                    continue;
                var index = 0;
                foreach (var motion in group.Value.EnumerateArray())
                {
                    var file = GetString(motion, "File");
                    AddRelative(file, files);
                    AddCanonicalRelative(file, declaredMotionFiles);
                    actions.Add(new Live2DActionDescriptor
                    {
                        Kind = Live2DActionKind.Motion,
                        DisplayName = $"{group.Name} / {index}",
                        MotionGroup = group.Name,
                        MotionIndex = index,
                    });
                    index++;
                }
            }
        }

        var discovered = DiscoverUndeclaredActions(root, entryPath);
        var newExpressions = discovered.Expressions
            .Where(expression => !declaredExpressionFiles.Contains(CanonicalRelativePath(expression.RelativePath)))
            .ToArray();
        var newMotionGroups = discovered.MotionGroups
            .Select(group => new DiscoveredMotionGroup(
                group.Name,
                group.Files.Where(file => !declaredMotionFiles.Contains(CanonicalRelativePath(file))).ToArray()))
            .Where(group => group.Files.Count > 0)
            .ToArray();
        if (newExpressions.Length > 0 || newMotionGroups.Length > 0)
        {
            var generatedRoot = JsonNode.Parse(sourceContents, documentOptions: new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            })?.AsObject() ?? throw new InvalidDataException("The model entry is not a JSON object.");
            var generatedReferences = generatedRoot["FileReferences"]?.AsObject()
                                      ?? throw new InvalidDataException("The model does not contain a valid FileReferences object.");

            if (newExpressions.Length > 0)
            {
                var generatedExpressions = generatedReferences["Expressions"] as JsonArray;
                if (generatedExpressions == null)
                {
                    generatedExpressions = new JsonArray();
                    generatedReferences["Expressions"] = generatedExpressions;
                }
                var usedNames = actions
                    .Where(action => action.Kind == Live2DActionKind.Expression)
                    .Select(action => action.ExpressionId)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var expression in newExpressions)
                {
                    var uniqueName = CreateUniqueName(expression.Name, usedNames);
                    generatedExpressions.Add(new JsonObject
                    {
                        ["Name"] = uniqueName,
                        ["File"] = ToManifestPath(expression.RelativePath),
                    });
                    files.Add(expression.RelativePath);
                    actions.Add(new Live2DActionDescriptor
                    {
                        Kind = Live2DActionKind.Expression,
                        DisplayName = uniqueName,
                        ExpressionId = uniqueName,
                    });
                }
            }

            if (newMotionGroups.Length > 0)
            {
                var generatedMotions = generatedReferences["Motions"] as JsonObject;
                if (generatedMotions == null)
                {
                    generatedMotions = new JsonObject();
                    generatedReferences["Motions"] = generatedMotions;
                }
                foreach (var group in newMotionGroups)
                {
                    var generatedGroup = generatedMotions[group.Name] as JsonArray;
                    if (generatedGroup == null)
                    {
                        generatedGroup = new JsonArray();
                        generatedMotions[group.Name] = generatedGroup;
                    }
                    var startIndex = generatedGroup.Count;
                    for (var offset = 0; offset < group.Files.Count; offset++)
                    {
                        var file = group.Files[offset];
                        generatedGroup.Add(new JsonObject { ["File"] = ToManifestPath(file) });
                        files.Add(file);
                        actions.Add(new Live2DActionDescriptor
                        {
                            Kind = Live2DActionKind.Motion,
                            DisplayName = $"{group.Name} / {startIndex + offset}",
                            MotionGroup = group.Name,
                            MotionIndex = startIndex + offset,
                        });
                    }
                }
            }

            generatedEntryContents = JsonSerializer.SerializeToUtf8Bytes(generatedRoot, new JsonSerializerOptions
            {
                WriteIndented = true,
            });
        }

        var normalizedFiles = files.Select(NormalizeRelativePath).Order(StringComparer.OrdinalIgnoreCase).ToArray();
        foreach (var relativeFile in normalizedFiles)
            ResolveContainedExistingFile(root, relativeFile);

        return new ParsedLive2DModel(
            entryPath,
            normalizedFiles,
            actions,
            ComputeHash(root, normalizedFiles, Path.GetFileName(entryPath), generatedEntryContents),
            generatedEntryContents);
    }

    public static string ResolveContainedExistingFile(string rootDirectory, string relativePath)
    {
        var root = Path.GetFullPath(rootDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                   + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Model dependency escapes the source directory: {relativePath}");
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Missing Live2D model dependency: {relativePath}", fullPath);
        return fullPath;
    }

    private static void AddOptionalFile(JsonElement owner, string propertyName, HashSet<string> files)
    {
        if (owner.TryGetProperty(propertyName, out var value))
            AddStringValue(value, files);
    }

    private static void AddStringValue(JsonElement value, HashSet<string> files)
    {
        if (value.ValueKind == JsonValueKind.String)
            AddRelative(value.GetString(), files);
    }

    private static void AddRelative(string? value, HashSet<string> files)
    {
        if (!string.IsNullOrWhiteSpace(value))
            files.Add(value);
    }

    private static void AddCanonicalRelative(string? value, HashSet<string> files)
    {
        if (!string.IsNullOrWhiteSpace(value))
            files.Add(CanonicalRelativePath(value));
    }

    private static string GetString(JsonElement owner, string propertyName)
        => owner.ValueKind == JsonValueKind.Object
           && owner.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";

    private static string NormalizeRelativePath(string path)
    {
        var normalized = path.Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(normalized))
            throw new InvalidDataException($"Absolute model dependency paths are not allowed: {path}");
        return normalized;
    }

    private static string CanonicalRelativePath(string path)
        => NormalizeRelativePath(path).Replace('\\', '/');

    private static DiscoveredActions DiscoverUndeclaredActions(
        string root,
        string entryPath)
    {
        var vtube = ReadVTubeStudioMetadata(root, Path.GetFileName(entryPath));
        var expressionPaths = new List<string>();
        var idlePaths = new List<string>();
        var hotkeyMotionPaths = new List<string>();

        foreach (var file in vtube.ExpressionFiles)
            AddResolvedAsset(root, "expressions", file, expressionPaths);
        AddFilesBySuffix(root, ".exp3.json", expressionPaths);

        AddResolvedAsset(root, "animations", vtube.IdleAnimation, idlePaths);
        foreach (var file in vtube.MotionFiles)
            AddResolvedAsset(root, "animations", file, hotkeyMotionPaths);

        var allMotions = new List<string>();
        AddFilesBySuffix(root, ".motion3.json", allMotions);
        foreach (var file in allMotions)
        {
            if (idlePaths.Contains(file, StringComparer.OrdinalIgnoreCase)
                || hotkeyMotionPaths.Contains(file, StringComparer.OrdinalIgnoreCase))
                continue;
            if (Path.GetFileName(file).StartsWith("idle", StringComparison.OrdinalIgnoreCase))
                idlePaths.Add(file);
            else
                hotkeyMotionPaths.Add(file);
        }

        var expressions = expressionPaths
            .Select(path => new DiscoveredExpression(StripCompoundSuffix(Path.GetFileName(path), ".exp3.json"), path))
            .ToArray();
        var groups = new List<DiscoveredMotionGroup>();
        if (idlePaths.Count > 0)
            groups.Add(new DiscoveredMotionGroup("Idle", idlePaths));
        if (hotkeyMotionPaths.Count > 0)
            groups.Add(new DiscoveredMotionGroup("Hotkey", hotkeyMotionPaths));
        return new DiscoveredActions(expressions, groups);
    }

    private static VTubeStudioMetadata ReadVTubeStudioMetadata(string root, string modelFileName)
    {
        foreach (var metadataPath in Directory.EnumerateFiles(root, "*.vtube.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllBytes(metadataPath), new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip,
                });
                var metadataRoot = document.RootElement;
                if (metadataRoot.ValueKind != JsonValueKind.Object)
                    continue;
                if (!metadataRoot.TryGetProperty("FileReferences", out var fileReferences)
                    || fileReferences.ValueKind != JsonValueKind.Object
                    || !string.Equals(GetString(fileReferences, "Model"), modelFileName, StringComparison.OrdinalIgnoreCase))
                    continue;

                var idle = GetString(fileReferences, "IdleAnimation");
                var expressions = new List<string>();
                var motions = new List<string>();
                if (metadataRoot.TryGetProperty("Hotkeys", out var hotkeys) && hotkeys.ValueKind == JsonValueKind.Array)
                {
                    foreach (var hotkey in hotkeys.EnumerateArray())
                    {
                        if (hotkey.ValueKind != JsonValueKind.Object)
                            continue;
                        var action = GetString(hotkey, "Action");
                        var file = GetString(hotkey, "File");
                        if (action.Equals("ToggleExpression", StringComparison.OrdinalIgnoreCase))
                            AddDistinct(expressions, file);
                        else if (action.Equals("TriggerAnimation", StringComparison.OrdinalIgnoreCase))
                            AddDistinct(motions, file);
                    }
                }
                return new VTubeStudioMetadata(idle, expressions, motions);
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException)
            {
                // A malformed optional VTube Studio sidecar must not break a valid Cubism model import.
            }
        }
        return new VTubeStudioMetadata("", [], []);
    }

    private static void AddResolvedAsset(string root, string conventionalDirectory, string file, List<string> paths)
    {
        if (string.IsNullOrWhiteSpace(file))
            return;

        var normalized = NormalizeRelativePath(file);
        var candidates = new[]
        {
            normalized,
            Path.Combine(conventionalDirectory, normalized),
        };
        foreach (var candidate in candidates)
        {
            var fullPath = Path.GetFullPath(Path.Combine(root, candidate));
            if (File.Exists(fullPath))
            {
                AddDistinct(paths, Path.GetRelativePath(root, fullPath));
                return;
            }
        }

        var match = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .FirstOrDefault(path => Path.GetFileName(path).Equals(Path.GetFileName(normalized), StringComparison.OrdinalIgnoreCase));
        if (match != null)
            AddDistinct(paths, Path.GetRelativePath(root, match));
    }

    private static void AddFilesBySuffix(string root, string suffix, List<string> paths)
    {
        foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                     .Where(path => path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                     .Order(StringComparer.OrdinalIgnoreCase))
            AddDistinct(paths, Path.GetRelativePath(root, path));
    }

    private static void AddDistinct(List<string> values, string value)
    {
        if (!string.IsNullOrWhiteSpace(value) && !values.Contains(value, StringComparer.OrdinalIgnoreCase))
            values.Add(value);
    }

    private static string StripCompoundSuffix(string value, string suffix)
        => value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) ? value[..^suffix.Length] : value;

    private static string CreateUniqueName(string preferredName, HashSet<string> usedNames)
    {
        var baseName = string.IsNullOrWhiteSpace(preferredName) ? "Expression" : preferredName;
        var candidate = baseName;
        var suffix = 2;
        while (!usedNames.Add(candidate))
            candidate = $"{baseName}_{suffix++}";
        return candidate;
    }

    private static string ToManifestPath(string path) => path.Replace('\\', '/');

    private static string ComputeHash(
        string root,
        IEnumerable<string> files,
        string entryFileName,
        byte[]? generatedEntryContents)
    {
        using var incremental = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var relative in files)
        {
            var relativeBytes = System.Text.Encoding.UTF8.GetBytes(relative.Replace('\\', '/').ToLowerInvariant());
            incremental.AppendData(relativeBytes);
            incremental.AppendData(generatedEntryContents != null
                                   && relative.Equals(entryFileName, StringComparison.OrdinalIgnoreCase)
                ? generatedEntryContents
                : File.ReadAllBytes(ResolveContainedExistingFile(root, relative)));
        }
        return Convert.ToHexString(incremental.GetHashAndReset()).ToLowerInvariant();
    }

    private sealed record DiscoveredExpression(string Name, string RelativePath);
    private sealed record DiscoveredMotionGroup(string Name, IReadOnlyList<string> Files);
    private sealed record DiscoveredActions(
        IReadOnlyList<DiscoveredExpression> Expressions,
        IReadOnlyList<DiscoveredMotionGroup> MotionGroups);
    private sealed record VTubeStudioMetadata(
        string IdleAnimation,
        IReadOnlyList<string> ExpressionFiles,
        IReadOnlyList<string> MotionFiles);
}
