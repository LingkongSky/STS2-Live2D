namespace Live2D.Scripts.Configuration;

internal static class Live2DConfigNormalizer
{
    public static List<Live2DModelConfig> RemoveUnavailableModels(
        Live2DSettings settings,
        Func<Live2DModelConfig, bool> isAvailable)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(isAvailable);
        var removed = settings.Models.Where(model => !isAvailable(model)).ToList();
        if (removed.Count == 0)
            return removed;
        var removedIds = removed.Select(model => model.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        settings.Models.RemoveAll(model => removedIds.Contains(model.Id));
        for (var index = 0; index < settings.Models.Count; index++)
            settings.Models[index].DisplayOrder = index;
        return removed;
    }

    public static void NormalizeInPlace(Live2DSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Global ??= new GlobalLive2DConfig();
        settings.Global.Hotkeys ??= new GlobalHotkeyConfig();
        settings.Global.MainMenu ??= SceneDisplayConfig.CreateMainMenuDefault();
        settings.Global.InGame ??= SceneDisplayConfig.CreateInGameDefault();
        settings.Global.Playback ??= new PlaybackConfig();
        settings.Global.Rendering ??= new RenderingConfig();
        settings.Global.Rendering.Filter ??= new FilterConfig();
        settings.Global.Rendering.Mask ??= new CanvasMaskConfig();
        settings.Models ??= [];
        foreach (var model in settings.Models)
        {
            model.Overrides ??= new Live2DModelOverrides();
            model.Overrides.MainMenu ??= new SceneDisplayOverrides();
            model.Overrides.InGame ??= new SceneDisplayOverrides();
            model.Overrides.Playback ??= new PlaybackOverrides();
            model.Overrides.Rendering ??= new RenderingOverrides();
            model.AvailableActions ??= [];
            model.ActionBindings ??= [];
        }
        settings.SchemaVersion = Live2DSettings.CurrentSchemaVersion;
    }
}
