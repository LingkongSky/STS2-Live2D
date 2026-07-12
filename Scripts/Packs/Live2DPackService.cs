using Live2D.Scripts.Configuration;
using Live2D.Scripts.Models;
using Live2D.Scripts.Runtime;
using STS2RitsuLib;

namespace Live2D.Scripts.Packs;

public enum GlobalConfigImportMode
{
    KeepLocal,
    ReplaceWithPackage,
}

public sealed record Live2DPackImportSummary(int ImportedModels, int SkippedDuplicates, bool ReplacedGlobalConfig);

public static class Live2DPackService
{
    public static void ExportAll(string destinationPath, bool includeGlobalConfig = true)
    {
        if (!destinationPath.EndsWith(".live2dpack", StringComparison.OrdinalIgnoreCase))
            destinationPath += ".live2dpack";
        Live2DPackArchive.Write(
            destinationPath,
            Live2DConfigStore.Get(),
            Live2DModelRepository.ModelsDirectory,
            includeGlobalConfig);
    }

    public static void ExportModel(string destinationPath, string modelId, bool includeGlobalConfig = true)
    {
        if (!destinationPath.EndsWith(".live2dpack", StringComparison.OrdinalIgnoreCase))
            destinationPath += ".live2dpack";
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
            destinationPath,
            packageSettings,
            Live2DModelRepository.ModelsDirectory,
            includeGlobalConfig,
            model.DisplayName);
    }

    public static Live2DPackImportSummary Import(
        string packagePath,
        GlobalConfigImportMode globalMode = GlobalConfigImportMode.KeepLocal)
    {
        var staging = Path.Combine(Path.GetTempPath(), "Live2D", "pack-import-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
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

            var replaceGlobal = globalMode == GlobalConfigImportMode.ReplaceWithPackage && package.Global != null;
            var store = RitsuLibFramework.GetDataStore(Entry.ModId);
            store.Modify<Live2DSettings>(Live2DConfigStore.SettingsKey, settings =>
            {
                if (replaceGlobal)
                    settings.Global = package.Global!;
                settings.Models.AddRange(imported);
            });
            store.Save(Live2DConfigStore.SettingsKey);
            Live2DRuntimeManager.RefreshAll();
            Live2DHotkeyManager.Refresh();
            return new Live2DPackImportSummary(imported.Count, skipped, replaceGlobal);
        }
        catch
        {
            foreach (var model in imported)
                Live2DModelRepository.DeleteFiles(model.Id);
            throw;
        }
        finally
        {
            if (Directory.Exists(staging))
                Directory.Delete(staging, recursive: true);
        }
    }
}
