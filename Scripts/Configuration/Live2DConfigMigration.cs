namespace Live2D.Scripts.Configuration;

public static class Live2DConfigMigration
{
    public static void NormalizeInPlace(Live2DSettings settings)
    {
        var storedSchemaVersion = settings.SchemaVersion;
        settings.Global ??= new GlobalLive2DConfig();
        settings.Global.Hotkeys ??= new GlobalHotkeyConfig();
        settings.Global.MainMenu ??= SceneDisplayConfig.CreateMainMenuDefault();
        if (storedSchemaVersion < 5)
            settings.Global.InGame = settings.Global.Combat ?? settings.Global.Map ?? SceneDisplayConfig.CreateInGameDefault();
        settings.Global.InGame ??= SceneDisplayConfig.CreateInGameDefault();
        settings.Global.Map = null;
        settings.Global.Combat = null;
        settings.Global.Playback ??= new PlaybackConfig();
        settings.Global.Rendering ??= new RenderingConfig();
        if (storedSchemaVersion < 3 && IsLegacyMainMenuDefault(settings.Global.MainMenu))
            settings.Global.MainMenu = SceneDisplayConfig.CreateMainMenuDefault();
        settings.Models ??= [];
        foreach (var model in settings.Models)
        {
            model.Overrides ??= new Live2DModelOverrides();
            if (storedSchemaVersion < 5)
                model.Overrides.InGame = model.Overrides.Combat ?? model.Overrides.Map ?? new SceneDisplayOverrides();
            model.Overrides.InGame ??= new SceneDisplayOverrides();
            model.Overrides.Map = null;
            model.Overrides.Combat = null;
            model.AvailableActions ??= [];
            model.ActionBindings ??= [];
            foreach (var binding in model.ActionBindings)
            {
                if (storedSchemaVersion < 5)
                    binding.InGame = (binding.Map ?? false) || (binding.Combat ?? false);
                binding.Map = null;
                binding.Combat = null;
            }
        }
        settings.SchemaVersion = Live2DSettings.CurrentSchemaVersion;
    }

    private static bool IsLegacyMainMenuDefault(SceneDisplayConfig value) =>
        value.Visible &&
        value.Anchor == AnchorPreset.BottomRight &&
        Math.Abs(value.OffsetX - (-260f)) < 0.001f &&
        Math.Abs(value.OffsetY - (-20f)) < 0.001f &&
        Math.Abs(value.Scale - 0.35f) < 0.001f &&
        Math.Abs(value.RotationDegrees) < 0.001f &&
        Math.Abs(value.Opacity - 1f) < 0.001f &&
        value.Layer == 0 &&
        value.MouseInteraction;
}
