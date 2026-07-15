using Godot;
using Live2D.Api;

namespace Live2D.Scripts.Configuration;

internal static class Live2DConfigResolver
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

    private static ResolvedRenderingConfig ResolveRendering(RenderingConfig global, RenderingOverrides? value)
    {
        value ??= new RenderingOverrides();
        var filter = value.Filter ?? global.Filter;
        var mask = value.Mask ?? global.Mask;
        var blendMode = value.BlendMode ?? global.BlendMode;
        if (!Enum.IsDefined(blendMode))
            blendMode = Live2DBlendMode.Normal;
        var maskType = Enum.IsDefined(mask.Type) ? mask.Type : Live2DMaskType.None;
        return new ResolvedRenderingConfig(
            Math.Max(0, value.MaskViewportSize ?? global.MaskViewportSize),
            blendMode,
            new Live2DFilterSettings
            {
                Tint = new Color(
                    FiniteOr(filter.TintR, 1f),
                    FiniteOr(filter.TintG, 1f),
                    FiniteOr(filter.TintB, 1f),
                    FiniteOr(filter.TintA, 1f)),
                Brightness = ClampFinite(filter.Brightness, 0f, -1f, 1f),
                Contrast = ClampFinite(filter.Contrast, 1f, 0f, 4f),
                Saturation = ClampFinite(filter.Saturation, 1f, 0f, 4f),
                Grayscale = ClampFinite(filter.Grayscale, 0f, 0f, 1f),
                HueShiftDegrees = FiniteOr(filter.HueShiftDegrees, 0f),
                Invert = ClampFinite(filter.Invert, 0f, 0f, 1f),
                Gamma = ClampFinite(filter.Gamma, 1f, 0.01f, 10f),
            },
            new Live2DMaskSettings
            {
                Type = maskType,
                Rect = new Rect2(
                    FiniteOr(mask.X, -500f),
                    FiniteOr(mask.Y, -500f),
                    PositiveFiniteOr(mask.Width, 1000f),
                    PositiveFiniteOr(mask.Height, 1000f)),
                CornerRadius = Math.Max(0f, FiniteOr(mask.CornerRadius, 32f)),
                SegmentsPerCorner = Math.Clamp(mask.SegmentsPerCorner, 2, 64),
            });
    }

    private static float FiniteOr(float value, float fallback)
        => float.IsFinite(value) ? value : fallback;

    private static float PositiveFiniteOr(float value, float fallback)
        => float.IsFinite(value) && value > 0f ? value : fallback;

    private static float ClampFinite(float value, float fallback, float minimum, float maximum)
        => Math.Clamp(FiniteOr(value, fallback), minimum, maximum);
}
