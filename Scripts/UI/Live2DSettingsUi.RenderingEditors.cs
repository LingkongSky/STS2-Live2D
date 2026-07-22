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
    private static readonly int[] MaskViewportSizePresets = [0, 256, 512, 1024, 2048, 4096];

    private static Control CreateModelRenderingEditor(
        string modelId,
        RenderingOverrides overrides,
        RenderingConfig global)
    {
        var root = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        root.AddThemeConstantOverride("separation", 12);
        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        AddNullableMaskViewportSize(grid, L("field.mask_size", "Mask Viewport Size (0 = Auto)"),
            overrides.MaskViewportSize, global.MaskViewportSize,
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
        AddOptionalSlider(grid, editable, L("field.brightness", "Brightness"), value.Brightness, -1, 1, 0.01, enabled.ButtonPressed,
            next => Change(filter => filter.Brightness = next));
        AddOptionalSlider(grid, editable, L("field.contrast", "Contrast"), value.Contrast, 0, 4, 0.01, enabled.ButtonPressed,
            next => Change(filter => filter.Contrast = next));
        AddOptionalSlider(grid, editable, L("field.saturation", "Saturation"), value.Saturation, 0, 4, 0.01, enabled.ButtonPressed,
            next => Change(filter => filter.Saturation = next));
        AddOptionalSlider(grid, editable, L("field.grayscale", "Grayscale"), value.Grayscale, 0, 1, 0.01, enabled.ButtonPressed,
            next => Change(filter => filter.Grayscale = next));
        AddOptionalSlider(grid, editable, L("field.hue", "Hue Shift (degrees)"), value.HueShiftDegrees, -180, 180, 1, enabled.ButtonPressed,
            next => Change(filter => filter.HueShiftDegrees = next), "°");
        AddOptionalSlider(grid, editable, L("field.invert", "Invert"), value.Invert, 0, 1, 0.01, enabled.ButtonPressed,
            next => Change(filter => filter.Invert = next));
        AddOptionalSlider(grid, editable, L("field.gamma", "Gamma"), value.Gamma, 0.01, 10, 0.01, enabled.ButtonPressed,
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
        var maskTypeInput = AddOptionalEnum(grid, editable, L("field.canvas_mask", "Canvas Mask"), value.Type, enabled.ButtonPressed,
            next => Change(mask => mask.Type = next));
        AddOptionalSlider(grid, editable, L("field.mask_x", "Mask X"), value.X, -8000, 8000, 1, enabled.ButtonPressed,
            next => Change(mask => mask.X = next));
        AddOptionalSlider(grid, editable, L("field.mask_y", "Mask Y"), value.Y, -8000, 8000, 1, enabled.ButtonPressed,
            next => Change(mask => mask.Y = next));
        AddOptionalSlider(grid, editable, L("field.mask_width", "Mask Width"), value.Width, 1, 16000, 1, enabled.ButtonPressed,
            next => Change(mask => mask.Width = next));
        AddOptionalSlider(grid, editable, L("field.mask_height", "Mask Height"), value.Height, 1, 16000, 1, enabled.ButtonPressed,
            next => Change(mask => mask.Height = next));
        AddOptionalSlider(grid, editable, L("field.corner_radius", "Corner Radius"), value.CornerRadius, 0, 8000, 1, enabled.ButtonPressed,
            next =>
            {
                maskTypeInput.Selected = Array.IndexOf(
                    Enum.GetValues<Live2DMaskType>(),
                    Live2DMaskType.RoundedRectangle);
                Change(mask =>
                {
                    mask.Type = Live2DMaskType.RoundedRectangle;
                    mask.CornerRadius = next;
                });
            });
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

    private static void AddNullableMaskViewportSize(
        GridContainer grid,
        string label,
        int? overrideValue,
        int globalValue,
        Action<int?> changed)
    {
        grid.AddChild(new Label { Text = label });
        var input = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        input.AddItem(F(
            "state.inherited_value",
            "Inherited global value: {0}",
            MaskViewportSizeName(globalValue)));
        var values = MaskViewportSizePresets.ToList();
        if (overrideValue.HasValue && !values.Contains(overrideValue.Value))
            values.Add(overrideValue.Value);
        foreach (var value in values)
            input.AddItem(MaskViewportSizeName(value));
        input.Selected = overrideValue.HasValue ? values.IndexOf(overrideValue.Value) + 1 : 0;
        input.ItemSelected += index => changed(index == 0 ? null : values[(int)index - 1]);
        grid.AddChild(input);
    }

    private static void AddOptionalSlider(
        GridContainer grid, List<Control> controls, string label, float value,
        double min, double max, double step, bool enabled, Action<float> changed,
        string suffix = "")
    {
        grid.AddChild(new Label { Text = label });
        var row = CreateRenderingSlider(
            value, min, max, step, enabled,
            next => changed((float)next), out var slider, out var input, suffix);
        controls.Add(slider);
        controls.Add(input);
        grid.AddChild(row);
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

    private static OptionButton AddOptionalEnum<T>(
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
        return input;
    }

    private static void AddOptionalColor(
        GridContainer grid, List<Control> controls, string label, Color value,
        bool enabled, Action<Color> changed)
    {
        grid.AddChild(new Label { Text = label });
        var input = CreateRenderingColorPicker(value, enabled, changed);
        controls.Add(input);
        grid.AddChild(input);
    }

    private static void AddGlobalRenderingFloat(
        GridContainer grid, string label, float value,
        double min, double max, double step, Action<float> changed,
        string suffix = "")
    {
        grid.AddChild(new Label { Text = label });
        grid.AddChild(CreateRenderingSlider(
            value, min, max, step, true,
            next => changed((float)next), out _, out _, suffix));
    }

    private static void AddGlobalMaskViewportSize(
        GridContainer grid,
        string label,
        int value,
        Action<int> changed)
    {
        grid.AddChild(new Label { Text = label });
        grid.AddChild(CreateMaskViewportSizeSelector(value, true, changed));
    }

    internal static OptionButton CreateMaskViewportSizeSelector(
        int value,
        bool enabled,
        Action<int> changed)
    {
        var values = MaskViewportSizePresets.ToList();
        if (!values.Contains(value))
            values.Add(value);
        var input = new OptionButton
        {
            Disabled = !enabled,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        foreach (var option in values)
            input.AddItem(MaskViewportSizeName(option));
        input.Selected = values.IndexOf(value);
        input.ItemSelected += index => changed(values[(int)index]);
        return input;
    }

    private static string MaskViewportSizeName(int value)
        => value == 0 ? L("state.auto", "Auto") : $"{value} × {value}";

    internal static HBoxContainer CreateRenderingSlider(
        double value,
        double min,
        double max,
        double step,
        bool enabled,
        Action<double> changed,
        out HSlider slider,
        out SpinBox input,
        string suffix = "")
    {
        var syncing = false;
        slider = new HSlider
        {
            MinValue = min,
            MaxValue = max,
            Step = step,
            Value = value,
            Editable = enabled,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        input = new SpinBox
        {
            MinValue = min,
            MaxValue = max,
            Step = step,
            Value = value,
            Editable = enabled,
            Suffix = suffix,
            CustomMinimumSize = new Vector2(112f, 0f),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd,
        };
        var capturedSlider = slider;
        var capturedInput = input;
        slider.ValueChanged += next =>
        {
            if (syncing)
                return;
            syncing = true;
            capturedInput.Value = next;
            syncing = false;
            changed(next);
        };
        input.ValueChanged += next =>
        {
            if (syncing)
                return;
            syncing = true;
            capturedSlider.Value = next;
            syncing = false;
            changed(next);
        };
        var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row.AddChild(slider);
        row.AddChild(input);
        return row;
    }

    internal static ColorPickerButton CreateRenderingColorPicker(
        Color value,
        bool enabled,
        Action<Color> changed)
    {
        var input = new ColorPickerButton
        {
            Color = value,
            EditAlpha = true,
            Disabled = !enabled,
            Text = $"#{value.ToHtml(true).ToUpperInvariant()}",
            CustomMinimumSize = new Vector2(180f, 42f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        input.ColorChanged += color =>
        {
            input.Text = $"#{color.ToHtml(true).ToUpperInvariant()}";
            changed(color);
        };
        return input;
    }

    private static void SetRenderingControlsEnabled(IEnumerable<Control> controls, bool enabled)
    {
        foreach (var control in controls)
        {
            if (control is SpinBox spinBox)
                spinBox.Editable = enabled;
            else if (control is Slider slider)
                slider.Editable = enabled;
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

