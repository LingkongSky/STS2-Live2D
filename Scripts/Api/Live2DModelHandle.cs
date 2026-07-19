using Godot;
using Live2D.Scripts.Configuration;
using Live2D.Scripts.Runtime;
using MegaCrit.Sts2.Core.Nodes;

namespace Live2D.Api;

internal sealed class Live2DModelHandle : ILive2DModelHandle
{
    private readonly Live2DRuntimeModelIdentity _identity;
    private readonly Live2DModelUpdate _runtimeOverrides = new();
    private readonly Live2DQueuedUpdateAccumulator _queuedUpdates;
    private readonly Live2DQueuedValueAccumulator _queuedParameters;
    private readonly Live2DQueuedValueAccumulator _queuedPartOpacities;
    private readonly Live2DAvailabilityState<ILive2DModelHandle> _availability;
    private IReadOnlyList<Live2DActionInfo> _actions = Array.Empty<Live2DActionInfo>();
    private Live2DModelInstance? _instance;
    private Live2DModelSnapshot _snapshot;

    public Live2DModelHandle(Live2DRuntimeModelDefinition definition)
    {
        _identity = definition.Identity;
        _availability = new Live2DAvailabilityState<ILive2DModelHandle>(this);
        _queuedUpdates = new Live2DQueuedUpdateAccumulator(Apply);
        _queuedParameters = new Live2DQueuedValueAccumulator(values =>
            ApplyQueuedValues(values, static (instance, batch) => instance.SetParameters(batch)));
        _queuedPartOpacities = new Live2DQueuedValueAccumulator(values =>
            ApplyQueuedValues(values, static (instance, batch) => instance.SetPartOpacities(batch)));
        UpdateDefinition(definition);
        _snapshot = UnavailableSnapshot();
    }

    public string ModelId => _identity.RuntimeId;
    public string OwnerModId => _identity.OwnerModId;
    public string? PackId => _identity.PackId;
    public string ModelKey => _identity.ModelKey;
    public string InstanceId => _identity.InstanceId;
    public Live2DScene Scene => _identity.Scene;
    public bool IsAvailable => _availability.IsAvailable;
    public IReadOnlyList<Live2DActionInfo> Actions => _actions;
    public Live2DModelSnapshot Snapshot
    {
        get
        {
            Live2DApi.EnsureMainThread();
            if (_instance?.IsAlive == true)
                _snapshot = _instance.CaptureSnapshot(Scene);
            return _snapshot with { IsAvailable = IsAvailable };
        }
    }

    public event Action<ILive2DModelHandle>? BecameAvailable;
    public event Action<ILive2DModelHandle>? BecameUnavailable;
    public event Action<ILive2DModelHandle>? MotionFinished;
    public event Action<ILive2DModelHandle, string>? MotionEvent;

    internal void Bind(Live2DModelInstance instance)
    {
        Live2DApi.EnsureMainThread();
        DetachEvents();
        _instance = instance;
        _instance.MotionCompleted += OnMotionFinished;
        _instance.MotionEventReceived += OnMotionEvent;
        if (!_runtimeOverrides.IsEmpty)
            _instance.Apply(_runtimeOverrides);
        _snapshot = _instance.CaptureSnapshot(Scene);
        _availability.Set(true);
        BecameAvailable?.Invoke(this);
    }

    internal void EnsureIdentity(Live2DRuntimeModelIdentity identity)
    {
        if (!string.Equals(_identity.RuntimeId, identity.RuntimeId, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(_identity.OwnerModId, identity.OwnerModId, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(_identity.PackId, identity.PackId, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(_identity.ModelKey, identity.ModelKey, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(_identity.InstanceId, identity.InstanceId, StringComparison.OrdinalIgnoreCase) ||
            _identity.Scene != identity.Scene)
            throw new InvalidOperationException(
                $"Live2D runtime identity collision for '{identity.RuntimeId}'.");
    }

    internal void UpdateDefinition(Live2DRuntimeModelDefinition definition)
    {
        EnsureIdentity(definition.Identity);
        _actions = Array.AsReadOnly(definition.Config.AvailableActions
            .Select(action => new Live2DActionInfo(
                action.Kind == Live2DActionKind.Expression
                    ? Live2DActionType.Expression
                    : Live2DActionType.Motion,
                action.DisplayName,
                action.MotionGroup,
                action.MotionIndex,
                action.ExpressionId))
            .ToArray());
    }

    internal void Unbind()
    {
        Live2DApi.EnsureMainThread();
        var wasAvailable = IsAvailable;
        if (_instance?.IsAlive == true)
            _snapshot = _instance.CaptureSnapshot(Scene) with { IsAvailable = false };
        DetachEvents();
        _instance = null;
        _availability.Set(false);
        if (wasAvailable)
            BecameUnavailable?.Invoke(this);
    }

    public Task<ILive2DModelHandle> WaitUntilAvailableAsync(
        CancellationToken cancellationToken = default)
        => _availability.WaitAsync(available: true, cancellationToken);

    public Task<ILive2DModelHandle> WaitUntilUnavailableAsync(
        CancellationToken cancellationToken = default)
        => _availability.WaitAsync(available: false, cancellationToken);

    public void Apply(Live2DModelUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);
        Live2DApi.EnsureMainThread();
        if (update.IsEmpty)
            return;
        update.Validate();
        _runtimeOverrides.MergeFrom(update);
        if (_instance?.IsAlive != true)
        {
            _snapshot = ApplyToSnapshot(_snapshot, update) with { IsAvailable = false };
            return;
        }
        _instance.Apply(update);
        _snapshot = _instance.CaptureSnapshot(Scene);
    }

    public void Update(Action<Live2DModelUpdate> configure)
        => Apply(CreateUpdate(configure));

    public void QueueUpdate(Live2DModelUpdate update)
        => _queuedUpdates.Queue(update);

    public void QueueUpdate(Action<Live2DModelUpdate> configure)
        => QueueUpdate(CreateUpdate(configure));

    public void SetPosition(Vector2 position) => Apply(new Live2DModelUpdate { Position = position });
    public void SetScale(Vector2 scale) => Apply(new Live2DModelUpdate { Scale = scale });
    public void SetUniformScale(float scale)
    {
        if (!float.IsFinite(scale) || scale <= 0f)
            throw new ArgumentOutOfRangeException(nameof(scale), "Uniform scale must be finite and positive.");
        SetScale(Vector2.One * scale);
    }
    public void SetRotation(float degrees) => Apply(new Live2DModelUpdate { RotationDegrees = degrees });
    public void SetOpacity(float opacity) => Apply(new Live2DModelUpdate { Opacity = opacity });
    public void SetVisible(bool visible) => Apply(new Live2DModelUpdate { Visible = visible });
    public void SetLayer(int layer) => Apply(new Live2DModelUpdate { Layer = layer });
    public void SetPlaybackSpeed(float speed) => Apply(new Live2DModelUpdate { PlaybackSpeed = speed });
    public void SetPhysicsEnabled(bool enabled) => Apply(new Live2DModelUpdate { PhysicsEnabled = enabled });
    public void SetPoseEnabled(bool enabled) => Apply(new Live2DModelUpdate { PoseEnabled = enabled });
    public void SetMaskViewportSize(int size) => Apply(new Live2DModelUpdate { MaskViewportSize = size });
    public void SetBlendMode(Live2DBlendMode mode) => Apply(new Live2DModelUpdate { BlendMode = mode });
    public void SetFilter(Live2DFilterSettings filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        Apply(new Live2DModelUpdate { Filter = filter });
    }
    public void ResetFilter() => SetFilter(Live2DFilterSettings.Default);
    public void SetMask(Live2DMaskSettings mask)
    {
        ArgumentNullException.ThrowIfNull(mask);
        Apply(new Live2DModelUpdate { Mask = mask });
    }
    public void ClearMask() => SetMask(Live2DMaskSettings.None);

    public void PlayAction(Live2DActionInfo action, bool loop = false)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (action.Type == Live2DActionType.Expression)
        {
            SetExpression(action.ExpressionId);
            return;
        }
        if (action.Type != Live2DActionType.Motion)
            throw new ArgumentOutOfRangeException(nameof(action), action.Type, "Unknown action type.");
        PlayMotion(action.MotionGroup, action.MotionIndex, loop);
    }

    public void PlayMotion(string group, int index, bool loop = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(group);
        Live2DApi.EnsureMainThread();
        RequireInstance().PlayMotion(group, index, loop);
    }

    public void StopMotion()
    {
        Live2DApi.EnsureMainThread();
        RequireInstance().StopMotion();
    }

    public void SetExpression(string expressionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expressionId);
        Live2DApi.EnsureMainThread();
        RequireInstance().SetExpression(expressionId);
    }

    public void ClearExpression()
    {
        Live2DApi.EnsureMainThread();
        RequireInstance().ClearExpression();
    }

    public IReadOnlyList<Live2DParameterInfo> GetParameters()
    {
        Live2DApi.EnsureMainThread();
        return RequireInstance().GetParameters();
    }

    public bool TryGetParameter(string parameterId, out Live2DParameterInfo parameter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parameterId);
        Live2DApi.EnsureMainThread();
        return RequireInstance().TryGetParameter(parameterId, out parameter!);
    }

    public void SetParameter(string parameterId, float value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parameterId);
        Live2DApi.EnsureMainThread();
        RequireInstance().SetParameter(parameterId, value);
    }

    public void SetParameters(IReadOnlyDictionary<string, float> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        Live2DApi.EnsureMainThread();
        RequireInstance().SetParameters(values);
    }

    public void QueueParameter(string parameterId, float value)
        => _queuedParameters.Queue(parameterId, value);

    public void QueueParameters(IReadOnlyDictionary<string, float> values)
        => _queuedParameters.Queue(values);

    public IReadOnlyList<Live2DPartInfo> GetParts()
    {
        Live2DApi.EnsureMainThread();
        return RequireInstance().GetParts();
    }

    public bool TryGetPart(string partId, out Live2DPartInfo part)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partId);
        Live2DApi.EnsureMainThread();
        return RequireInstance().TryGetPart(partId, out part!);
    }

    public void SetPartOpacity(string partId, float opacity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partId);
        Live2DApi.EnsureMainThread();
        RequireInstance().SetPartOpacity(partId, opacity);
    }

    public void SetPartOpacities(IReadOnlyDictionary<string, float> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        Live2DApi.EnsureMainThread();
        RequireInstance().SetPartOpacities(values);
    }

    public void QueuePartOpacity(string partId, float opacity)
        => _queuedPartOpacities.Queue(partId, opacity);

    public void QueuePartOpacities(IReadOnlyDictionary<string, float> values)
        => _queuedPartOpacities.Queue(values);

    private Live2DModelInstance RequireInstance()
        => _instance?.IsAlive == true
            ? _instance
            : throw new InvalidOperationException(
                $"Live2D model '{ModelId}' is not available in scene '{Scene}'.");

    private void ApplyQueuedValues(
        IReadOnlyDictionary<string, float> values,
        Action<Live2DModelInstance, IReadOnlyDictionary<string, float>> apply)
    {
        Live2DApi.EnsureMainThread();
        if (_instance?.IsAlive == true)
            apply(_instance, values);
    }

    private void DetachEvents()
    {
        if (_instance is null)
            return;
        _instance.MotionCompleted -= OnMotionFinished;
        _instance.MotionEventReceived -= OnMotionEvent;
    }

    private void OnMotionFinished() => MotionFinished?.Invoke(this);
    private void OnMotionEvent(string value) => MotionEvent?.Invoke(this, value);

    private static Live2DModelUpdate CreateUpdate(Action<Live2DModelUpdate> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var update = new Live2DModelUpdate();
        configure(update);
        return update;
    }

    private static Live2DModelSnapshot ApplyToSnapshot(
        Live2DModelSnapshot snapshot,
        Live2DModelUpdate update)
        => snapshot with
        {
            Position = update.Position ?? snapshot.Position,
            Scale = update.Scale ?? snapshot.Scale,
            RotationDegrees = update.RotationDegrees ?? snapshot.RotationDegrees,
            Opacity = update.Opacity is { } opacity
                ? Math.Clamp(opacity, 0f, 1f)
                : snapshot.Opacity,
            Visible = update.Visible ?? snapshot.Visible,
            Layer = update.Layer ?? snapshot.Layer,
            PlaybackSpeed = update.PlaybackSpeed is { } speed
                ? Math.Max(0f, speed)
                : snapshot.PlaybackSpeed,
            PhysicsEnabled = update.PhysicsEnabled ?? snapshot.PhysicsEnabled,
            PoseEnabled = update.PoseEnabled ?? snapshot.PoseEnabled,
            MaskViewportSize = update.MaskViewportSize is { } maskViewportSize
                ? Math.Max(0, maskViewportSize)
                : snapshot.MaskViewportSize,
            BlendMode = update.BlendMode ?? snapshot.BlendMode,
            Filter = update.Filter ?? snapshot.Filter,
            Mask = update.Mask ?? snapshot.Mask,
        };

    private Live2DModelSnapshot UnavailableSnapshot() => new(
        ModelId,
        Scene,
        false,
        Vector2.Zero,
        Vector2.One,
        0f,
        1f,
        false,
        0,
        1f,
        true,
        true,
        0,
        Live2DBlendMode.Normal,
        Live2DFilterSettings.Default,
        Live2DMaskSettings.None,
        Live2DPlaybackSnapshot.Empty);
}
