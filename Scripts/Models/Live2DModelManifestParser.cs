using System.Security.Cryptography;
using System.Text.Json;
using Live2D.Scripts.Configuration;

namespace Live2D.Scripts.Models;

public sealed record ParsedLive2DModel(
    string EntryPath,
    IReadOnlyList<string> RelativeFiles,
    IReadOnlyList<Live2DActionDescriptor> Actions,
    string ContentHash);

public static class Live2DModelManifestParser
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
        using var document = JsonDocument.Parse(File.ReadAllBytes(entryPath), new JsonDocumentOptions
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
                    AddRelative(GetString(motion, "File"), files);
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

        var normalizedFiles = files.Select(NormalizeRelativePath).Order(StringComparer.OrdinalIgnoreCase).ToArray();
        foreach (var relativeFile in normalizedFiles)
            ResolveContainedExistingFile(root, relativeFile);

        return new ParsedLive2DModel(entryPath, normalizedFiles, actions, ComputeHash(root, normalizedFiles));
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

    private static string GetString(JsonElement owner, string propertyName)
        => owner.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
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

    private static string ComputeHash(string root, IEnumerable<string> files)
    {
        using var incremental = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var relative in files)
        {
            var relativeBytes = System.Text.Encoding.UTF8.GetBytes(relative.Replace('\\', '/').ToLowerInvariant());
            incremental.AppendData(relativeBytes);
            incremental.AppendData(File.ReadAllBytes(ResolveContainedExistingFile(root, relative)));
        }
        return Convert.ToHexString(incremental.GetHashAndReset()).ToLowerInvariant();
    }
}
