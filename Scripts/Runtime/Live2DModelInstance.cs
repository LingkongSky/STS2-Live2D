using Godot;
using Live2D.Api;
using Live2D.Scripts.Configuration;
using Live2D.Scripts.Models;

namespace Live2D.Scripts.Runtime;

internal sealed class Live2DModelInstance
{
    private readonly GDCubismUserModelCS _userModel;
    private readonly Live2DModelConfig _model;
    private readonly Live2DRuntimeModelIdentity _identity;
    private readonly Polygon2D _maskNode;
    private readonly CanvasGroup _filterGroup;
    private Live2DPlaybackController? _playbackController;
    private ShaderMaterial? _renderMaterial;
    private Live2DBlendMode _blendMode = Live2DBlendMode.Normal;
    private Live2DFilterSettings _filter = Live2DFilterSettings.Default;
    private Live2DMaskSettings _mask = Live2DMaskSettings.None;
    private string? _activeMotionGroup;
    private int _activeMotionIndex = -1;
    private bool _motionLooping;
    private string _activeExpression = "";

    public Node2D Root { get; }
    public string ModelId => _identity.RuntimeId;
    internal Live2DRuntimeModelIdentity Identity => _identity;
    public bool IsAlive => GodotObject.IsInstanceValid(Root) && Root.IsInsideTree();
    public bool IsUsable => IsAlive && Root.IsVisibleInTree();

    internal event Action? MotionCompleted;
    internal event Action<string>? MotionEventReceived;

    private Live2DModelInstance(
        Node2D root,
        Polygon2D maskNode,
        CanvasGroup filterGroup,
        GDCubismUserModelCS userModel,
        Live2DRuntimeModelDefinition definition)
    {
        Root = root;
        _maskNode = maskNode;
        _filterGroup = filterGroup;
        _userModel = userModel;
        _model = definition.Config;
        _identity = definition.Identity;
        _userModel.MotionFinished += OnNativeMotionFinished;
        _userModel.MotionEvent += OnNativeMotionEvent;
    }

    internal static Live2DModelInstance Create(
        Live2DRuntimeModelDefinition definition,
        ResolvedLive2DConfig resolved,
        SceneDisplayConfig sceneConfig,
        Vector2 viewportSize)
    {
        var root = new Node2D
        {
            Name = $"Model_{definition.Identity.RuntimeId}",
            ProcessMode = Node.ProcessModeEnum.Always,
            Position = Live2DLayout.ResolvePosition(
                viewportSize,
                sceneConfig.Anchor,
                sceneConfig.OffsetX,
                sceneConfig.OffsetY),
            Scale = Vector2.One * Live2DLayout.ResolveModelScale(sceneConfig.Scale, viewportSize),
            RotationDegrees = sceneConfig.RotationDegrees,
            ZIndex = sceneConfig.Layer,
            Visible = sceneConfig.Visible,
            Modulate = new Color(1f, 1f, 1f, Math.Clamp(sceneConfig.Opacity, 0f, 1f)),
        };

        var maskNode = new Polygon2D
        {
            Name = "Live2DCanvasMask",
            ProcessMode = Node.ProcessModeEnum.Always,
            Color = Colors.White,
            ClipChildren = CanvasItem.ClipChildrenMode.Disabled,
        };
        root.AddChild(maskNode);

        var filterGroup = new CanvasGroup
        {
            Name = "Live2DFilterGroup",
            ProcessMode = Node.ProcessModeEnum.Always,
            FitMargin = 0f,
            ClearMargin = 10f,
            UseMipmaps = false,
        };
        maskNode.AddChild(filterGroup);

        var userModel = new GDCubismUserModelCS();
        var userModelNode = userModel.GetInternalObject();
        userModelNode.Name = "GDCubismUserModel";
        userModelNode.ProcessMode = Node.ProcessModeEnum.Always;
        filterGroup.AddChild(userModelNode);

        userModel.LoadExpressions = true;
        userModel.LoadMotions = true;
        userModel.PlaybackProcessMode = GDCubismUserModelCS.MotionProcessCallbackEnum.Idle;
        userModel.SpeedScale = Math.Max(0f, resolved.Playback.Speed);
        userModel.PhysicsEvaluate = resolved.Playback.EnablePhysics;
        userModel.PoseUpdate = resolved.Playback.EnablePose;
        userModel.MaskViewportSize = Math.Max(0, resolved.Rendering.MaskViewportSize);
        AddEffectNode(userModelNode, "GDCubismEffectBreath");
        AddEffectNode(userModelNode, "GDCubismEffectEyeBlink");
        userModel.Assets = definition.AssetPath;

        var result = new Live2DModelInstance(root, maskNode, filterGroup, userModel, definition);
        var playbackController = new Live2DPlaybackController
        {
            Name = "Live2DPlaybackController",
            ProcessMode = Node.ProcessModeEnum.Always,
        };
        playbackController.Configure(result, resolved.Playback.AutoPlayIdle);
        result._playbackController = playbackController;
        root.AddChild(playbackController);
        result.Apply(new Live2DModelUpdate
        {
            BlendMode = resolved.Rendering.BlendMode,
            Filter = resolved.Rendering.Filter,
            Mask = resolved.Rendering.Mask,
        });
        return result;
    }

    public void Play(Live2DActionDescriptor action, bool loop = false)
    {
        try
        {
            if (action.Kind == Live2DActionKind.Expression)
            {
                if (_activeExpression == action.ExpressionId)
                {
                    _userModel.StopExpression();
                    _activeExpression = "";
                }
                else
                {
                    if (_activeExpression.Length > 0)
                        _userModel.StopExpression();
                    _userModel.StartExpression(action.ExpressionId);
                    _activeExpression = action.ExpressionId;
                }
                return;
            }

            PlayMotion(action.MotionGroup, action.MotionIndex, loop);
        }
        catch (Exception ex)
        {
            _playbackController?.RequestIdle();
            Entry.Logger.Warn($"[{Entry.ModId}] Failed to play action for model {_model.Id}: {ex.Message}");
        }
    }

    internal Live2DModelSnapshot CaptureSnapshot(Live2DScene scene) => new(
        ModelId,
        scene,
        IsAlive,
        Root.Position,
        Root.Scale,
        Root.RotationDegrees,
        Root.Modulate.A,
        Root.Visible,
        Root.ZIndex,
        _userModel.SpeedScale,
        _userModel.PhysicsEvaluate,
        _userModel.PoseUpdate,
        _userModel.MaskViewportSize,
        _blendMode,
        _filter,
        _mask,
        new Live2DPlaybackSnapshot(
            _activeMotionGroup,
            _activeMotionIndex,
            _motionLooping,
            _activeExpression.Length == 0 ? null : _activeExpression));

    internal void Apply(Live2DModelUpdate update)
    {
        update.Validate();
        if (update.Position is { } position)
            Root.Position = position;
        if (update.Scale is { } scale)
            Root.Scale = scale;
        if (update.RotationDegrees is { } rotation)
            Root.RotationDegrees = rotation;
        if (update.Opacity is { } opacity)
        {
            var modulate = Root.Modulate;
            modulate.A = Math.Clamp(opacity, 0f, 1f);
            Root.Modulate = modulate;
        }
        if (update.Visible is { } visible)
            Root.Visible = visible;
        if (update.Layer is { } layer)
            Root.ZIndex = layer;
        if (update.PlaybackSpeed is { } speed)
            _userModel.SpeedScale = Math.Max(0f, speed);
        if (update.PhysicsEnabled is { } physicsEnabled)
            _userModel.PhysicsEvaluate = physicsEnabled;
        if (update.PoseEnabled is { } poseEnabled)
            _userModel.PoseUpdate = poseEnabled;
        if (update.MaskViewportSize is { } maskViewportSize)
            _userModel.MaskViewportSize = Math.Max(0, maskViewportSize);
        if (update.BlendMode is { } blendMode)
            _blendMode = blendMode;
        if (update.Filter is { } filter)
            _filter = filter;
        if (update.Mask is { } mask)
            _mask = mask;
        if (update.BlendMode is not null || update.Filter is not null)
            ApplyFilter();
        if (update.Mask is not null)
            ApplyMask();
    }

    private void ApplyFilter()
    {
        if (_blendMode == Live2DBlendMode.Normal && _filter.IsNeutral)
        {
            _filterGroup.Material = null;
            return;
        }

        _renderMaterial ??= Live2DRenderPipeline.CreateMaterial(_blendMode);
        Live2DRenderPipeline.UpdateMaterial(_renderMaterial, _blendMode, _filter);
        _filterGroup.Material = _renderMaterial;
    }

    private void ApplyMask()
    {
        if (_mask.Type == Live2DMaskType.None)
        {
            _maskNode.ClipChildren = CanvasItem.ClipChildrenMode.Disabled;
            _maskNode.Polygon = [];
            return;
        }

        _maskNode.Polygon = Live2DRenderPipeline.BuildMaskPolygon(_mask);
        _maskNode.ClipChildren = CanvasItem.ClipChildrenMode.Only;
    }

    internal void PlayMotion(string group, int index, bool loop)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        _playbackController?.BeforeMotion(loop);
        if (loop)
            _userModel.StartMotionLoop(
                group,
                index,
                GDCubismUserModelCS.PriorityEnum.PriorityForce,
                true,
                true);
        else
            _userModel.StartMotion(group, index, GDCubismUserModelCS.PriorityEnum.PriorityForce);
        _activeMotionGroup = group;
        _activeMotionIndex = index;
        _motionLooping = loop;
    }

    internal void StopMotion()
    {
        _userModel.StopMotion();
        ClearActiveMotion();
        _playbackController?.RequestIdle();
    }

    internal void SetExpression(string expressionId)
    {
        if (_activeExpression.Length > 0)
            _userModel.StopExpression();
        _userModel.StartExpression(expressionId);
        _activeExpression = expressionId;
    }

    internal void ClearExpression()
    {
        if (_activeExpression.Length == 0)
            return;
        _userModel.StopExpression();
        _activeExpression = "";
    }

    internal IReadOnlyList<Live2DParameterInfo> GetParameters()
        => _userModel.GetParameters().Select(parameter => new Live2DParameterInfo(
            parameter.Id,
            parameter.Value,
            parameter.DefaultValue,
            parameter.MinimumValue,
            parameter.MaximumValue)).ToArray();

    internal bool TryGetParameter(string parameterId, out Live2DParameterInfo parameter)
    {
        var controller = FindCubismValue(_userModel.GetParameters(), parameterId);
        if (controller is null)
        {
            parameter = null!;
            return false;
        }
        parameter = new Live2DParameterInfo(
            controller.Id,
            controller.Value,
            controller.DefaultValue,
            controller.MinimumValue,
            controller.MaximumValue);
        return true;
    }

    internal void SetParameter(string parameterId, float value)
    {
        EnsureFinite(value, nameof(value));
        var parameter = RequireCubismValue(
            _userModel.GetParameters(),
            parameterId,
            "parameter");
        parameter.Value = Math.Clamp(value, parameter.MinimumValue, parameter.MaximumValue);
    }

    internal void SetParameters(IReadOnlyDictionary<string, float> values)
        => SetCubismValues(
            values,
            _userModel.GetParameters(),
            "parameter",
            static (parameter, value) => Math.Clamp(
                value,
                parameter.MinimumValue,
                parameter.MaximumValue));

    internal IReadOnlyList<Live2DPartInfo> GetParts()
        => _userModel.GetPartOpacities()
            .Select(part => new Live2DPartInfo(part.Id, part.Value))
            .ToArray();

    internal bool TryGetPart(string partId, out Live2DPartInfo part)
    {
        var controller = FindCubismValue(_userModel.GetPartOpacities(), partId);
        if (controller is null)
        {
            part = null!;
            return false;
        }
        part = new Live2DPartInfo(controller.Id, controller.Value);
        return true;
    }

    internal void SetPartOpacity(string partId, float opacity)
    {
        EnsureFinite(opacity, nameof(opacity));
        var part = RequireCubismValue(
            _userModel.GetPartOpacities(),
            partId,
            "part");
        part.Value = Math.Clamp(opacity, 0f, 1f);
    }

    internal void SetPartOpacities(IReadOnlyDictionary<string, float> values)
        => SetCubismValues(
            values,
            _userModel.GetPartOpacities(),
            "part",
            static (_, value) => Math.Clamp(value, 0f, 1f));

    private static T? FindCubismValue<T>(IEnumerable<T> values, string id)
        where T : GDCubismValueAbsCS
        => values.FirstOrDefault(value =>
            string.Equals(value.Id, id, StringComparison.OrdinalIgnoreCase));

    private static T RequireCubismValue<T>(IEnumerable<T> values, string id, string kind)
        where T : GDCubismValueAbsCS
        => FindCubismValue(values, id)
           ?? throw new KeyNotFoundException($"Cubism {kind} does not exist: {id}");

    private static void SetCubismValues<T>(
        IReadOnlyDictionary<string, float> values,
        IEnumerable<T> controllers,
        string kind,
        Func<T, float, float> normalize)
        where T : GDCubismValueAbsCS
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
            return;

        var byId = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        foreach (var controller in controllers)
            byId[controller.Id] = controller;

        var resolved = new (T Controller, float Value)[values.Count];
        var index = 0;
        foreach (var pair in values)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pair.Key);
            EnsureFinite(pair.Value, nameof(values));
            if (!byId.TryGetValue(pair.Key, out var controller))
                throw new KeyNotFoundException($"Cubism {kind} does not exist: {pair.Key}");
            resolved[index++] = (controller, normalize(controller, pair.Value));
        }

        foreach (var pair in resolved)
            pair.Controller.Value = pair.Value;
    }

    internal IdleStartResult TryStartIdle()
    {
        try
        {
            var motions = _userModel.GetMotions();
            if (motions.Count == 0)
                return IdleStartResult.Retry;

            foreach (var groupName in motions.Keys)
            {
                if (!string.Equals(groupName, "Idle", StringComparison.OrdinalIgnoreCase)
                    || !motions.TryGetValue(groupName, out var count)
                    || count <= 0)
                    continue;

                _userModel.StartMotionLoop(
                    groupName,
                    0,
                    GDCubismUserModelCS.PriorityEnum.PriorityIdle,
                    true,
                    true);
                _activeMotionGroup = groupName;
                _activeMotionIndex = 0;
                _motionLooping = true;
                return IdleStartResult.Started;
            }

            return IdleStartResult.NoIdleMotion;
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"[{Entry.ModId}] Unable to start idle motion for model {_model.Id}: {ex.Message}");
            return IdleStartResult.Retry;
        }
    }

    internal bool HasQueuedMotion()
    {
        try
        {
            return _userModel.GetCubismMotionQueueEntries().Count > 0;
        }
        catch
        {
            return false;
        }
    }

    internal void ConnectMotionFinished(Action handler) => _userModel.MotionFinished += handler;

    internal void DisconnectMotionFinished(Action handler) => _userModel.MotionFinished -= handler;

    private void OnNativeMotionFinished()
    {
        ClearActiveMotion();
        MotionCompleted?.Invoke();
    }

    private void OnNativeMotionEvent(string value) => MotionEventReceived?.Invoke(value);

    private void ClearActiveMotion()
    {
        _activeMotionGroup = null;
        _activeMotionIndex = -1;
        _motionLooping = false;
    }

    private static void EnsureFinite(Vector2 value, string parameterName)
    {
        EnsureFinite(value.X, parameterName);
        EnsureFinite(value.Y, parameterName);
    }

    private static void EnsureFinite(float value, string parameterName)
    {
        if (!float.IsFinite(value))
            throw new ArgumentOutOfRangeException(parameterName, "Value must be finite.");
    }

    private static void AddEffectNode(Node2D userModelNode, string nativeClassName)
    {
        try
        {
            var effect = ClassDB.Instantiate(nativeClassName).As<Node>();
            if (effect == null)
            {
                Entry.Logger.Warn($"[{Entry.ModId}] Unable to instantiate {nativeClassName}.");
                return;
            }

            effect.Name = nativeClassName;
            effect.ProcessMode = Node.ProcessModeEnum.Always;
            userModelNode.AddChild(effect);
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"[{Entry.ModId}] Unable to attach {nativeClassName}: {ex.Message}");
        }
    }
}
