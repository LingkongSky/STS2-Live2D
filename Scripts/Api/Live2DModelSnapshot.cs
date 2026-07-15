using Godot;

namespace Live2D.Api;

/// <summary>Current runtime state of a rendered Live2D model instance.</summary>
/// <param name="ModelId">Runtime model ID.</param>
/// <param name="Scene">Host scene.</param>
/// <param name="IsAvailable">Whether a live scene instance is bound.</param>
/// <param name="Position">Model-root position.</param>
/// <param name="Scale">Model-root scale.</param>
/// <param name="RotationDegrees">Clockwise model-root rotation.</param>
/// <param name="Opacity">Model opacity.</param>
/// <param name="Visible">Model-root visibility.</param>
/// <param name="Layer">Canvas Z index.</param>
/// <param name="PlaybackSpeed">Cubism playback speed.</param>
/// <param name="PhysicsEnabled">Whether Cubism physics is enabled.</param>
/// <param name="PoseEnabled">Whether Cubism pose updates are enabled.</param>
/// <param name="MaskViewportSize">Cubism drawable-mask viewport size.</param>
/// <param name="BlendMode">Whole-model blend operation.</param>
/// <param name="Filter">Whole-model color filter.</param>
/// <param name="Mask">Model-local canvas crop.</param>
/// <param name="Playback">Currently commanded motion/expression state.</param>
public sealed record Live2DModelSnapshot(
    string ModelId,
    Live2DScene Scene,
    bool IsAvailable,
    Vector2 Position,
    Vector2 Scale,
    float RotationDegrees,
    float Opacity,
    bool Visible,
    int Layer,
    float PlaybackSpeed,
    bool PhysicsEnabled,
    bool PoseEnabled,
    int MaskViewportSize,
    Live2DBlendMode BlendMode,
    Live2DFilterSettings Filter,
    Live2DMaskSettings Mask,
    Live2DPlaybackSnapshot Playback);

/// <summary>Currently commanded motion and expression state for a model instance.</summary>
/// <param name="MotionGroup">Active motion group, or null.</param>
/// <param name="MotionIndex">Active motion index, or -1.</param>
/// <param name="MotionLooping">Whether the active motion loops.</param>
/// <param name="ExpressionId">Active expression ID, or null.</param>
public sealed record Live2DPlaybackSnapshot(
    string? MotionGroup,
    int MotionIndex,
    bool MotionLooping,
    string? ExpressionId)
{
    /// <summary>Empty playback state.</summary>
    public static Live2DPlaybackSnapshot Empty { get; } = new(null, -1, false, null);
    /// <summary>Whether a motion is currently commanded.</summary>
    public bool HasMotion => !string.IsNullOrWhiteSpace(MotionGroup) && MotionIndex >= 0;
    /// <summary>Whether an expression is currently commanded.</summary>
    public bool HasExpression => !string.IsNullOrWhiteSpace(ExpressionId);
}

/// <summary>Metadata and current value for a Cubism parameter.</summary>
/// <param name="Id">Parameter ID.</param>
/// <param name="Value">Current value.</param>
/// <param name="DefaultValue">Model-declared default value.</param>
/// <param name="MinimumValue">Model-declared minimum value.</param>
/// <param name="MaximumValue">Model-declared maximum value.</param>
public sealed record Live2DParameterInfo(
    string Id,
    float Value,
    float DefaultValue,
    float MinimumValue,
    float MaximumValue);

/// <summary>Metadata and current value for a Cubism part opacity controller.</summary>
/// <param name="Id">Part ID.</param>
/// <param name="Opacity">Current opacity.</param>
public sealed record Live2DPartInfo(string Id, float Opacity);
