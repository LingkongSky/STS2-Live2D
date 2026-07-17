using Godot;

namespace Live2D.Api;

/// <summary>
/// Describes a partial, transient update for one rendered Live2D model instance.
/// Unspecified properties retain their current value.
/// <para>中文：只填写需要修改的字段；未填写字段沿用当前值。</para>
/// </summary>
public sealed class Live2DModelUpdate
{
    /// <summary>Model-root position in canvas coordinates.</summary>
    public Vector2? Position { get; set; }
    /// <summary>Model-root scale. Components must be finite and non-zero.</summary>
    public Vector2? Scale { get; set; }
    /// <summary>Clockwise model-root rotation in degrees.</summary>
    public float? RotationDegrees { get; set; }
    /// <summary>Model opacity, clamped to the range 0 through 1.</summary>
    public float? Opacity { get; set; }
    /// <summary>Model-root visibility.</summary>
    public bool? Visible { get; set; }
    /// <summary>Godot canvas Z index.</summary>
    public int? Layer { get; set; }
    /// <summary>Cubism playback speed. Negative values are treated as zero.</summary>
    public float? PlaybackSpeed { get; set; }
    /// <summary>Whether Cubism physics evaluation is enabled.</summary>
    public bool? PhysicsEnabled { get; set; }
    /// <summary>Whether Cubism pose updates are enabled.</summary>
    public bool? PoseEnabled { get; set; }
    /// <summary>Cubism drawable-mask viewport size. Negative values are treated as zero.</summary>
    public int? MaskViewportSize { get; set; }
    /// <summary>Whole-model canvas blend operation.</summary>
    public Live2DBlendMode? BlendMode { get; set; }
    /// <summary>Whole-model color filter.</summary>
    public Live2DFilterSettings? Filter { get; set; }
    /// <summary>Model-local canvas crop.</summary>
    public Live2DMaskSettings? Mask { get; set; }

    internal bool IsEmpty =>
        Position is null &&
        Scale is null &&
        RotationDegrees is null &&
        Opacity is null &&
        Visible is null &&
        Layer is null &&
        PlaybackSpeed is null &&
        PhysicsEnabled is null &&
        PoseEnabled is null &&
        MaskViewportSize is null &&
        BlendMode is null &&
        Filter is null &&
        Mask is null;

    internal void Validate()
    {
        if (Position is { } position)
        {
            EnsureFinite(position.X, nameof(Position));
            EnsureFinite(position.Y, nameof(Position));
        }
        if (Scale is { } scale)
        {
            EnsureFinite(scale.X, nameof(Scale));
            EnsureFinite(scale.Y, nameof(Scale));
            if (Mathf.IsZeroApprox(scale.X) || Mathf.IsZeroApprox(scale.Y))
                throw new ArgumentOutOfRangeException(nameof(Scale), "Scale components cannot be zero.");
        }
        if (RotationDegrees is { } rotation)
            EnsureFinite(rotation, nameof(RotationDegrees));
        if (Opacity is { } opacity)
            EnsureFinite(opacity, nameof(Opacity));
        if (PlaybackSpeed is { } speed)
            EnsureFinite(speed, nameof(PlaybackSpeed));
        if (BlendMode is { } blendMode && !Enum.IsDefined(blendMode))
            throw new ArgumentOutOfRangeException(nameof(BlendMode));
        Filter?.Validate();
        Mask?.Validate();
    }

    internal void MergeFrom(Live2DModelUpdate update)
    {
        Position = update.Position ?? Position;
        Scale = update.Scale ?? Scale;
        RotationDegrees = update.RotationDegrees ?? RotationDegrees;
        Opacity = update.Opacity ?? Opacity;
        Visible = update.Visible ?? Visible;
        Layer = update.Layer ?? Layer;
        PlaybackSpeed = update.PlaybackSpeed ?? PlaybackSpeed;
        PhysicsEnabled = update.PhysicsEnabled ?? PhysicsEnabled;
        PoseEnabled = update.PoseEnabled ?? PoseEnabled;
        MaskViewportSize = update.MaskViewportSize ?? MaskViewportSize;
        BlendMode = update.BlendMode ?? BlendMode;
        Filter = update.Filter ?? Filter;
        Mask = update.Mask ?? Mask;
    }

    internal Live2DModelUpdate Copy() => new()
    {
        Position = Position,
        Scale = Scale,
        RotationDegrees = RotationDegrees,
        Opacity = Opacity,
        Visible = Visible,
        Layer = Layer,
        PlaybackSpeed = PlaybackSpeed,
        PhysicsEnabled = PhysicsEnabled,
        PoseEnabled = PoseEnabled,
        MaskViewportSize = MaskViewportSize,
        BlendMode = BlendMode,
        Filter = Filter,
        Mask = Mask,
    };

    private static void EnsureFinite(float value, string parameterName)
    {
        if (!float.IsFinite(value))
            throw new ArgumentOutOfRangeException(parameterName, "Value must be finite.");
    }
}
