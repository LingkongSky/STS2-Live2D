using Godot;
using Live2D.Scripts.Configuration;

namespace Live2D.Scripts.Runtime;

internal static class Live2DLayout
{
    public static readonly Vector2 ReferenceViewportSize = new(1920f, 1080f);

    public static float GetViewportScale(Vector2 viewportSize)
    {
        if (viewportSize.X <= 0f || viewportSize.Y <= 0f)
            return 1f;
        return Math.Max(0.1f, Math.Min(
            viewportSize.X / ReferenceViewportSize.X,
            viewportSize.Y / ReferenceViewportSize.Y));
    }

    public static Vector2 ResolvePosition(
        Vector2 viewportSize,
        AnchorPreset anchorPreset,
        float referenceOffsetX,
        float referenceOffsetY)
    {
        var anchor = anchorPreset switch
        {
            AnchorPreset.TopLeft => Vector2.Zero,
            AnchorPreset.TopCenter => new Vector2(viewportSize.X * 0.5f, 0f),
            AnchorPreset.TopRight => new Vector2(viewportSize.X, 0f),
            AnchorPreset.CenterLeft => new Vector2(0f, viewportSize.Y * 0.5f),
            AnchorPreset.Center => viewportSize * 0.5f,
            AnchorPreset.CenterRight => new Vector2(viewportSize.X, viewportSize.Y * 0.5f),
            AnchorPreset.BottomLeft => new Vector2(0f, viewportSize.Y),
            AnchorPreset.BottomCenter => new Vector2(viewportSize.X * 0.5f, viewportSize.Y),
            AnchorPreset.BottomRight => viewportSize,
            _ => viewportSize,
        };
        var scale = GetViewportScale(viewportSize);
        return anchor + new Vector2(referenceOffsetX, referenceOffsetY) * scale;
    }

    public static float ResolveModelScale(float configuredScale, Vector2 viewportSize)
        => Math.Max(0.01f, configuredScale) * GetViewportScale(viewportSize);

    public static Vector2 ToReferenceDelta(Vector2 viewportDelta, Vector2 viewportSize)
        => viewportDelta / GetViewportScale(viewportSize);
}
