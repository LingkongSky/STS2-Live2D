using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Live2D.Scripts.Models;
using Live2D.Scripts.Packs;
using Live2D.Scripts.Runtime;
using STS2RitsuLib;
using STS2RitsuLib.Utils.Persistence;

namespace Live2D.Scripts.Configuration;

internal static class Live2DConfigStore
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

    internal static void UpsertExternalPack(Live2DRegisteredPackRegistry.RegisteredPack pack)
    {
        var store = RitsuLibFramework.GetDataStore(Entry.ModId);
        store.Modify<Live2DSettings>(SettingsKey, settings =>
        {
            foreach (var source in pack.Models.Values)
            {
                var externalModelId = CreateExternalModelId(pack.Key.OwnerModId, pack.Key.PackId, source.ModelKey);
                if (settings.RemovedExternalModelIds.Contains(externalModelId, StringComparer.OrdinalIgnoreCase))
                    continue;

                var model = settings.Models.FirstOrDefault(value =>
                    value.IsExternalPackModel &&
                    string.Equals(value.ExternalOwnerModId, pack.Key.OwnerModId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(value.ExternalPackId, pack.Key.PackId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(value.ExternalModelKey, source.ModelKey, StringComparison.OrdinalIgnoreCase));
                if (model == null)
                {
                    model = Clone(source.Config);
                    model.Id = externalModelId;
                    model.DisplayOrder = settings.Models.Count;
                    model.ExternalOwnerModId = pack.Key.OwnerModId;
                    model.ExternalPackId = pack.Key.PackId;
                    model.ExternalModelKey = source.ModelKey;
                    settings.Models.Add(model);
                }

                // Pack metadata and the staged asset path follow the provider. User-owned
                // layout, rendering and hotkey choices on an existing entry remain intact.
                model.SourcePath = source.AssetPath;
                model.ContentHash = source.ContentHash;
                model.AvailableActions = Clone(source.Config.AvailableActions);
                // Seed provider defaults once for entries created by an older Pack
                // that exposed actions but omitted their bindings. Clearing a binding
                // in the UI keeps its binding record, so later registrations preserve
                // the player's intentional choices.
                if (model.ActionBindings.Count == 0 && source.Config.ActionBindings.Count > 0)
                    model.ActionBindings = Clone(source.Config.ActionBindings);
            }
        });
        store.Save(SettingsKey);
        Live2DRuntimeManager.RefreshAll();
        Live2DHotkeyManager.Refresh();
    }

    internal static int RestoreRemovedExternalModels()
    {
        var store = RitsuLibFramework.GetDataStore(Entry.ModId);
        var restoredCount = 0;
        store.Modify<Live2DSettings>(SettingsKey, settings =>
        {
            restoredCount = settings.RemovedExternalModelIds.Count;
            settings.RemovedExternalModelIds.Clear();
        });
        if (restoredCount == 0)
            return 0;

        store.Save(SettingsKey);
        foreach (var pack in Live2DRegisteredPackRegistry.GetRegisteredPacksSnapshot())
            UpsertExternalPack(pack);
        return restoredCount;
    }

    public static int PruneMissingModels()
    {
        var store = RitsuLibFramework.GetDataStore(Entry.ModId);
        List<(Live2DModelConfig Model, string Reason)> unavailable = [];
        store.Modify<Live2DSettings>(SettingsKey, settings =>
        {
            var reasons = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var removed = Live2DConfigNormalizer.RemoveUnavailableModels(settings, model =>
            {
                if (model.IsExternalPackModel)
                    return true;
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
        store.Modify<Live2DSettings>(SettingsKey, Live2DConfigNormalizer.NormalizeInPlace);
        store.Save(SettingsKey);
    }

    private static string CreateExternalModelId(string ownerModId, string packId, string modelKey)
    {
        var identity = $"{ownerModId.ToUpperInvariant()}\0{packId.ToUpperInvariant()}\0{modelKey.ToUpperInvariant()}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))).ToLowerInvariant();
        return "extlib_" + hash[..24];
    }

    private static T Clone<T>(T value)
        => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))
           ?? throw new InvalidDataException("Unable to clone Live2D pack configuration.");
}
