using Live2D.Api;

namespace Live2D.Scripts.Configuration;

internal enum Live2DSceneKind
{
    MainMenu,
    InGame,
}

internal enum AnchorPreset
{
    TopLeft,
    TopCenter,
    TopRight,
    CenterLeft,
    Center,
    CenterRight,
    BottomLeft,
    BottomCenter,
    BottomRight,
}

internal enum Live2DActionKind
{
    Motion,
    Expression,
}

internal sealed class Live2DSettings
{
    public const int CurrentSchemaVersion = 6;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public GlobalLive2DConfig Global { get; set; } = new();
    public List<Live2DModelConfig> Models { get; set; } = [];
}

internal sealed class GlobalLive2DConfig
{
    public GlobalHotkeyConfig Hotkeys { get; set; } = new();
    public SceneDisplayConfig MainMenu { get; set; } = SceneDisplayConfig.CreateMainMenuDefault();
    public SceneDisplayConfig InGame { get; set; } = SceneDisplayConfig.CreateInGameDefault();
    public PlaybackConfig Playback { get; set; } = new();
    public RenderingConfig Rendering { get; set; } = new();
}

internal sealed class GlobalHotkeyConfig
{
    public string ToggleVisibility { get; set; } = "";
}

internal sealed class SceneDisplayConfig
{
    public bool Visible { get; set; } = true;
    public AnchorPreset Anchor { get; set; } = AnchorPreset.BottomRight;
    public float OffsetX { get; set; } = -260f;
    public float OffsetY { get; set; } = -20f;
    public float Scale { get; set; } = 0.35f;
    public float RotationDegrees { get; set; }
    public float Opacity { get; set; } = 1f;
    public int Layer { get; set; }
    public bool MouseInteraction { get; set; } = true;

    public static SceneDisplayConfig CreateMainMenuDefault() => new()
    {
        // Equivalent to MiSide.tscn at 1920x1080:
        // root scale 0.5 and model position (2841.087, 3393.871).
        // Expressed relative to the bottom-right anchor for responsive layouts.
        Anchor = AnchorPreset.BottomRight,
        OffsetX = -499.4565f,
        OffsetY = 616.9355f,
        Scale = 0.5f,
    };

    public static SceneDisplayConfig CreateInGameDefault() => new()
    {
        Visible = true,
        Scale = 0.3f,
        Layer = 20,
    };
}

internal sealed class SceneDisplayOverrides
{
    public bool? Visible { get; set; }
    public AnchorPreset? Anchor { get; set; }
    public float? OffsetX { get; set; }
    public float? OffsetY { get; set; }
    public float? Scale { get; set; }
    public float? RotationDegrees { get; set; }
    public float? Opacity { get; set; }
    public int? Layer { get; set; }
    public bool? MouseInteraction { get; set; }
}

internal sealed class PlaybackConfig
{
    public float Speed { get; set; } = 1f;
    public bool EnablePhysics { get; set; } = true;
    public bool EnablePose { get; set; } = true;
    public bool AutoPlayIdle { get; set; } = true;
    public float ActionCooldownSeconds { get; set; } = 0.1f;
}

internal sealed class PlaybackOverrides
{
    public float? Speed { get; set; }
    public bool? EnablePhysics { get; set; }
    public bool? EnablePose { get; set; }
    public bool? AutoPlayIdle { get; set; }
    public float? ActionCooldownSeconds { get; set; }
}

internal sealed class RenderingConfig
{
    public int MaskViewportSize { get; set; }
    public Live2DBlendMode BlendMode { get; set; }
    public FilterConfig Filter { get; set; } = new();
    public CanvasMaskConfig Mask { get; set; } = new();
}

internal sealed class RenderingOverrides
{
    public int? MaskViewportSize { get; set; }
    public Live2DBlendMode? BlendMode { get; set; }
    public FilterConfig? Filter { get; set; }
    public CanvasMaskConfig? Mask { get; set; }
}

internal sealed class FilterConfig
{
    public float TintR { get; set; } = 1f;
    public float TintG { get; set; } = 1f;
    public float TintB { get; set; } = 1f;
    public float TintA { get; set; } = 1f;
    public float Brightness { get; set; }
    public float Contrast { get; set; } = 1f;
    public float Saturation { get; set; } = 1f;
    public float Grayscale { get; set; }
    public float HueShiftDegrees { get; set; }
    public float Invert { get; set; }
    public float Gamma { get; set; } = 1f;
}

internal sealed class CanvasMaskConfig
{
    public Live2DMaskType Type { get; set; }
    public float X { get; set; } = -500f;
    public float Y { get; set; } = -500f;
    public float Width { get; set; } = 1000f;
    public float Height { get; set; } = 1000f;
    public float CornerRadius { get; set; } = 32f;
    public int SegmentsPerCorner { get; set; } = 12;
}

internal sealed class Live2DModelConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string DisplayName { get; set; } = "Live2D Model";
    public string ModelRelativePath { get; set; } = "";
    public string SourcePath { get; set; } = "";
    public string ContentHash { get; set; } = "";
    public DateTimeOffset ImportedAt { get; set; } = DateTimeOffset.UtcNow;
    public int DisplayOrder { get; set; }
    public Live2DModelOverrides Overrides { get; set; } = new();
    public List<Live2DActionDescriptor> AvailableActions { get; set; } = [];
    public List<ActionBindingConfig> ActionBindings { get; set; } = [];
}

internal sealed class Live2DModelOverrides
{
    public SceneDisplayOverrides MainMenu { get; set; } = new();
    public SceneDisplayOverrides InGame { get; set; } = new();
    public PlaybackOverrides Playback { get; set; } = new();
    public RenderingOverrides Rendering { get; set; } = new();
}

internal sealed class Live2DActionDescriptor
{
    public Live2DActionKind Kind { get; set; }
    public string DisplayName { get; set; } = "";
    public string MotionGroup { get; set; } = "";
    public int MotionIndex { get; set; } = -1;
    public string ExpressionId { get; set; } = "";
}

internal sealed class ActionBindingConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public Live2DActionKind Kind { get; set; }
    public string MotionGroup { get; set; } = "";
    public int MotionIndex { get; set; } = -1;
    public string ExpressionId { get; set; } = "";
    public string KeyBinding { get; set; } = "";
    public bool MainMenu { get; set; } = true;
    public bool InGame { get; set; } = true;
    public bool Loop { get; set; }
}

internal sealed record ResolvedLive2DConfig(
    SceneDisplayConfig MainMenu,
    SceneDisplayConfig InGame,
    PlaybackConfig Playback,
    ResolvedRenderingConfig Rendering);

internal sealed record ResolvedRenderingConfig(
    int MaskViewportSize,
    Live2DBlendMode BlendMode,
    Live2DFilterSettings Filter,
    Live2DMaskSettings Mask);
