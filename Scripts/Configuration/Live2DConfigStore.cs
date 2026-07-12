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

    private static void Normalize()
    {
        var store = RitsuLibFramework.GetDataStore(Entry.ModId);
        store.Modify<Live2DSettings>(SettingsKey, Live2DConfigMigration.NormalizeInPlace);
        store.Save(SettingsKey);
    }
}
