using Godot;
using Live2D.Api;
using Live2D.Scripts.Configuration;
using Live2D.Scripts.Models;
using Live2D.Scripts.Runtime;
using STS2RitsuLib;
using STS2RitsuLib.Settings;

namespace Live2D.Scripts.UI;

internal sealed partial class Live2DPreviewEditor : CanvasLayer
{
    private const string OverlayNodeName = "Live2DPreviewEditor";
    private readonly Dictionary<Live2DSceneKind, PreviewDraft> _drafts = new();
    private IModSettingsUiActionHost _uiHost = null!;
    private Live2DModelConfig _model = null!;
    private ResolvedLive2DConfig _resolved = null!;
    private int _globalMaskViewportSize;
    private int _maskViewportDraft;
    private Live2DBlendMode _globalBlendMode;
    private Live2DBlendMode _blendDraft;
    private FilterConfig _globalFilter = null!;
    private FilterConfig _filterDraft = null!;
    private CanvasMaskConfig _globalMask = null!;
    private CanvasMaskConfig _maskDraft = null!;
    private Live2DPreviewCanvas _canvas = null!;
    private Label _previewError = null!;
    private Live2DModelInstance? _preview;
    private Line2D? _maskOutline;
    private OptionButton _sceneChoice = null!;
    private OptionButton _resolutionChoice = null!;
    private SpinBox _offsetX = null!;
    private SpinBox _offsetY = null!;
    private HSlider _scale = null!;
    private HSlider _rotation = null!;
    private SpinBox _scaleInput = null!;
    private SpinBox _rotationInput = null!;
    private CheckBox _maskViewportOverride = null!;
    private OptionButton _maskViewportInput = null!;
    private CheckBox _filterOverride = null!;
    private CheckBox _maskOverride = null!;
    private CheckBox _maskCanvasEdit = null!;
    private OptionButton _maskTypeInput = null!;
    private readonly List<Control> _filterInputs = [];
    private readonly List<Control> _maskInputs = [];
    private readonly List<Action<CanvasMaskConfig>> _maskControlRefreshers = [];
    private Live2DSceneKind _currentScene;
    private Vector2 _previewViewportSize = Live2DLayout.ReferenceViewportSize;
    private bool _maskViewportOverrideEnabled;
    private bool _blendOverrideEnabled;
    private bool _filterOverrideEnabled;
    private bool _maskOverrideEnabled;
    // 同步滑条和数字输入框时，阻止 ValueChanged 事件反向写回草稿。
    private bool _updatingControls;

    public static void Show(string modelId, IModSettingsUiActionHost uiHost)
    {
        if (Engine.GetMainLoop() is not SceneTree tree || tree.Root == null)
            return;

        tree.Root.GetNodeOrNull<CanvasLayer>(OverlayNodeName)?.QueueFree();
        var settings = Live2DConfigStore.Get();
        var model = settings.Models.FirstOrDefault(value => value.Id == modelId);
        if (model == null)
            return;

        var editor = new Live2DPreviewEditor
        {
            Name = OverlayNodeName,
            Layer = 150,
        };
        editor.Configure(model, settings.Global, uiHost);
        tree.Root.AddChild(editor);
    }

    private void Configure(
        Live2DModelConfig model,
        GlobalLive2DConfig global,
        IModSettingsUiActionHost uiHost)
    {
        _model = model;
        _uiHost = uiHost;
        _resolved = Live2DConfigResolver.Resolve(global, model.Overrides);
        _globalMaskViewportSize = global.Rendering.MaskViewportSize;
        _maskViewportOverrideEnabled = model.Overrides.Rendering.MaskViewportSize.HasValue;
        _maskViewportDraft = model.Overrides.Rendering.MaskViewportSize ?? global.Rendering.MaskViewportSize;
        _globalBlendMode = global.Rendering.BlendMode;
        _blendOverrideEnabled = model.Overrides.Rendering.BlendMode.HasValue;
        _blendDraft = model.Overrides.Rendering.BlendMode ?? global.Rendering.BlendMode;
        _globalFilter = CloneFilter(global.Rendering.Filter);
        _filterOverrideEnabled = model.Overrides.Rendering.Filter is not null;
        _filterDraft = CloneFilter(model.Overrides.Rendering.Filter ?? global.Rendering.Filter);
        _globalMask = CloneMask(global.Rendering.Mask);
        _maskOverrideEnabled = model.Overrides.Rendering.Mask is not null;
        _maskDraft = CloneMask(model.Overrides.Rendering.Mask ?? global.Rendering.Mask);
        if (Engine.GetMainLoop() is SceneTree tree)
        {
            var currentSize = tree.Root.GetVisibleRect().Size;
            if (currentSize.X > 0f && currentSize.Y > 0f)
                _previewViewportSize = currentSize;
        }
        foreach (var scene in Enum.GetValues<Live2DSceneKind>())
            _drafts[scene] = PreviewDraft.From(Live2DConfigResolver.ForScene(_resolved, scene));
        BuildUi();
    }

    public override void _Ready()
    {
        Callable.From(RebuildPreview).CallDeferred();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    private void BuildUi()
    {
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Stop };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        var backdrop = new ColorRect
        {
            Color = new Color(0.025f, 0.035f, 0.055f, 0.985f),
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(backdrop);

        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 28);
        root.AddChild(margin);

        var page = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        page.AddThemeConstantOverride("separation", 16);
        margin.AddChild(page);

        var header = new HBoxContainer();
        var title = new Label
        {
            Text = F("preview.title", "Live2D Preview — {0}", _model.DisplayName),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        title.AddThemeFontSizeOverride("font_size", 28);
        title.AddThemeColorOverride("font_color", new Color(0.96f, 0.82f, 0.45f));
        header.AddChild(title);
        header.AddChild(new Label { Text = L("preview.scene", "Target Scene") });
        _sceneChoice = new OptionButton { CustomMinimumSize = new Vector2(190f, 0f) };
        foreach (var scene in Enum.GetValues<Live2DSceneKind>())
            _sceneChoice.AddItem(Live2DSettingsUi.SceneName(scene));
        _sceneChoice.ItemSelected += index => SelectScene((Live2DSceneKind)index);
        header.AddChild(_sceneChoice);
        header.AddChild(new Label { Text = L("preview.resolution", "Preview Resolution") });
        _resolutionChoice = new OptionButton { CustomMinimumSize = new Vector2(205f, 0f) };
        AddResolutionOption(_previewViewportSize,
            F("preview.resolution_current", "Current Window ({0}×{1})",
                Math.Round(_previewViewportSize.X), Math.Round(_previewViewportSize.Y)));
        AddResolutionOption(new Vector2(1920f, 1080f), "1920 × 1080 (16:9)");
        AddResolutionOption(new Vector2(2560f, 1440f), "2560 × 1440 (16:9)");
        AddResolutionOption(new Vector2(3440f, 1440f), "3440 × 1440 (21:9)");
        AddResolutionOption(new Vector2(3840f, 2160f), "3840 × 2160 (4K)");
        _resolutionChoice.ItemSelected += SelectResolution;
        header.AddChild(_resolutionChoice);
        page.AddChild(header);

        var content = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        content.AddThemeConstantOverride("separation", 18);
        page.AddChild(content);

        var previewColumn = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        previewColumn.AddThemeConstantOverride("separation", 10);
        _previewError = new Label
        {
            Visible = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Modulate = new Color(1f, 0.45f, 0.35f),
        };
        previewColumn.AddChild(_previewError);
        var previewPanel = new PanelContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        previewPanel.AddThemeStyleboxOverride("panel", CreatePanelStyle(new Color(0.035f, 0.055f, 0.08f)));
        _canvas = new Live2DPreviewCanvas
        {
            CustomMinimumSize = new Vector2(720f, 520f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            ClipContents = true,
        };
        _canvas.Dragged += MovePreview;
        _canvas.ScaleRequested += delta => ChangeScale(CurrentDraft.Scale + delta);
        _canvas.RotateRequested += delta => ChangeRotation(CurrentDraft.RotationDegrees + delta);
        _canvas.MaskDragged += MoveMask;
        _canvas.MaskResizeRequested += ResizeMask;
        _canvas.Resized += ApplyPreviewTransform;
        previewPanel.AddChild(_canvas);
        previewColumn.AddChild(previewPanel);
        content.AddChild(previewColumn);

        var inspectorPanel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(340f, 0f),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        inspectorPanel.AddThemeStyleboxOverride("panel", CreatePanelStyle(new Color(0.055f, 0.07f, 0.1f)));
        var inspectorMargin = new MarginContainer();
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            inspectorMargin.AddThemeConstantOverride(side, 18);
        inspectorPanel.AddChild(inspectorMargin);
        var inspectorScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        inspectorMargin.AddChild(inspectorScroll);
        var inspector = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        inspector.AddThemeConstantOverride("separation", 12);
        inspectorScroll.AddChild(inspector);
        inspector.AddChild(CreateSectionTitle(L("preview.transform_title", "Transform")));

        _offsetX = CreateSpinBox(-4000, 4000, 1, value => ChangeOffset(x: (float)value));
        _offsetY = CreateSpinBox(-4000, 4000, 1, value => ChangeOffset(y: (float)value));
        inspector.AddChild(CreateField(L("field.offset_x", "Horizontal Offset"), _offsetX));
        inspector.AddChild(CreateField(L("field.offset_y", "Vertical Offset"), _offsetY));

        (_scale, _scaleInput) = CreateSlider(0.01, 4, 0.01, value => ChangeScale((float)value));
        inspector.AddChild(CreateField(L("field.scale", "Scale"), CreateSliderRow(_scale, _scaleInput)));
        (_rotation, _rotationInput) = CreateSlider(
            -180,
            180,
            1,
            value => ChangeRotation((float)value),
            "°");
        inspector.AddChild(CreateField(L("field.rotation", "Rotation"), CreateSliderRow(_rotation, _rotationInput)));

        inspector.AddChild(new Label
        {
            Text = L("preview.help", "Drag to move · Mouse wheel to scale · Shift + wheel to rotate"),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Modulate = new Color(0.78f, 0.8f, 0.84f),
        });
        inspector.AddChild(new HSeparator());
        inspector.AddChild(CreateRenderingEditor());
        content.AddChild(inspectorPanel);

        var footer = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.End,
        };
        var cancel = new Button { Text = L("button.cancel", "Cancel"), CustomMinimumSize = new Vector2(130f, 44f) };
        cancel.Pressed += Close;
        footer.AddChild(cancel);
        var save = new Button { Text = L("button.save", "Save Changes"), CustomMinimumSize = new Vector2(160f, 44f) };
        save.Pressed += Save;
        footer.AddChild(save);
        page.AddChild(footer);
    }

    private PreviewDraft CurrentDraft => _drafts[_currentScene];

    private void SelectScene(Live2DSceneKind scene)
    {
        _currentScene = scene;
        RebuildPreview();
    }

    private void AddResolutionOption(Vector2 size, string label)
    {
        _resolutionChoice.AddItem(label);
        var index = _resolutionChoice.ItemCount - 1;
        _resolutionChoice.SetItemMetadata(index, size);
    }

    private void SelectResolution(long index)
    {
        var metadata = _resolutionChoice.GetItemMetadata((int)index);
        if (metadata.VariantType != Variant.Type.Vector2)
            return;
        _previewViewportSize = metadata.AsVector2();
        _canvas.SetSimulationSize(_previewViewportSize);
        RebuildPreview();
    }

    private void RebuildPreview()
    {
        if (_canvas == null || !GodotObject.IsInstanceValid(_canvas) || !_canvas.IsInsideTree())
            return;

        if (_preview != null && GodotObject.IsInstanceValid(_preview.Root))
            _preview.Root.QueueFree();
        _preview = null;
        _maskOutline = null;
        _previewError.Visible = false;
        _previewError.Text = "";
        UpdateControls();

        try
        {
            var source = Live2DConfigResolver.ForScene(_resolved, _currentScene);
            var draft = CurrentDraft;
            var config = new SceneDisplayConfig
            {
                Visible = true,
                Anchor = draft.Anchor,
                OffsetX = draft.OffsetX,
                OffsetY = draft.OffsetY,
                Scale = draft.Scale,
                RotationDegrees = draft.RotationDegrees,
                Opacity = source.Opacity,
                Layer = 0,
                MouseInteraction = false,
            };
            _canvas.SetSimulationSize(_previewViewportSize);
            var definition = new Live2DRuntimeModelDefinition(
                new Live2DRuntimeModelIdentity(
                    _model.Id,
                    Entry.ModId,
                    null,
                    _model.Id,
                    _model.Id,
                    _currentScene == Live2DSceneKind.MainMenu
                        ? Live2DScene.MainMenu
                        : Live2DScene.InGame),
                _model,
                Live2DModelRepository.GetAbsoluteModelPath(_model));
            _preview = Live2DModelInstance.Create(definition, _resolved, config, _previewViewportSize);
            _maskOutline = new Line2D
            {
                Name = "Live2DPreviewMaskOutline",
                Width = 2f,
                Closed = true,
                DefaultColor = new Color(0.2f, 0.85f, 1f, 0.95f),
                ZIndex = 4090,
            };
            _preview.Root.AddChild(_maskOutline);
            ApplyPreviewRendering();
            _canvas.AddPreview(_preview.Root);
            Entry.Logger.Info(
                $"[{Entry.ModId}] Opened preview for model {_model.Id} in {_currentScene} " +
                $"({_previewViewportSize.X:0}x{_previewViewportSize.Y:0}).");
            ApplyPreviewTransform();
        }
        catch (Exception ex)
        {
            _previewError.Text = F("preview.load_error", "Unable to load preview: {0}", ex.Message);
            _previewError.Visible = true;
            Entry.Logger.Error(
                $"[{Entry.ModId}] Failed to open preview for model {_model.Id} in {_currentScene}: {ex}");
        }
    }

    private void ApplyPreviewTransform()
    {
        if (_preview == null || !GodotObject.IsInstanceValid(_preview.Root))
            return;
        var draft = CurrentDraft;
        _preview.Apply(new Live2DModelUpdate
        {
            Position = Live2DLayout.ResolvePosition(
                _previewViewportSize,
                draft.Anchor,
                draft.OffsetX,
                draft.OffsetY),
            Scale = Vector2.One * Live2DLayout.ResolveModelScale(draft.Scale, _previewViewportSize),
            RotationDegrees = draft.RotationDegrees,
        });
    }

    private void MovePreview(Vector2 delta)
    {
        var draft = CurrentDraft;
        var referenceDelta = Live2DLayout.ToReferenceDelta(delta, _previewViewportSize);
        draft.OffsetX += referenceDelta.X;
        draft.OffsetY += referenceDelta.Y;
        draft.PositionDirty = true;
        ApplyPreviewTransform();
        UpdateControls();
    }

    private void ChangeOffset(float? x = null, float? y = null)
    {
        if (_updatingControls)
            return;
        var draft = CurrentDraft;
        if (x.HasValue) draft.OffsetX = x.Value;
        if (y.HasValue) draft.OffsetY = y.Value;
        draft.PositionDirty = true;
        ApplyPreviewTransform();
    }

    private void ChangeScale(float value)
    {
        if (_updatingControls)
            return;
        var draft = CurrentDraft;
        draft.Scale = Math.Clamp(value, 0.01f, 4f);
        draft.ScaleDirty = true;
        ApplyPreviewTransform();
        UpdateControls();
    }

    private void ChangeRotation(float value)
    {
        if (_updatingControls)
            return;
        var draft = CurrentDraft;
        draft.RotationDegrees = Mathf.Wrap(value + 180f, 0f, 360f) - 180f;
        draft.RotationDirty = true;
        ApplyPreviewTransform();
        UpdateControls();
    }

    private Control CreateRenderingEditor()
    {
        var root = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        root.AddThemeConstantOverride("separation", 10);
        root.AddChild(CreateSectionTitle(L("preview.rendering_title", "Rendering")));

        _maskViewportOverride = new CheckBox
        {
            Text = L("preview.override_mask_size", "Override global mask viewport size"),
            ButtonPressed = _maskViewportOverrideEnabled,
        };
        _maskViewportOverride.Toggled += enabled =>
        {
            if (_updatingControls)
                return;
            _maskViewportOverrideEnabled = enabled;
            SetControlEnabled(_maskViewportInput, enabled);
            ApplyPreviewRendering();
        };
        root.AddChild(_maskViewportOverride);
        _maskViewportInput = Live2DSettingsUi.CreateMaskViewportSizeSelector(
            _maskViewportDraft,
            _maskViewportOverrideEnabled,
            value =>
        {
            if (_updatingControls)
                return;
            _maskViewportDraft = value;
            ApplyPreviewRendering();
        });
        root.AddChild(CreateField(L("field.mask_size", "Mask Viewport Size (0 = Auto)"), _maskViewportInput));

        var blendModes = Enum.GetValues<Live2DBlendMode>();
        var blend = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        blend.AddItem(F(
            "state.inherited_value",
            "Inherited global value: {0}",
            BlendModeName(_globalBlendMode)));
        foreach (var mode in blendModes)
            blend.AddItem(BlendModeName(mode));
        blend.Selected = _blendOverrideEnabled ? Array.IndexOf(blendModes, _blendDraft) + 1 : 0;
        blend.ItemSelected += index =>
        {
            if (_updatingControls)
                return;
            _blendOverrideEnabled = index > 0;
            if (_blendOverrideEnabled)
                _blendDraft = blendModes[index - 1];
            ApplyPreviewRendering();
        };
        root.AddChild(CreateField(L("field.blend_mode", "Blend Mode"), blend));

        root.AddChild(new HSeparator());
        root.AddChild(CreateSectionTitle(L("preview.filter_title", "Filter")));
        _filterOverride = new CheckBox
        {
            Text = L("rendering.override_filter", "Override global filter"),
            ButtonPressed = _filterOverrideEnabled,
        };
        _filterOverride.Toggled += enabled =>
        {
            if (_updatingControls)
                return;
            _filterOverrideEnabled = enabled;
            SetControlsEnabled(_filterInputs, enabled);
            ApplyPreviewRendering();
        };
        root.AddChild(_filterOverride);

        var tint = Live2DSettingsUi.CreateRenderingColorPicker(
            new Color(
                _filterDraft.TintR,
                _filterDraft.TintG,
                _filterDraft.TintB,
                _filterDraft.TintA),
            _filterOverrideEnabled,
            color => ChangeFilter(filter =>
            {
                filter.TintR = color.R;
                filter.TintG = color.G;
                filter.TintB = color.B;
                filter.TintA = color.A;
            }));
        AddRenderingInput(root, _filterInputs, L("field.tint", "Tint"), tint);
        AddFilterSlider(root, L("field.brightness", "Brightness"), _filterDraft.Brightness, -1, 1, 0.01,
            value => ChangeFilter(filter => filter.Brightness = (float)value));
        AddFilterSlider(root, L("field.contrast", "Contrast"), _filterDraft.Contrast, 0, 4, 0.01,
            value => ChangeFilter(filter => filter.Contrast = (float)value));
        AddFilterSlider(root, L("field.saturation", "Saturation"), _filterDraft.Saturation, 0, 4, 0.01,
            value => ChangeFilter(filter => filter.Saturation = (float)value));
        AddFilterSlider(root, L("field.grayscale", "Grayscale"), _filterDraft.Grayscale, 0, 1, 0.01,
            value => ChangeFilter(filter => filter.Grayscale = (float)value));
        AddFilterSlider(root, L("field.hue", "Hue Shift (degrees)"), _filterDraft.HueShiftDegrees, -180, 180, 1,
            value => ChangeFilter(filter => filter.HueShiftDegrees = (float)value), "°");
        AddFilterSlider(root, L("field.invert", "Invert"), _filterDraft.Invert, 0, 1, 0.01,
            value => ChangeFilter(filter => filter.Invert = (float)value));
        AddFilterSlider(root, L("field.gamma", "Gamma"), _filterDraft.Gamma, 0.01, 10, 0.01,
            value => ChangeFilter(filter => filter.Gamma = (float)value));
        SetControlsEnabled(_filterInputs, _filterOverrideEnabled);

        root.AddChild(new HSeparator());
        root.AddChild(CreateSectionTitle(L("preview.mask_title", "Canvas Mask")));

        _maskOverride = new CheckBox
        {
            Text = L("rendering.override_mask", "Override global canvas mask"),
            ButtonPressed = _maskOverrideEnabled,
        };
        _maskOverride.Toggled += ChangeMaskOverride;
        root.AddChild(_maskOverride);

        var fitMask = new Button
        {
            Text = L("preview.mask_fit_model", "Enable and fit to model"),
            CustomMinimumSize = new Vector2(0f, 38f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        fitMask.Pressed += FitMaskToModel;
        root.AddChild(fitMask);

        _maskCanvasEdit = new CheckBox
        {
            Text = L("preview.mask_edit_canvas", "Edit mask directly on canvas"),
        };
        _maskCanvasEdit.Toggled += ToggleMaskCanvasEditing;
        root.AddChild(_maskCanvasEdit);

        var maskTypes = Enum.GetValues<Live2DMaskType>();
        _maskTypeInput = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        foreach (var type in maskTypes)
            _maskTypeInput.AddItem(MaskTypeName(type));
        _maskTypeInput.Selected = Math.Max(0, Array.IndexOf(maskTypes, _maskDraft.Type));
        _maskTypeInput.ItemSelected += index => ChangeMask(mask => mask.Type = maskTypes[index]);
        AddMaskInput(root, L("field.canvas_mask", "Canvas Mask"), _maskTypeInput);

        AddMaskSlider(root, L("field.mask_x", "Mask X"), _maskDraft.X, -8000, 8000, 1,
            value => ChangeMask(mask => mask.X = (float)value), mask => mask.X);
        AddMaskSlider(root, L("field.mask_y", "Mask Y"), _maskDraft.Y, -8000, 8000, 1,
            value => ChangeMask(mask => mask.Y = (float)value), mask => mask.Y);
        AddMaskSlider(root, L("field.mask_width", "Mask Width"), _maskDraft.Width, 1, 16000, 1,
            value => ChangeMask(mask => mask.Width = (float)value), mask => mask.Width);
        AddMaskSlider(root, L("field.mask_height", "Mask Height"), _maskDraft.Height, 1, 16000, 1,
            value => ChangeMask(mask => mask.Height = (float)value), mask => mask.Height);
        AddMaskSlider(root, L("field.corner_radius", "Corner Radius"), _maskDraft.CornerRadius, 0, 8000, 1,
            value => ChangeCornerRadius((float)value), mask => mask.CornerRadius);
        SetMaskInputsEnabled(_maskOverrideEnabled);
        return root;
    }

    private void AddFilterSlider(
        VBoxContainer root,
        string label,
        double value,
        double min,
        double max,
        double step,
        Action<double> changed,
        string suffix = "")
    {
        var row = Live2DSettingsUi.CreateRenderingSlider(
            value,
            min,
            max,
            step,
            _filterOverrideEnabled,
            changed,
            out var slider,
            out var input,
            suffix);
        _filterInputs.Add(slider);
        _filterInputs.Add(input);
        root.AddChild(CreateField(label, row));
    }

    private static void AddRenderingInput(
        VBoxContainer root,
        List<Control> inputs,
        string label,
        Control input)
    {
        inputs.Add(input);
        root.AddChild(CreateField(label, input));
    }

    private void ChangeFilter(Action<FilterConfig> mutation)
    {
        if (_updatingControls)
            return;
        mutation(_filterDraft);
        ApplyPreviewRendering();
    }

    private void AddMaskSlider(
        VBoxContainer root,
        string label,
        double value,
        double min,
        double max,
        double step,
        Action<double> changed,
        Func<CanvasMaskConfig, double> read,
        bool rounded = false)
    {
        var row = Live2DSettingsUi.CreateRenderingSlider(
            value,
            min,
            max,
            step,
            _maskOverrideEnabled,
            changed,
            out var slider,
            out var input);
        input.Rounded = rounded;
        _maskInputs.Add(slider);
        _maskInputs.Add(input);
        _maskControlRefreshers.Add(mask =>
        {
            var next = read(mask);
            slider.Value = next;
            input.Value = next;
        });
        root.AddChild(CreateField(label, row));
    }

    private void AddMaskInput(VBoxContainer root, string label, Control input)
    {
        _maskInputs.Add(input);
        root.AddChild(CreateField(label, input));
    }

    private void ChangeMaskOverride(bool enabled)
    {
        if (_updatingControls)
            return;
        _maskOverrideEnabled = enabled;
        SetMaskInputsEnabled(enabled);
        if (!enabled && _maskCanvasEdit.ButtonPressed)
        {
            _maskCanvasEdit.SetPressedNoSignal(false);
            _canvas.SetMaskEditing(false);
        }
        ApplyPreviewRendering();
    }

    private void ChangeMask(Action<CanvasMaskConfig> mutation)
    {
        if (_updatingControls)
            return;
        mutation(_maskDraft);
        ApplyPreviewRendering();
    }

    private void ChangeCornerRadius(float radius)
    {
        if (_updatingControls)
            return;
        _maskDraft.Type = Live2DMaskType.RoundedRectangle;
        _maskDraft.CornerRadius = radius;
        UpdateMaskControls();
        ApplyPreviewRendering();
    }

    private void FitMaskToModel()
    {
        if (_preview == null || !GodotObject.IsInstanceValid(_preview.Root))
            return;

        var bounds = _preview.CanvasBounds;
        _maskOverrideEnabled = true;
        _maskOverride.SetPressedNoSignal(true);
        _maskDraft.Type = Live2DMaskType.Rectangle;
        _maskDraft.X = bounds.Position.X;
        _maskDraft.Y = bounds.Position.Y;
        _maskDraft.Width = Math.Max(1f, bounds.Size.X);
        _maskDraft.Height = Math.Max(1f, bounds.Size.Y);
        _maskDraft.CornerRadius = Math.Min(
            _maskDraft.CornerRadius,
            Math.Min(_maskDraft.Width, _maskDraft.Height) * 0.5f);
        SetMaskInputsEnabled(true);
        UpdateMaskControls();
        ApplyPreviewRendering();
    }

    private void ToggleMaskCanvasEditing(bool enabled)
    {
        if (enabled)
        {
            if (!_maskOverrideEnabled)
            {
                _maskOverrideEnabled = true;
                _maskOverride.SetPressedNoSignal(true);
                SetMaskInputsEnabled(true);
            }
            if (_maskDraft.Type == Live2DMaskType.None)
                FitMaskToModel();
        }
        _canvas.SetMaskEditing(enabled);
        ApplyPreviewRendering();
    }

    private void MoveMask(Vector2 simulationDelta)
    {
        if (!_maskOverrideEnabled)
            return;
        var draft = CurrentDraft;
        var modelScale = Math.Max(0.001f, Live2DLayout.ResolveModelScale(draft.Scale, _previewViewportSize));
        var localDelta = simulationDelta.Rotated(-Mathf.DegToRad(draft.RotationDegrees)) / modelScale;
        _maskDraft.X += localDelta.X;
        _maskDraft.Y += localDelta.Y;
        UpdateMaskControls();
        ApplyPreviewRendering();
    }

    private void ResizeMask(float factor)
    {
        if (!_maskOverrideEnabled)
            return;
        var oldWidth = _maskDraft.Width;
        var oldHeight = _maskDraft.Height;
        _maskDraft.Width = Math.Clamp(oldWidth * factor, 1f, 16000f);
        _maskDraft.Height = Math.Clamp(oldHeight * factor, 1f, 16000f);
        _maskDraft.X += (oldWidth - _maskDraft.Width) * 0.5f;
        _maskDraft.Y += (oldHeight - _maskDraft.Height) * 0.5f;
        _maskDraft.CornerRadius = Math.Min(
            _maskDraft.CornerRadius,
            Math.Min(_maskDraft.Width, _maskDraft.Height) * 0.5f);
        UpdateMaskControls();
        ApplyPreviewRendering();
    }

    private void UpdateMaskControls()
    {
        _updatingControls = true;
        var values = Enum.GetValues<Live2DMaskType>();
        _maskTypeInput.Selected = Math.Max(0, Array.IndexOf(values, _maskDraft.Type));
        foreach (var refresh in _maskControlRefreshers)
            refresh(_maskDraft);
        _updatingControls = false;
    }

    private void SetMaskInputsEnabled(bool enabled)
        => SetControlsEnabled(_maskInputs, enabled);

    private static void SetControlsEnabled(IEnumerable<Control> controls, bool enabled)
    {
        foreach (var control in controls)
            SetControlEnabled(control, enabled);
    }

    private static void SetControlEnabled(Control? control, bool enabled)
    {
        if (control is SpinBox spinBox)
            spinBox.Editable = enabled;
        else if (control is Slider slider)
            slider.Editable = enabled;
        else if (control is BaseButton button)
            button.Disabled = !enabled;
    }

    private void ApplyPreviewRendering()
    {
        var mask = ToMaskSettings(_maskOverrideEnabled ? _maskDraft : _globalMask);
        if (_preview != null && GodotObject.IsInstanceValid(_preview.Root))
            _preview.Apply(new Live2DModelUpdate
            {
                MaskViewportSize = _maskViewportOverrideEnabled
                    ? _maskViewportDraft
                    : _globalMaskViewportSize,
                BlendMode = _blendOverrideEnabled ? _blendDraft : _globalBlendMode,
                Filter = ToFilterSettings(_filterOverrideEnabled ? _filterDraft : _globalFilter),
                Mask = mask,
            });
        if (_maskOutline == null || !GodotObject.IsInstanceValid(_maskOutline))
            return;
        _maskOutline.Points = Live2DRenderPipeline.BuildMaskPolygon(mask);
        _maskOutline.Visible = mask.Type != Live2DMaskType.None;
        var modelScale = Live2DLayout.ResolveModelScale(CurrentDraft.Scale, _previewViewportSize);
        _maskOutline.Width = 2.5f / Math.Max(0.001f, modelScale * _canvas.CanvasScale);
    }

    private void UpdateControls()
    {
        if (_sceneChoice == null)
            return;
        _updatingControls = true;
        var draft = CurrentDraft;
        _sceneChoice.Selected = (int)_currentScene;
        _offsetX.Value = draft.OffsetX;
        _offsetY.Value = draft.OffsetY;
        _scale.Value = draft.Scale;
        _rotation.Value = draft.RotationDegrees;
        _scaleInput.Value = draft.Scale;
        _rotationInput.Value = draft.RotationDegrees;
        _updatingControls = false;
    }

    private void Save()
    {
        var store = RitsuLibFramework.GetDataStore(Entry.ModId);
        store.Modify<Live2DSettings>(Live2DConfigStore.SettingsKey, settings =>
        {
            var model = settings.Models.FirstOrDefault(value => value.Id == _model.Id);
            if (model == null)
                return;
            foreach (var (scene, draft) in _drafts)
            {
                var target = scene switch
                {
                    Live2DSceneKind.MainMenu => model.Overrides.MainMenu,
                    Live2DSceneKind.InGame => model.Overrides.InGame,
                    _ => throw new ArgumentOutOfRangeException(nameof(scene), scene, null),
                };
                if (draft.PositionDirty)
                {
                    target.OffsetX = draft.OffsetX;
                    target.OffsetY = draft.OffsetY;
                }
                if (draft.ScaleDirty) target.Scale = draft.Scale;
                if (draft.RotationDirty) target.RotationDegrees = draft.RotationDegrees;
            }
            model.Overrides.Rendering.MaskViewportSize = _maskViewportOverrideEnabled
                ? _maskViewportDraft
                : null;
            model.Overrides.Rendering.BlendMode = _blendOverrideEnabled ? _blendDraft : null;
            model.Overrides.Rendering.Filter = _filterOverrideEnabled ? CloneFilter(_filterDraft) : null;
            model.Overrides.Rendering.Mask = _maskOverrideEnabled ? CloneMask(_maskDraft) : null;
        });
        store.Save(Live2DConfigStore.SettingsKey);
        Live2DRuntimeManager.RefreshAll();
        _uiHost.RequestRefresh();
        Entry.Logger.Info($"[{Entry.ModId}] Saved preview transforms and rendering for model {_model.Id}.");
        Close();
    }

    private void Close()
    {
        if (GodotObject.IsInstanceValid(this))
            QueueFree();
    }

    private static SpinBox CreateSpinBox(double min, double max, double step, Action<double> changed)
    {
        var input = new SpinBox
        {
            MinValue = min,
            MaxValue = max,
            Step = step,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        input.ValueChanged += value => changed(value);
        return input;
    }

    private static (HSlider Slider, SpinBox Input) CreateSlider(
        double min,
        double max,
        double step,
        Action<double> changed,
        string suffix = "")
    {
        var slider = new HSlider
        {
            MinValue = min,
            MaxValue = max,
            Step = step,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        var input = new SpinBox
        {
            MinValue = min,
            MaxValue = max,
            Step = step,
            Suffix = suffix,
            CustomMinimumSize = new Vector2(105f, 0f),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd,
        };
        slider.ValueChanged += value => changed(value);
        input.ValueChanged += value => changed(value);
        return (slider, input);
    }

    private static Control CreateSliderRow(HSlider slider, SpinBox input)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row.AddChild(slider);
        row.AddChild(input);
        return row;
    }

    private static Control CreateField(string label, Control input)
    {
        var box = new VBoxContainer();
        box.AddChild(new Label { Text = label, Modulate = new Color(0.82f, 0.84f, 0.88f) });
        box.AddChild(input);
        return box;
    }

    private static Label CreateSectionTitle(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 20);
        label.AddThemeColorOverride("font_color", new Color(0.96f, 0.82f, 0.45f));
        return label;
    }

    private static string MaskTypeName(Live2DMaskType type) => type switch
    {
        Live2DMaskType.None => L("mask.none", "None"),
        Live2DMaskType.Rectangle => L("mask.rectangle", "Rectangle"),
        Live2DMaskType.Ellipse => L("mask.ellipse", "Ellipse"),
        Live2DMaskType.RoundedRectangle => L("mask.rounded_rectangle", "Rounded Rectangle"),
        _ => type.ToString(),
    };

    private static string BlendModeName(Live2DBlendMode mode) => mode switch
    {
        Live2DBlendMode.Normal => L("blend.normal", "Normal"),
        Live2DBlendMode.Add => L("blend.add", "Add"),
        Live2DBlendMode.Subtract => L("blend.subtract", "Subtract"),
        Live2DBlendMode.Multiply => L("blend.multiply", "Multiply"),
        Live2DBlendMode.PremultipliedAlpha => L("blend.premultiplied_alpha", "Premultiplied Alpha"),
        _ => mode.ToString(),
    };

    private static FilterConfig CloneFilter(FilterConfig filter) => new()
    {
        TintR = filter.TintR,
        TintG = filter.TintG,
        TintB = filter.TintB,
        TintA = filter.TintA,
        Brightness = filter.Brightness,
        Contrast = filter.Contrast,
        Saturation = filter.Saturation,
        Grayscale = filter.Grayscale,
        HueShiftDegrees = filter.HueShiftDegrees,
        Invert = filter.Invert,
        Gamma = filter.Gamma,
    };

    private static Live2DFilterSettings ToFilterSettings(FilterConfig filter) => new()
    {
        Tint = new Color(filter.TintR, filter.TintG, filter.TintB, filter.TintA),
        Brightness = Math.Clamp(filter.Brightness, -1f, 1f),
        Contrast = Math.Clamp(filter.Contrast, 0f, 4f),
        Saturation = Math.Clamp(filter.Saturation, 0f, 4f),
        Grayscale = Math.Clamp(filter.Grayscale, 0f, 1f),
        HueShiftDegrees = filter.HueShiftDegrees,
        Invert = Math.Clamp(filter.Invert, 0f, 1f),
        Gamma = Math.Clamp(filter.Gamma, 0.01f, 10f),
    };

    private static CanvasMaskConfig CloneMask(CanvasMaskConfig mask) => new()
    {
        Type = mask.Type,
        X = mask.X,
        Y = mask.Y,
        Width = mask.Width,
        Height = mask.Height,
        CornerRadius = mask.CornerRadius,
        SegmentsPerCorner = mask.SegmentsPerCorner,
    };

    private static Live2DMaskSettings ToMaskSettings(CanvasMaskConfig mask) => new()
    {
        Type = Enum.IsDefined(mask.Type) ? mask.Type : Live2DMaskType.None,
        Rect = new Rect2(mask.X, mask.Y, Math.Max(1f, mask.Width), Math.Max(1f, mask.Height)),
        CornerRadius = Math.Max(0f, mask.CornerRadius),
        SegmentsPerCorner = Math.Clamp(mask.SegmentsPerCorner, 2, 64),
    };

    private static StyleBoxFlat CreatePanelStyle(Color color) => new()
    {
        BgColor = color,
        BorderColor = new Color(0.35f, 0.4f, 0.48f, 0.75f),
        BorderWidthLeft = 1,
        BorderWidthTop = 1,
        BorderWidthRight = 1,
        BorderWidthBottom = 1,
        CornerRadiusTopLeft = 8,
        CornerRadiusTopRight = 8,
        CornerRadiusBottomLeft = 8,
        CornerRadiusBottomRight = 8,
    };

    private static string L(string key, string fallback) => Live2DLocalization.Get(key, fallback);
    private static string F(string key, string fallback, params object?[] args)
        => Live2DLocalization.Format(key, fallback, args);

    private sealed class PreviewDraft
    {
        public AnchorPreset Anchor { get; set; }
        public float OffsetX { get; set; }
        public float OffsetY { get; set; }
        public float Scale { get; set; }
        public float RotationDegrees { get; set; }
        public bool PositionDirty { get; set; }
        public bool ScaleDirty { get; set; }
        public bool RotationDirty { get; set; }

        public static PreviewDraft From(SceneDisplayConfig config) => new()
        {
            Anchor = config.Anchor,
            OffsetX = config.OffsetX,
            OffsetY = config.OffsetY,
            Scale = config.Scale,
            RotationDegrees = config.RotationDegrees,
        };
    }

}
