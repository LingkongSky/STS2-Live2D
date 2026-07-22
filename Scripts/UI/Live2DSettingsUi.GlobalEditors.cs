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
    private static Control CreateGlobalPlaybackEditor()
    {
        var global = Live2DConfigStore.Get().Global;
        var root = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        AddGlobalFloat(grid, L("field.speed", "Playback Speed"), global.Playback.Speed, 0, 4, 0.05,
            value => ModifyGlobal(target => target.Playback.Speed = value));
        AddGlobalBool(grid, L("field.physics", "Physics"), global.Playback.EnablePhysics,
            value => ModifyGlobal(target => target.Playback.EnablePhysics = value));
        AddGlobalBool(grid, L("field.pose", "Pose Processing"), global.Playback.EnablePose,
            value => ModifyGlobal(target => target.Playback.EnablePose = value));
        AddGlobalBool(grid, L("field.auto_idle", "Auto-play Idle"), global.Playback.AutoPlayIdle,
            value => ModifyGlobal(target => target.Playback.AutoPlayIdle = value));
        AddGlobalFloat(grid, L("field.cooldown", "Action Cooldown (seconds)"), global.Playback.ActionCooldownSeconds, 0, 10, 0.05,
            value => ModifyGlobal(target => target.Playback.ActionCooldownSeconds = value));
        AddGlobalMaskViewportSize(grid, L("field.mask_size", "Mask Viewport Size (0 = Auto)"), global.Rendering.MaskViewportSize,
            value => ModifyGlobal(target => target.Rendering.MaskViewportSize = value));
        AddGlobalEnum(grid, L("field.blend_mode", "Blend Mode"), global.Rendering.BlendMode,
            value => ModifyGlobal(target => target.Rendering.BlendMode = value));
        AddGlobalColor(grid, L("field.tint", "Tint"), new Color(
                global.Rendering.Filter.TintR,
                global.Rendering.Filter.TintG,
                global.Rendering.Filter.TintB,
                global.Rendering.Filter.TintA),
            value => ModifyGlobal(target =>
            {
                target.Rendering.Filter.TintR = value.R;
                target.Rendering.Filter.TintG = value.G;
                target.Rendering.Filter.TintB = value.B;
                target.Rendering.Filter.TintA = value.A;
            }));
        AddGlobalRenderingFloat(grid, L("field.brightness", "Brightness"), global.Rendering.Filter.Brightness, -1, 1, 0.01,
            value => ModifyGlobal(target => target.Rendering.Filter.Brightness = value));
        AddGlobalRenderingFloat(grid, L("field.contrast", "Contrast"), global.Rendering.Filter.Contrast, 0, 4, 0.01,
            value => ModifyGlobal(target => target.Rendering.Filter.Contrast = value));
        AddGlobalRenderingFloat(grid, L("field.saturation", "Saturation"), global.Rendering.Filter.Saturation, 0, 4, 0.01,
            value => ModifyGlobal(target => target.Rendering.Filter.Saturation = value));
        AddGlobalRenderingFloat(grid, L("field.grayscale", "Grayscale"), global.Rendering.Filter.Grayscale, 0, 1, 0.01,
            value => ModifyGlobal(target => target.Rendering.Filter.Grayscale = value));
        AddGlobalRenderingFloat(grid, L("field.hue", "Hue Shift (degrees)"), global.Rendering.Filter.HueShiftDegrees, -180, 180, 1,
            value => ModifyGlobal(target => target.Rendering.Filter.HueShiftDegrees = value), "°");
        AddGlobalRenderingFloat(grid, L("field.invert", "Invert"), global.Rendering.Filter.Invert, 0, 1, 0.01,
            value => ModifyGlobal(target => target.Rendering.Filter.Invert = value));
        AddGlobalRenderingFloat(grid, L("field.gamma", "Gamma"), global.Rendering.Filter.Gamma, 0.01, 10, 0.01,
            value => ModifyGlobal(target => target.Rendering.Filter.Gamma = value));
        var maskTypeInput = AddGlobalEnum(grid, L("field.canvas_mask", "Canvas Mask"), global.Rendering.Mask.Type,
            value => ModifyGlobal(target => target.Rendering.Mask.Type = value));
        AddGlobalRenderingFloat(grid, L("field.mask_x", "Mask X"), global.Rendering.Mask.X, -8000, 8000, 1,
            value => ModifyGlobal(target => target.Rendering.Mask.X = value));
        AddGlobalRenderingFloat(grid, L("field.mask_y", "Mask Y"), global.Rendering.Mask.Y, -8000, 8000, 1,
            value => ModifyGlobal(target => target.Rendering.Mask.Y = value));
        AddGlobalRenderingFloat(grid, L("field.mask_width", "Mask Width"), global.Rendering.Mask.Width, 1, 16000, 1,
            value => ModifyGlobal(target => target.Rendering.Mask.Width = value));
        AddGlobalRenderingFloat(grid, L("field.mask_height", "Mask Height"), global.Rendering.Mask.Height, 1, 16000, 1,
            value => ModifyGlobal(target => target.Rendering.Mask.Height = value));
        AddGlobalRenderingFloat(grid, L("field.corner_radius", "Corner Radius"), global.Rendering.Mask.CornerRadius, 0, 8000, 1,
            value =>
            {
                maskTypeInput.Selected = Array.IndexOf(
                    Enum.GetValues<Live2DMaskType>(),
                    Live2DMaskType.RoundedRectangle);
                ModifyGlobal(target =>
                {
                    target.Rendering.Mask.Type = Live2DMaskType.RoundedRectangle;
                    target.Rendering.Mask.CornerRadius = value;
                });
            });
        root.AddChild(grid);
        return root;
    }

    private static Control CreateGlobalSceneEditor(Live2DSceneKind scene, SceneDisplayConfig config)
    {
        var box = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        AddGlobalBool(grid, L("field.visible", "Visible"), config.Visible,
            value => ModifyGlobalScene(scene, target => target.Visible = value));
        AddGlobalAnchor(grid, config.Anchor,
            value => ModifyGlobalScene(scene, target => target.Anchor = value));
        AddGlobalFloat(grid, L("field.offset_x", "Horizontal Offset"), config.OffsetX, -4000, 4000, 1,
            value => ModifyGlobalScene(scene, target => target.OffsetX = value));
        AddGlobalFloat(grid, L("field.offset_y", "Vertical Offset"), config.OffsetY, -4000, 4000, 1,
            value => ModifyGlobalScene(scene, target => target.OffsetY = value));
        AddGlobalFloat(grid, L("field.scale", "Scale"), config.Scale, 0.01, 4, 0.01,
            value => ModifyGlobalScene(scene, target => target.Scale = value));
        AddGlobalFloat(grid, L("field.rotation", "Rotation"), config.RotationDegrees, -180, 180, 1,
            value => ModifyGlobalScene(scene, target => target.RotationDegrees = value));
        AddGlobalFloat(grid, L("field.opacity", "Opacity"), config.Opacity, 0, 1, 0.01,
            value => ModifyGlobalScene(scene, target => target.Opacity = value));
        AddGlobalInt(grid, L("field.layer", "Display Layer"), config.Layer, -100, 100,
            value => ModifyGlobalScene(scene, target => target.Layer = value));
        AddGlobalBool(grid, L("field.mouse", "Mouse Interaction"), config.MouseInteraction,
            value => ModifyGlobalScene(scene, target => target.MouseInteraction = value));
        box.AddChild(grid);
        return box;
    }

    private static void AddGlobalBool(GridContainer grid, string label, bool value, Action<bool> changed)
    {
        grid.AddChild(new Label { Text = label });
        var input = new CheckBox
        {
            Text = value ? L("state.on", "On") : L("state.off", "Off"),
            ButtonPressed = value,
        };
        input.Toggled += active =>
        {
            input.Text = active ? L("state.on", "On") : L("state.off", "Off");
            changed(active);
        };
        grid.AddChild(input);
    }

    private static void AddGlobalAnchor(GridContainer grid, AnchorPreset value, Action<AnchorPreset> changed)
    {
        grid.AddChild(new Label { Text = L("field.anchor", "Anchor") });
        var input = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        foreach (var anchor in Enum.GetValues<AnchorPreset>())
            input.AddItem(AnchorName(anchor));
        input.Selected = (int)value;
        input.ItemSelected += index => changed((AnchorPreset)index);
        grid.AddChild(input);
    }

    private static void AddGlobalFloat(
        GridContainer grid,
        string label,
        float value,
        double min,
        double max,
        double step,
        Action<float> changed)
    {
        grid.AddChild(new Label { Text = label });
        var input = new SpinBox
        {
            MinValue = min,
            MaxValue = max,
            Step = step,
            Value = value,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        input.ValueChanged += newValue => changed((float)newValue);
        grid.AddChild(input);
    }

    private static void AddGlobalInt(
        GridContainer grid,
        string label,
        int value,
        int min,
        int max,
        Action<int> changed)
    {
        grid.AddChild(new Label { Text = label });
        var input = new SpinBox
        {
            MinValue = min,
            MaxValue = max,
            Step = 1,
            Value = value,
            Rounded = true,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        input.ValueChanged += newValue => changed((int)newValue);
        grid.AddChild(input);
    }

    private static OptionButton AddGlobalEnum<T>(
        GridContainer grid,
        string label,
        T value,
        Action<T> changed) where T : struct, Enum
    {
        grid.AddChild(new Label { Text = label });
        var input = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var values = Enum.GetValues<T>();
        foreach (var item in values)
            input.AddItem(RenderingEnumName(item));
        input.Selected = Array.IndexOf(values, value);
        input.ItemSelected += index => changed(values[index]);
        grid.AddChild(input);
        return input;
    }

    private static void AddGlobalColor(
        GridContainer grid,
        string label,
        Color value,
        Action<Color> changed)
    {
        grid.AddChild(new Label { Text = label });
        var input = CreateRenderingColorPicker(value, true, changed);
        grid.AddChild(input);
    }

}

