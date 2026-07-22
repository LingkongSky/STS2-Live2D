using Godot;
using Live2D.Api;

namespace Live2D.Scripts.Runtime;

internal static class Live2DRenderPipeline
{
    private const string ShaderBody = """
        shader_type canvas_item;
        render_mode unshaded, __BLEND_MODE__;

        uniform vec4 tint_color : source_color = vec4(1.0);
        uniform float brightness = 0.0;
        uniform float contrast = 1.0;
        uniform float saturation = 1.0;
        uniform float grayscale = 0.0;
        uniform float hue_shift = 0.0;
        uniform float invert_amount = 0.0;
        uniform float gamma_value = 1.0;
        uniform int canvas_mask_type = 0;
        uniform vec4 canvas_mask_rect = vec4(-500.0, -500.0, 1000.0, 1000.0);
        uniform float canvas_mask_corner_radius = 32.0;
        uniform float composite_render_scale = 1.0;
        uniform vec2 composite_canvas_center = vec2(0.0);

        varying vec2 canvas_mask_local_position;
        varying vec4 model_modulate;

        void vertex() {
            canvas_mask_local_position = VERTEX / composite_render_scale + composite_canvas_center;
            model_modulate = COLOR;
        }

        vec3 rotate_hue(vec3 color, float angle) {
            vec3 axis = normalize(vec3(1.0));
            float cosine = cos(angle);
            float sine = sin(angle);
            return color * cosine
                + cross(axis, color) * sine
                + axis * dot(axis, color) * (1.0 - cosine);
        }

        void fragment() {
            if (canvas_mask_type != 0) {
                vec2 rect_position = canvas_mask_rect.xy;
                vec2 rect_size = canvas_mask_rect.zw;
                vec2 relative = canvas_mask_local_position - rect_position;
                bool outside_rect = relative.x < 0.0 || relative.y < 0.0
                    || relative.x > rect_size.x || relative.y > rect_size.y;
                if (outside_rect) {
                    discard;
                }
                if (canvas_mask_type == 2) {
                    vec2 normalized = (relative - rect_size * 0.5) / (rect_size * 0.5);
                    if (dot(normalized, normalized) > 1.0) {
                        discard;
                    }
                } else if (canvas_mask_type == 3) {
                    vec2 half_size = rect_size * 0.5;
                    float radius = min(canvas_mask_corner_radius, min(half_size.x, half_size.y));
                    vec2 rounded = abs(relative - half_size) - (half_size - vec2(radius));
                    float distance_to_edge = length(max(rounded, vec2(0.0)))
                        + min(max(rounded.x, rounded.y), 0.0) - radius;
                    if (distance_to_edge > 0.0) {
                        discard;
                    }
                }
            }

            vec4 c = texture(TEXTURE, UV) * model_modulate;
            if (c.a > 0.0001) {
                c.rgb /= c.a;
            }

            c.rgb *= tint_color.rgb;
            c.rgb = rotate_hue(c.rgb, hue_shift);
            float luminance = dot(c.rgb, vec3(0.2126, 0.7152, 0.0722));
            c.rgb = mix(vec3(luminance), c.rgb, saturation);
            c.rgb = (c.rgb - vec3(0.5)) * contrast + vec3(0.5 + brightness);
            luminance = dot(c.rgb, vec3(0.2126, 0.7152, 0.0722));
            c.rgb = mix(c.rgb, vec3(luminance), grayscale);
            c.rgb = mix(c.rgb, vec3(1.0) - c.rgb, invert_amount);
            c.rgb = pow(max(c.rgb, vec3(0.0)), vec3(1.0 / gamma_value));
            c.a *= tint_color.a;
            __PREMULTIPLY__
            COLOR = c;
        }
        """;

    private static readonly Dictionary<Live2DBlendMode, Shader> Shaders = [];

    public static ShaderMaterial CreateMaterial(Live2DBlendMode blendMode)
        => new() { Shader = GetShader(blendMode) };

    public static bool RequiresCompositeRendering(
        Live2DBlendMode blendMode,
        Live2DFilterSettings filter,
        Live2DMaskSettings mask)
        => blendMode != Live2DBlendMode.Normal ||
           !filter.IsNeutral ||
           mask.Type != Live2DMaskType.None;

    public static void UpdateMaterial(
        ShaderMaterial material,
        Live2DBlendMode blendMode,
        Live2DFilterSettings filter)
    {
        material.Shader = GetShader(blendMode);
        material.SetShaderParameter("tint_color", filter.Tint);
        material.SetShaderParameter("brightness", filter.Brightness);
        material.SetShaderParameter("contrast", filter.Contrast);
        material.SetShaderParameter("saturation", filter.Saturation);
        material.SetShaderParameter("grayscale", filter.Grayscale);
        material.SetShaderParameter("hue_shift", Mathf.DegToRad(filter.HueShiftDegrees));
        material.SetShaderParameter("invert_amount", filter.Invert);
        material.SetShaderParameter("gamma_value", filter.Gamma);
    }

    public static void UpdateMask(ShaderMaterial material, Live2DMaskSettings mask)
    {
        material.SetShaderParameter("canvas_mask_type", (int)mask.Type);
        material.SetShaderParameter("canvas_mask_rect", new Vector4(
            mask.Rect.Position.X,
            mask.Rect.Position.Y,
            mask.Rect.Size.X,
            mask.Rect.Size.Y));
        material.SetShaderParameter("canvas_mask_corner_radius", mask.CornerRadius);
    }

    public static void UpdateCompositeGeometry(
        ShaderMaterial material,
        float renderScale,
        Vector2 canvasCenter)
    {
        material.SetShaderParameter("composite_render_scale", renderScale);
        material.SetShaderParameter("composite_canvas_center", canvasCenter);
    }

    public static Vector2[] BuildMaskPolygon(Live2DMaskSettings mask) => mask.Type switch
    {
        Live2DMaskType.None => [],
        Live2DMaskType.Rectangle => BuildRectangle(mask.Rect),
        Live2DMaskType.Ellipse => BuildEllipse(mask.Rect, mask.SegmentsPerCorner * 4),
        Live2DMaskType.RoundedRectangle => BuildRoundedRectangle(
            mask.Rect,
            mask.CornerRadius,
            mask.SegmentsPerCorner),
        _ => throw new ArgumentOutOfRangeException(nameof(mask), mask.Type, null),
    };

    private static Shader GetShader(Live2DBlendMode blendMode)
    {
        if (Shaders.TryGetValue(blendMode, out var shader) && GodotObject.IsInstanceValid(shader))
            return shader;

        var (renderMode, premultiply) = blendMode switch
        {
            Live2DBlendMode.Normal => ("blend_mix", ""),
            Live2DBlendMode.Add => ("blend_add", ""),
            Live2DBlendMode.Subtract => ("blend_sub", ""),
            Live2DBlendMode.Multiply => ("blend_mul", ""),
            Live2DBlendMode.PremultipliedAlpha => ("blend_premul_alpha", "c.rgb *= c.a;"),
            _ => throw new ArgumentOutOfRangeException(nameof(blendMode), blendMode, null),
        };
        shader = new Shader
        {
            Code = ShaderBody
                .Replace("__BLEND_MODE__", renderMode, StringComparison.Ordinal)
                .Replace("__PREMULTIPLY__", premultiply, StringComparison.Ordinal),
        };
        Shaders[blendMode] = shader;
        return shader;
    }

    private static Vector2[] BuildRectangle(Rect2 rect)
    {
        var end = rect.End;
        return
        [
            rect.Position,
            new Vector2(end.X, rect.Position.Y),
            end,
            new Vector2(rect.Position.X, end.Y),
        ];
    }

    private static Vector2[] BuildEllipse(Rect2 rect, int segments)
    {
        var points = new Vector2[segments];
        var center = rect.GetCenter();
        var radius = rect.Size * 0.5f;
        for (var index = 0; index < segments; index++)
        {
            var angle = Mathf.Tau * index / segments;
            points[index] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }
        return points;
    }

    private static Vector2[] BuildRoundedRectangle(Rect2 rect, float requestedRadius, int segments)
    {
        var radius = Math.Min(requestedRadius, Math.Min(rect.Size.X, rect.Size.Y) * 0.5f);
        if (Mathf.IsZeroApprox(radius))
            return BuildRectangle(rect);

        var points = new Vector2[segments * 4];
        var end = rect.End;
        var centers = new[]
        {
            new Vector2(rect.Position.X + radius, rect.Position.Y + radius),
            new Vector2(end.X - radius, rect.Position.Y + radius),
            new Vector2(end.X - radius, end.Y - radius),
            new Vector2(rect.Position.X + radius, end.Y - radius),
        };
        var startAngles = new[] { Mathf.Pi, -Mathf.Pi * 0.5f, 0f, Mathf.Pi * 0.5f };

        for (var corner = 0; corner < 4; corner++)
        {
            for (var segment = 0; segment < segments; segment++)
            {
                var progress = segments == 1 ? 0f : segment / (float)(segments - 1);
                var angle = startAngles[corner] + progress * Mathf.Pi * 0.5f;
                points[corner * segments + segment] = centers[corner] +
                    new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
        }
        return points;
    }
}
