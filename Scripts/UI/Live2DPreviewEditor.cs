using Godot;
using Live2D.Scripts.Configuration;
using Live2D.Scripts.Runtime;
using STS2RitsuLib;
using STS2RitsuLib.Settings;

namespace Live2D.Scripts.UI;

public sealed partial class Live2DPreviewEditor : CanvasLayer
{
    private const string OverlayNodeName = "Live2DPreviewEditor";
    private readonly Dictionary<Live2DSceneKind, PreviewDraft> _drafts = new();
    private IModSettingsUiActionHost _uiHost = null!;
    private Live2DModelConfig _model = null!;
    private ResolvedLive2DConfig _resolved = null!;
    private Live2DPreviewCanvas _canvas = null!;
    private Live2DModelInstance? _preview;
    private OptionButton _sceneChoice = null!;
    private OptionButton _resolutionChoice = null!;
    private SpinBox _offsetX = null!;
    private SpinBox _offsetY = null!;
    private HSlider _scale = null!;
    private HSlider _rotation = null!;
    private SpinBox _scaleInput = null!;
    private SpinBox _rotationInput = null!;
    private Live2DSceneKind _currentScene;
    private Vector2 _previewViewportSize = Live2DLayout.ReferenceViewportSize;
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
        _canvas.Resized += ApplyPreviewTransform;
        previewPanel.AddChild(_canvas);
        content.AddChild(previewPanel);

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
        var inspector = new VBoxContainer();
        inspector.AddThemeConstantOverride("separation", 12);
        inspectorMargin.AddChild(inspector);
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
        _preview = Live2DModelInstance.Create(_model, _resolved, config, _previewViewportSize);
        _canvas.AddPreview(_preview.Root);
        Entry.Logger.Info(
            $"[{Entry.ModId}] Opened preview for model {_model.Id} in {_currentScene} " +
            $"({_previewViewportSize.X:0}x{_previewViewportSize.Y:0}).");
        UpdateControls();
        ApplyPreviewTransform();
    }

    private void ApplyPreviewTransform()
    {
        if (_preview == null || !GodotObject.IsInstanceValid(_preview.Root))
            return;
        var draft = CurrentDraft;
        _preview.Root.Position = Live2DLayout.ResolvePosition(
            _previewViewportSize,
            draft.Anchor,
            draft.OffsetX,
            draft.OffsetY);
        _preview.Root.Scale = Vector2.One * Live2DLayout.ResolveModelScale(draft.Scale, _previewViewportSize);
        _preview.Root.RotationDegrees = draft.RotationDegrees;
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
        });
        store.Save(Live2DConfigStore.SettingsKey);
        Live2DRuntimeManager.RefreshAll();
        _uiHost.RequestRefresh();
        Entry.Logger.Info($"[{Entry.ModId}] Saved preview transforms for model {_model.Id}.");
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

public sealed partial class Live2DPreviewCanvas : Control
{
    private bool _dragging;
    private Node2D _stage = null!;
    private Vector2 _simulationSize = Live2DLayout.ReferenceViewportSize;
    private float _canvasScale = 1f;
    private Rect2 _displayRect;
    public event Action<Vector2>? Dragged;
    public event Action<float>? ScaleRequested;
    public event Action<float>? RotateRequested;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.Drag;
        _stage = new Node2D { Name = "SimulatedViewport" };
        AddChild(_stage);
        UpdateStageTransform();
        QueueRedraw();
    }

    public void SetSimulationSize(Vector2 size)
    {
        if (size.X <= 0f || size.Y <= 0f)
            return;
        _simulationSize = size;
        UpdateStageTransform();
        QueueRedraw();
    }

    public void AddPreview(Node2D preview)
    {
        if (_stage == null || !GodotObject.IsInstanceValid(_stage))
        {
            _stage = new Node2D { Name = "SimulatedViewport" };
            AddChild(_stage);
            UpdateStageTransform();
        }
        _stage.AddChild(preview);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
        {
            UpdateStageTransform();
            QueueRedraw();
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } button:
                _dragging = button.Pressed;
                MouseDefaultCursorShape = _dragging ? CursorShape.CanDrop : CursorShape.Drag;
                AcceptEvent();
                break;
            case InputEventMouseMotion motion when _dragging:
                Dragged?.Invoke(motion.Relative / Math.Max(0.001f, _canvasScale));
                AcceptEvent();
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp } wheelUp:
                if (wheelUp.ShiftPressed) RotateRequested?.Invoke(2f);
                else ScaleRequested?.Invoke(0.05f);
                AcceptEvent();
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelDown } wheelDown:
                if (wheelDown.ShiftPressed) RotateRequested?.Invoke(-2f);
                else ScaleRequested?.Invoke(-0.05f);
                AcceptEvent();
                break;
        }
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.012f, 0.018f, 0.03f));
        DrawRect(_displayRect, new Color(0.035f, 0.055f, 0.08f));
        var grid = 96f * _canvasScale;
        var minor = new Color(0.3f, 0.38f, 0.48f, 0.16f);
        for (var x = _displayRect.Position.X + grid; x < _displayRect.End.X; x += grid)
            DrawLine(new Vector2(x, _displayRect.Position.Y), new Vector2(x, _displayRect.End.Y), minor, 1f);
        for (var y = _displayRect.Position.Y + grid; y < _displayRect.End.Y; y += grid)
            DrawLine(new Vector2(_displayRect.Position.X, y), new Vector2(_displayRect.End.X, y), minor, 1f);
        var center = _displayRect.GetCenter();
        DrawLine(new Vector2(center.X, _displayRect.Position.Y), new Vector2(center.X, _displayRect.End.Y),
            new Color(0.96f, 0.82f, 0.45f, 0.28f), 1f);
        DrawLine(new Vector2(_displayRect.Position.X, center.Y), new Vector2(_displayRect.End.X, center.Y),
            new Color(0.96f, 0.82f, 0.45f, 0.28f), 1f);
        DrawRect(_displayRect, new Color(0.45f, 0.54f, 0.66f, 0.8f), false, 1f);
    }

    private void UpdateStageTransform()
    {
        if (Size.X <= 0f || Size.Y <= 0f || _simulationSize.X <= 0f || _simulationSize.Y <= 0f)
            return;
        _canvasScale = Math.Max(0.001f, Math.Min(Size.X / _simulationSize.X, Size.Y / _simulationSize.Y));
        var displaySize = _simulationSize * _canvasScale;
        _displayRect = new Rect2((Size - displaySize) * 0.5f, displaySize);
        if (_stage != null && GodotObject.IsInstanceValid(_stage))
        {
            _stage.Position = _displayRect.Position;
            _stage.Scale = Vector2.One * _canvasScale;
        }
    }
}
