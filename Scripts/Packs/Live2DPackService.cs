using Live2D.Scripts.Configuration;
using Live2D.Scripts.Models;
using Live2D.Scripts.Runtime;
using STS2RitsuLib;

namespace Live2D.Scripts.Packs;

public sealed record Live2DPackImportSummary(int ImportedModels, int SkippedDuplicates);

public static class Live2DPackService
{
    public static void ExportAll(string destinationPath)
    {
        Live2DPackArchive.Write(
            EnsurePackageExtension(destinationPath),
            Live2DConfigStore.Get(),
            Live2DModelRepository.ModelsDirectory,
            includeGlobalConfig: false);
    }

    public static void ExportModel(string destinationPath, string modelId)
    {
        var current = Live2DConfigStore.Get();
        var model = current.Models.FirstOrDefault(value => value.Id == modelId)
                    ?? throw new InvalidOperationException($"Model configuration does not exist: {modelId}");
        var packageSettings = new Live2DSettings
        {
            SchemaVersion = current.SchemaVersion,
            Global = current.Global,
            Models = [model],
        };
        Live2DPackArchive.Write(
            EnsurePackageExtension(destinationPath),
            packageSettings,
            Live2DModelRepository.ModelsDirectory,
            includeGlobalConfig: false,
            model.DisplayName);
    }

    public static void ExportGlobal(string destinationPath)
    {
        var current = Live2DConfigStore.Get();
        var packageSettings = new Live2DSettings
        {
            SchemaVersion = current.SchemaVersion,
            Global = current.Global,
            // 全局配置包不携带模型，避免导入时意外覆盖或复制模型资源。
            Models = [],
        };
        Live2DPackArchive.Write(
            EnsurePackageExtension(destinationPath),
            packageSettings,
            Live2DModelRepository.ModelsDirectory,
            includeGlobalConfig: true,
            packageName: "Live2D Global Configuration");
    }

    public static void ImportGlobal(string packagePath)
    {
        var staging = CreateStagingDirectory("global-import");
        try
        {
            var package = Live2DPackArchive.ReadToStaging(packagePath, staging);
            if (package.Global == null)
                throw new InvalidDataException("Package does not contain global Live2D configuration.");

            var store = RitsuLibFramework.GetDataStore(Entry.ModId);
            store.Modify<Live2DSettings>(Live2DConfigStore.SettingsKey,
                settings => settings.Global = package.Global);
            store.Save(Live2DConfigStore.SettingsKey);
            Live2DRuntimeManager.RefreshAll();
            Live2DHotkeyManager.Refresh();
        }
        finally
        {
            DeleteStagingDirectory(staging);
        }
    }

    public static Live2DPackImportSummary Import(string packagePath)
    {
        var staging = CreateStagingDirectory("pack-import");
        var imported = new List<Live2DModelConfig>();
        try
        {
            var package = Live2DPackArchive.ReadToStaging(packagePath, staging);
            var local = Live2DConfigStore.Get();
            var knownHashes = local.Models.Select(model => model.ContentHash)
                .Where(hash => !string.IsNullOrWhiteSpace(hash))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var skipped = 0;

            foreach (var manifestModel in package.Manifest.Models)
            {
                if (!string.IsNullOrWhiteSpace(manifestModel.ContentHash)
                    && knownHashes.Contains(manifestModel.ContentHash))
                {
                    skipped++;
                    continue;
                }

                var sourceConfig = package.Models.First(model =>
                    string.Equals(model.Id, manifestModel.OriginalId, StringComparison.OrdinalIgnoreCase));
                var model = Live2DModelRepository.Import(package.ExtractedEntryPaths[manifestModel.OriginalId]);
                model.DisplayName = sourceConfig.DisplayName;
                model.DisplayOrder = local.Models.Count + imported.Count;
                model.Overrides = sourceConfig.Overrides ?? new Live2DModelOverrides();
                model.ActionBindings = sourceConfig.ActionBindings ?? [];
                imported.Add(model);
                if (!string.IsNullOrWhiteSpace(model.ContentHash))
                    knownHashes.Add(model.ContentHash);
            }

            var store = RitsuLibFramework.GetDataStore(Entry.ModId);
            // 模型包只导入模型；全局配置统一由 ImportGlobal 处理。
            store.Modify<Live2DSettings>(Live2DConfigStore.SettingsKey,
                settings => settings.Models.AddRange(imported));
            store.Save(Live2DConfigStore.SettingsKey);
            Live2DRuntimeManager.RefreshAll();
            Live2DHotkeyManager.Refresh();
            return new Live2DPackImportSummary(imported.Count, skipped);
        }
        catch
        {
            foreach (var model in imported)
                Live2DModelRepository.DeleteFiles(model.Id);
            throw;
        }
        finally
        {
            DeleteStagingDirectory(staging);
        }
    }

    private static string EnsurePackageExtension(string path)
    {
        return path.EndsWith(".live2dpack", StringComparison.OrdinalIgnoreCase)
            ? path
            : path + ".live2dpack";
    }

    private static string CreateStagingDirectory(string prefix)
    {
        var path = Path.Combine(Path.GetTempPath(), "Live2D", $"{prefix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteStagingDirectory(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
    }
}
