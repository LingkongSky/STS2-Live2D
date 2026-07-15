using Godot;
using Live2D.Api;
using Live2D.Scripts.Configuration;
using Live2D.Scripts.Models;
using Live2D.Scripts.Packs;
using Live2D.Scripts.Runtime;
using STS2RitsuLib;
using STS2RitsuLib.RuntimeInput;
using STS2RitsuLib.Settings;

namespace Live2D.Scripts.UI;

internal static partial class Live2DSettingsUi
{
    private static Control CreateModelRenderingEditor(
        string modelId,
        RenderingOverrides overrides,
        RenderingConfig global)
    {
        var root = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        root.AddThemeConstantOverride("separation", 12);
        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        AddNullableInt(grid, L("field.mask_size", "Mask Viewport Size (0 = Auto)"),
            overrides.MaskViewportSize, global.MaskViewportSize, 0, 4096,
            value => ModifyRendering(modelId, target => target.MaskViewportSize = value));
        AddNullableEnum(grid, L("field.blend_mode", "Blend Mode"), overrides.BlendMode, global.BlendMode,
            value => ModifyRendering(modelId, target => target.BlendMode = value));
        root.AddChild(grid);
        root.AddChild(CreateOptionalFilterEditor(modelId, overrides, global.Filter));
        root.AddChild(CreateOptionalMaskEditor(modelId, overrides, global.Mask));
        return root;
    }

    private static Control CreateOptionalFilterEditor(
        string modelId,
        RenderingOverrides overrides,
        FilterConfig global)
    {
        var root = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var enabled = new CheckBox
        {
            Text = L("rendering.override_filter", "Override global filter"),
            ButtonPressed = overrides.Filter is not null,
        };
        root.AddChild(enabled);
        var value = overrides.Filter ?? global;
        var current = CloneFilter(value);
        void Change(Action<FilterConfig> mutation)
        {
            mutation(current);
            ModifyRendering(modelId, target => target.Filter = current);
        }
        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var editable = new List<Control>();
        AddOptionalColor(grid, editable, L("field.tint", "Tint"),
            new Color(value.TintR, value.TintG, value.TintB, value.TintA), enabled.ButtonPressed,
            next => Change(filter =>
            {
                filter.TintR = next.R;
                filter.TintG = next.G;
                filter.TintB = next.B;
                filter.TintA = next.A;
            }));
        AddOptionalFloat(grid, editable, L("field.brightness", "Brightness"), value.Brightness, -1, 1, 0.01, enabled.ButtonPressed,
            next => Change(filter => filter.Brightness = next));
        AddOptionalFloat(grid, editable, L("field.contrast", "Contrast"), value.Contrast, 0, 4, 0.01, enabled.ButtonPressed,
            next => Change(filter => filter.Contrast = next));
        AddOptionalFloat(grid, editable, L("field.saturation", "Saturation"), value.Saturation, 0, 4, 0.01, enabled.ButtonPressed,
            next => Change(filter => filter.Saturation = next));
        AddOptionalFloat(grid, editable, L("field.grayscale", "Grayscale"), value.Grayscale, 0, 1, 0.01, enabled.ButtonPressed,
            next => Change(filter => filter.Grayscale = next));
        AddOptionalFloat(grid, editable, L("field.hue", "Hue Shift (degrees)"), value.HueShiftDegrees, -180, 180, 1, enabled.ButtonPressed,
            next => Change(filter => filter.HueShiftDegrees = next));
        AddOptionalFloat(grid, editable, L("field.invert", "Invert"), value.Invert, 0, 1, 0.01, enabled.ButtonPressed,
            next => Change(filter => filter.Invert = next));
        AddOptionalFloat(grid, editable, L("field.gamma", "Gamma"), value.Gamma, 0.01, 10, 0.01, enabled.ButtonPressed,
            next => Change(filter => filter.Gamma = next));
        enabled.Toggled += active =>
        {
            SetRenderingControlsEnabled(editable, active);
            ModifyRendering(modelId, target => target.Filter = active ? current : null);
        };
        root.AddChild(grid);
        return WrapCard(root);
    }

    private static Control CreateOptionalMaskEditor(
        string modelId,
        RenderingOverrides overrides,
        CanvasMaskConfig global)
    {
        var root = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var enabled = new CheckBox
        {
            Text = L("rendering.override_mask", "Override global canvas mask"),
            ButtonPressed = overrides.Mask is not null,
        };
        root.AddChild(enabled);
        var value = overrides.Mask ?? global;
        var current = CloneMask(value);
        void Change(Action<CanvasMaskConfig> mutation)
        {
            mutation(current);
            ModifyRendering(modelId, target => target.Mask = current);
        }
        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var editable = new List<Control>();
        AddOptionalEnum(grid, editable, L("field.canvas_mask", "Canvas Mask"), value.Type, enabled.ButtonPressed,
            next => Change(mask => mask.Type = next));
        AddOptionalFloat(grid, editable, L("field.mask_x", "Mask X"), value.X, -8000, 8000, 1, enabled.ButtonPressed,
            next => Change(mask => mask.X = next));
        AddOptionalFloat(grid, editable, L("field.mask_y", "Mask Y"), value.Y, -8000, 8000, 1, enabled.ButtonPressed,
            next => Change(mask => mask.Y = next));
        AddOptionalFloat(grid, editable, L("field.mask_width", "Mask Width"), value.Width, 1, 16000, 1, enabled.ButtonPressed,
            next => Change(mask => mask.Width = next));
        AddOptionalFloat(grid, editable, L("field.mask_height", "Mask Height"), value.Height, 1, 16000, 1, enabled.ButtonPressed,
            next => Change(mask => mask.Height = next));
        AddOptionalFloat(grid, editable, L("field.corner_radius", "Corner Radius"), value.CornerRadius, 0, 8000, 1, enabled.ButtonPressed,
            next => Change(mask => mask.CornerRadius = next));
        AddOptionalInt(grid, editable, L("field.mask_segments", "Mask Edge Segments"), value.SegmentsPerCorner, 2, 64, enabled.ButtonPressed,
            next => Change(mask => mask.SegmentsPerCorner = next));
        enabled.Toggled += active =>
        {
            SetRenderingControlsEnabled(editable, active);
            ModifyRendering(modelId, target => target.Mask = active ? current : null);
        };
        root.AddChild(grid);
        return WrapCard(root);
    }

    private static void AddNullableEnum<T>(
        GridContainer grid,
        string label,
        T? overrideValue,
        T globalValue,
        Action<T?> changed) where T : struct, Enum
    {
        grid.AddChild(new Label { Text = label });
        var input = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var values = Enum.GetValues<T>();
        input.AddItem(F("state.inherited_value", "Inherited global value: {0}", RenderingEnumName(globalValue)));
        foreach (var item in values)
            input.AddItem(RenderingEnumName(item));
        input.Selected = overrideValue.HasValue ? Array.IndexOf(values, overrideValue.Value) + 1 : 0;
        input.ItemSelected += index => changed(index == 0 ? null : values[index - 1]);
        grid.AddChild(input);
    }

    private static void AddOptionalFloat(
        GridContainer grid, List<Control> controls, string label, float value,
        double min, double max, double step, bool enabled, Action<float> changed)
    {
        grid.AddChild(new Label { Text = label });
        var input = new SpinBox
        {
            MinValue = min, MaxValue = max, Step = step, Value = value,
            Editable = enabled, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        input.ValueChanged += next => changed((float)next);
        controls.Add(input);
        grid.AddChild(input);
    }

    private static void AddOptionalInt(
        GridContainer grid, List<Control> controls, string label, int value,
        int min, int max, bool enabled, Action<int> changed)
    {
        grid.AddChild(new Label { Text = label });
        var input = new SpinBox
        {
            MinValue = min, MaxValue = max, Step = 1, Value = value, Rounded = true,
            Editable = enabled, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        input.ValueChanged += next => changed((int)next);
        controls.Add(input);
        grid.AddChild(input);
    }

    private static void AddOptionalEnum<T>(
        GridContainer grid, List<Control> controls, string label, T value,
        bool enabled, Action<T> changed) where T : struct, Enum
    {
        grid.AddChild(new Label { Text = label });
        var input = new OptionButton { Disabled = !enabled, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var values = Enum.GetValues<T>();
        foreach (var item in values)
            input.AddItem(RenderingEnumName(item));
        input.Selected = Array.IndexOf(values, value);
        input.ItemSelected += index => changed(values[index]);
        controls.Add(input);
        grid.AddChild(input);
    }

    private static void AddOptionalColor(
        GridContainer grid, List<Control> controls, string label, Color value,
        bool enabled, Action<Color> changed)
    {
        grid.AddChild(new Label { Text = label });
        var input = new ColorPickerButton
        {
            Color = value, EditAlpha = true, Disabled = !enabled,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        input.ColorChanged += color => changed(color);
        controls.Add(input);
        grid.AddChild(input);
    }

    private static void SetRenderingControlsEnabled(IEnumerable<Control> controls, bool enabled)
    {
        foreach (var control in controls)
        {
            if (control is SpinBox spinBox)
                spinBox.Editable = enabled;
            else if (control is BaseButton button)
                button.Disabled = !enabled;
        }
    }

    private static string RenderingEnumName<T>(T value) where T : struct, Enum
        => value switch
        {
            Live2DBlendMode.Normal => L("blend.normal", "Normal"),
            Live2DBlendMode.Add => L("blend.add", "Add"),
            Live2DBlendMode.Subtract => L("blend.subtract", "Subtract"),
            Live2DBlendMode.Multiply => L("blend.multiply", "Multiply"),
            Live2DBlendMode.PremultipliedAlpha => L("blend.premultiplied_alpha", "Premultiplied Alpha"),
            Live2DMaskType.None => L("mask.none", "None"),
            Live2DMaskType.Rectangle => L("mask.rectangle", "Rectangle"),
            Live2DMaskType.Ellipse => L("mask.ellipse", "Ellipse"),
            Live2DMaskType.RoundedRectangle => L("mask.rounded_rectangle", "Rounded Rectangle"),
            _ => value.ToString(),
        };

    private static FilterConfig CloneFilter(FilterConfig value) => new()
    {
        TintR = value.TintR,
        TintG = value.TintG,
        TintB = value.TintB,
        TintA = value.TintA,
        Brightness = value.Brightness,
        Contrast = value.Contrast,
        Saturation = value.Saturation,
        Grayscale = value.Grayscale,
        HueShiftDegrees = value.HueShiftDegrees,
        Invert = value.Invert,
        Gamma = value.Gamma,
    };

    private static CanvasMaskConfig CloneMask(CanvasMaskConfig value) => new()
    {
        Type = value.Type,
        X = value.X,
        Y = value.Y,
        Width = value.Width,
        Height = value.Height,
        CornerRadius = value.CornerRadius,
        SegmentsPerCorner = value.SegmentsPerCorner,
    };

}

