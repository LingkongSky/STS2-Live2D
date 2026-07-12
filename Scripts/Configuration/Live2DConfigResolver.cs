namespace Live2D.Scripts.Configuration;

public static class Live2DConfigResolver
{
    public static ResolvedLive2DConfig Resolve(GlobalLive2DConfig global, Live2DModelOverrides? overrides)
    {
        ArgumentNullException.ThrowIfNull(global);
        overrides ??= new Live2DModelOverrides();

        return new ResolvedLive2DConfig(
            ResolveScene(global.MainMenu, overrides.MainMenu),
            ResolveScene(global.InGame, overrides.InGame),
            ResolvePlayback(global.Playback, overrides.Playback),
            ResolveRendering(global.Rendering, overrides.Rendering));
    }

    public static SceneDisplayConfig ForScene(ResolvedLive2DConfig config, Live2DSceneKind scene) => scene switch
    {
        Live2DSceneKind.MainMenu => config.MainMenu,
        Live2DSceneKind.InGame => config.InGame,
        _ => throw new ArgumentOutOfRangeException(nameof(scene), scene, null),
    };

    private static SceneDisplayConfig ResolveScene(SceneDisplayConfig global, SceneDisplayOverrides? value)
    {
        value ??= new SceneDisplayOverrides();
        return new SceneDisplayConfig
        {
            Visible = value.Visible ?? global.Visible,
            Anchor = value.Anchor ?? global.Anchor,
            OffsetX = value.OffsetX ?? global.OffsetX,
            OffsetY = value.OffsetY ?? global.OffsetY,
            Scale = value.Scale ?? global.Scale,
            RotationDegrees = value.RotationDegrees ?? global.RotationDegrees,
            Opacity = value.Opacity ?? global.Opacity,
            Layer = value.Layer ?? global.Layer,
            MouseInteraction = value.MouseInteraction ?? global.MouseInteraction,
        };
    }

    private static PlaybackConfig ResolvePlayback(PlaybackConfig global, PlaybackOverrides? value)
    {
        value ??= new PlaybackOverrides();
        return new PlaybackConfig
        {
            Speed = value.Speed ?? global.Speed,
            EnablePhysics = value.EnablePhysics ?? global.EnablePhysics,
            EnablePose = value.EnablePose ?? global.EnablePose,
            AutoPlayIdle = value.AutoPlayIdle ?? global.AutoPlayIdle,
            ActionCooldownSeconds = value.ActionCooldownSeconds ?? global.ActionCooldownSeconds,
        };
    }

    private static RenderingConfig ResolveRendering(RenderingConfig global, RenderingOverrides? value)
    {
        value ??= new RenderingOverrides();
        return new RenderingConfig
        {
            MaskViewportSize = value.MaskViewportSize ?? global.MaskViewportSize,
        };
    }
}
