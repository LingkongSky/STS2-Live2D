using Godot;

namespace Live2D.Api;

/// <summary>Blend operation used when the composited model is drawn.</summary>
public enum Live2DBlendMode
{
    /// <summary>Standard alpha blending.</summary>
    Normal,
    /// <summary>Additive blending.</summary>
    Add,
    /// <summary>Subtractive blending.</summary>
    Subtract,
    /// <summary>Multiplicative blending.</summary>
    Multiply,
    /// <summary>Premultiplied-alpha blending.</summary>
    PremultipliedAlpha,
}

/// <summary>Built-in canvas mask shapes. Coordinates are local to the model instance.</summary>
public enum Live2DMaskType
{
    /// <summary>No canvas crop.</summary>
    None,
    /// <summary>Axis-aligned rectangle.</summary>
    Rectangle,
    /// <summary>Ellipse fitted inside the configured rectangle.</summary>
    Ellipse,
    /// <summary>Rectangle with rounded corners.</summary>
    RoundedRectangle,
}

/// <summary>
/// Color adjustments applied to the whole composited model. Neutral values leave the
/// rendered image unchanged.
/// </summary>
public sealed record Live2DFilterSettings
{
    /// <summary>Neutral filter settings.</summary>
    public static Live2DFilterSettings Default { get; } = new();

    /// <summary>Multiplicative RGBA tint; default is white.</summary>
    public Color Tint { get; init; } = Colors.White;
    /// <summary>Brightness offset from -1 through 1.</summary>
    public float Brightness { get; init; }
    /// <summary>Contrast multiplier from 0 through 4.</summary>
    public float Contrast { get; init; } = 1f;
    /// <summary>Saturation multiplier from 0 through 4.</summary>
    public float Saturation { get; init; } = 1f;
    /// <summary>Grayscale mix from 0 through 1.</summary>
    public float Grayscale { get; init; }
    /// <summary>Hue rotation in degrees.</summary>
    public float HueShiftDegrees { get; init; }
    /// <summary>Color inversion mix from 0 through 1.</summary>
    public float Invert { get; init; }
    /// <summary>Gamma correction from 0.01 through 10.</summary>
    public float Gamma { get; init; } = 1f;

    internal bool IsNeutral =>
        Tint.IsEqualApprox(Colors.White) &&
        Mathf.IsZeroApprox(Brightness) &&
        Mathf.IsEqualApprox(Contrast, 1f) &&
        Mathf.IsEqualApprox(Saturation, 1f) &&
        Mathf.IsZeroApprox(Grayscale) &&
        Mathf.IsZeroApprox(HueShiftDegrees) &&
        Mathf.IsZeroApprox(Invert) &&
        Mathf.IsEqualApprox(Gamma, 1f);

    internal void Validate()
    {
        EnsureFinite(Tint.R, nameof(Tint));
        EnsureFinite(Tint.G, nameof(Tint));
        EnsureFinite(Tint.B, nameof(Tint));
        EnsureFinite(Tint.A, nameof(Tint));
        EnsureFinite(Brightness, nameof(Brightness));
        EnsureFinite(Contrast, nameof(Contrast));
        EnsureFinite(Saturation, nameof(Saturation));
        EnsureFinite(Grayscale, nameof(Grayscale));
        EnsureFinite(HueShiftDegrees, nameof(HueShiftDegrees));
        EnsureFinite(Invert, nameof(Invert));
        EnsureFinite(Gamma, nameof(Gamma));

        EnsureRange(Brightness, -1f, 1f, nameof(Brightness));
        EnsureRange(Contrast, 0f, 4f, nameof(Contrast));
        EnsureRange(Saturation, 0f, 4f, nameof(Saturation));
        EnsureRange(Grayscale, 0f, 1f, nameof(Grayscale));
        EnsureRange(Invert, 0f, 1f, nameof(Invert));
        EnsureRange(Gamma, 0.01f, 10f, nameof(Gamma));
    }

    private static void EnsureFinite(float value, string parameterName)
    {
        if (!float.IsFinite(value))
            throw new ArgumentOutOfRangeException(parameterName, "Value must be finite.");
    }

    private static void EnsureRange(float value, float minimum, float maximum, string parameterName)
    {
        if (value < minimum || value > maximum)
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"Value must be between {minimum} and {maximum}.");
    }
}

/// <summary>
/// Canvas crop/mask applied in model-local coordinates. Rounded and elliptical masks
/// approximate their edges with <see cref="SegmentsPerCorner"/> line segments.
/// </summary>
public sealed record Live2DMaskSettings
{
    /// <summary>Disabled canvas crop settings.</summary>
    public static Live2DMaskSettings None { get; } = new();

    /// <summary>Crop shape.</summary>
    public Live2DMaskType Type { get; init; }
    /// <summary>Crop bounds in model-local coordinates.</summary>
    public Rect2 Rect { get; init; } = new(-500f, -500f, 1000f, 1000f);
    /// <summary>Rounded-rectangle corner radius; must be non-negative.</summary>
    public float CornerRadius { get; init; } = 32f;
    /// <summary>Curve segments per corner, from 2 through 64.</summary>
    public int SegmentsPerCorner { get; init; } = 12;

    internal void Validate()
    {
        if (!Enum.IsDefined(Type))
            throw new ArgumentOutOfRangeException(nameof(Type));
        EnsureFinite(Rect.Position.X, nameof(Rect));
        EnsureFinite(Rect.Position.Y, nameof(Rect));
        EnsureFinite(Rect.Size.X, nameof(Rect));
        EnsureFinite(Rect.Size.Y, nameof(Rect));
        EnsureFinite(CornerRadius, nameof(CornerRadius));

        if (Type != Live2DMaskType.None && (Rect.Size.X <= 0f || Rect.Size.Y <= 0f))
            throw new ArgumentOutOfRangeException(nameof(Rect), "Mask width and height must be positive.");
        if (CornerRadius < 0f)
            throw new ArgumentOutOfRangeException(nameof(CornerRadius), "Corner radius cannot be negative.");
        if (SegmentsPerCorner is < 2 or > 64)
            throw new ArgumentOutOfRangeException(
                nameof(SegmentsPerCorner),
                "Segments per corner must be between 2 and 64.");
    }

    private static void EnsureFinite(float value, string parameterName)
    {
        if (!float.IsFinite(value))
            throw new ArgumentOutOfRangeException(parameterName, "Value must be finite.");
    }
}
