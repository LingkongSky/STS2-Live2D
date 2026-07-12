using Live2D.Scripts.Models;
using STS2RitsuLib;
using STS2RitsuLib.Utils.Persistence;

namespace Live2D.Scripts.Configuration;

public static class Live2DConfigStore
{
    public const string SettingsKey = "settings";
    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized)
            return;

        using var _ = RitsuLibFramework.BeginModDataRegistration(Entry.ModId);
        var store = RitsuLibFramework.GetDataStore(Entry.ModId);
        store.Register(
            key: SettingsKey,
            fileName: "settings.json",
            scope: SaveScope.Global,
            defaultFactory: () => new Live2DSettings(),
            autoCreateIfMissing: true);
        _initialized = true;
        Normalize();
        PruneMissingModels();
    }

    public static Live2DSettings Get() => RitsuLibFramework.GetDataStore(Entry.ModId).Get<Live2DSettings>(SettingsKey);

    public static Live2DModelConfig ImportModel(string modelJsonPath)
    {
        var model = Live2DModelRepository.Import(modelJsonPath);
        var store = RitsuLibFramework.GetDataStore(Entry.ModId);
        store.Modify<Live2DSettings>(SettingsKey, settings =>
        {
            model.DisplayOrder = settings.Models.Count;
            settings.Models.Add(model);
        });
        store.Save(SettingsKey);
        Live2D.Scripts.Runtime.Live2DRuntimeManager.RefreshAll();
        return model;
    }

    public static void SaveAndRefresh()
    {
        RitsuLibFramework.GetDataStore(Entry.ModId).Save(SettingsKey);
        Live2D.Scripts.Runtime.Live2DRuntimeManager.RefreshAll();
    }

    public static int PruneMissingModels()
    {
        var store = RitsuLibFramework.GetDataStore(Entry.ModId);
        List<(Live2DModelConfig Model, string Reason)> unavailable = [];
        store.Modify<Live2DSettings>(SettingsKey, settings =>
        {
            var reasons = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var removed = Live2DConfigMigration.RemoveUnavailableModels(settings, model =>
            {
                var available = Live2DModelRepository.IsManagedModelAvailable(model, out var reason);
                if (!available)
                    reasons[model.Id] = reason;
                return available;
            });
            unavailable = removed.Select(model =>
                (model, reasons.GetValueOrDefault(model.Id, "managed model files are unavailable"))).ToList();
        });

        if (unavailable.Count == 0)
            return 0;
        store.Save(SettingsKey);
        foreach (var (model, reason) in unavailable)
            Entry.Logger.Warn($"[{Entry.ModId}] Removed stale model configuration '{model.DisplayName}' ({model.Id}): {reason}.");
        return unavailable.Count;
    }

    private static void Normalize()
    {
        var store = RitsuLibFramework.GetDataStore(Entry.ModId);
        store.Modify<Live2DSettings>(SettingsKey, Live2DConfigMigration.NormalizeInPlace);
        store.Save(SettingsKey);
    }
}
