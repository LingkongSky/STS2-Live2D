using Godot;

namespace Live2D.Api;

/// <summary>
/// Stable handle for one model in one scene. The handle remains valid when the
/// underlying Godot nodes are rebuilt and reports availability through <see cref="IsAvailable"/>.
/// <para>中文：句柄在场景重建后仍保持有效；实际节点是否可用请检查 <see cref="IsAvailable"/>。</para>
/// </summary>
public interface ILive2DModelHandle
{
    /// <summary>Runtime model ID unique within a scene.</summary>
    string ModelId { get; }
    /// <summary>Owning Mod ID.</summary>
    string OwnerModId { get; }
    /// <summary>Registered Pack ID, or null for a user-managed model.</summary>
    string? PackId { get; }
    /// <summary>Stable model key inside its Pack.</summary>
    string ModelKey { get; }
    /// <summary>Owner-selected stable runtime instance ID.</summary>
    string InstanceId { get; }
    /// <summary>Scene in which this handle is instantiated.</summary>
    Live2DScene Scene { get; }
    /// <summary>Whether the handle is currently bound to a live scene instance.</summary>
    bool IsAvailable { get; }
    /// <summary>Whether <see cref="Destroy"/> is currently permitted.</summary>
    bool CanDestroy { get; }
    /// <summary>Actions declared by the model, available even before scene binding.</summary>
    IReadOnlyList<Live2DActionInfo> Actions { get; }
    /// <summary>
    /// Current live or last-known model state. Read on the Godot main thread.
    /// <para>中文：只能在 Godot 主线程读取；节点暂不可用时返回最后一次状态。</para>
    /// </summary>
    Live2DModelSnapshot Snapshot { get; }

    /// <summary>Raised on the Godot main thread after a scene instance is bound.</summary>
    event Action<ILive2DModelHandle>? BecameAvailable;
    /// <summary>Raised on the Godot main thread after a scene instance is unbound.</summary>
    event Action<ILive2DModelHandle>? BecameUnavailable;
    /// <summary>Raised when the active non-looping motion finishes.</summary>
    event Action<ILive2DModelHandle>? MotionFinished;
    /// <summary>Raised for a Cubism motion event; the second argument is its event string.</summary>
    event Action<ILive2DModelHandle, string>? MotionEvent;

    /// <summary>
    /// Completes when this stable handle is bound to a live scene instance. Returns
    /// immediately when it is already available and can be called from any thread.
    /// <para>中文：可从任意线程等待模型绑定，不需要轮询。</para>
    /// </summary>
    Task<ILive2DModelHandle> WaitUntilAvailableAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes when this stable handle is no longer bound to a live scene instance.
    /// Returns immediately when it is already unavailable and can be called from any thread.
    /// </summary>
    Task<ILive2DModelHandle> WaitUntilUnavailableAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a partial transient state update on the Godot main thread.
    /// <para>中文：立即更新必须在主线程调用；未赋值字段保持不变。</para>
    /// </summary>
    void Apply(Live2DModelUpdate update);
    /// <summary>Builds and applies a partial transient state update on the main thread.</summary>
    void Update(Action<Live2DModelUpdate> configure);

    /// <summary>
    /// Queues a state update from any thread. Pending updates for this model are
    /// merged, with the most recently submitted value winning for each field.
    /// <para>中文：可从任意线程调用；同一字段在执行前只保留最后一次提交值。</para>
    /// </summary>
    void QueueUpdate(Live2DModelUpdate update);

    /// <summary>
    /// Builds and queues a mergeable state update from any thread. Ordered commands
    /// such as motions and expressions are intentionally not part of this update.
    /// </summary>
    void QueueUpdate(Action<Live2DModelUpdate> configure);

    /// <summary>Sets model-root position.</summary>
    void SetPosition(Vector2 position);
    /// <summary>Sets model-root scale.</summary>
    void SetScale(Vector2 scale);
    /// <summary>Sets equal positive X/Y model-root scale.</summary>
    void SetUniformScale(float scale);
    /// <summary>Sets clockwise model-root rotation in degrees.</summary>
    void SetRotation(float degrees);
    /// <summary>Sets model opacity, clamped to 0 through 1.</summary>
    void SetOpacity(float opacity);
    /// <summary>Sets model-root visibility.</summary>
    void SetVisible(bool visible);
    /// <summary>Sets Godot canvas Z index.</summary>
    void SetLayer(int layer);
    /// <summary>Sets Cubism playback speed.</summary>
    void SetPlaybackSpeed(float speed);
    /// <summary>Enables or disables Cubism physics.</summary>
    void SetPhysicsEnabled(bool enabled);
    /// <summary>Enables or disables Cubism pose updates.</summary>
    void SetPoseEnabled(bool enabled);
    /// <summary>Sets Cubism drawable-mask viewport size.</summary>
    void SetMaskViewportSize(int size);
    /// <summary>Sets the whole-model blend operation.</summary>
    void SetBlendMode(Live2DBlendMode mode);
    /// <summary>Sets the whole-model color filter.</summary>
    void SetFilter(Live2DFilterSettings filter);
    /// <summary>Restores the neutral whole-model filter.</summary>
    void ResetFilter();
    /// <summary>Sets a model-local canvas crop.</summary>
    void SetMask(Live2DMaskSettings mask);
    /// <summary>Disables the model-local canvas crop.</summary>
    void ClearMask();

    /// <summary>Plays a declared action.</summary>
    void PlayAction(Live2DActionInfo action, bool loop = false);
    /// <summary>Plays a Cubism motion group/index.</summary>
    void PlayMotion(string group, int index, bool loop = false);
    /// <summary>Stops the current motion.</summary>
    void StopMotion();
    /// <summary>Starts a Cubism expression by ID.</summary>
    void SetExpression(string expressionId);
    /// <summary>Stops the active expression.</summary>
    void ClearExpression();

    /// <summary>Returns current Cubism parameter metadata and values.</summary>
    IReadOnlyList<Live2DParameterInfo> GetParameters();
    /// <summary>Tries to find a Cubism parameter by case-insensitive ID.</summary>
    bool TryGetParameter(string parameterId, out Live2DParameterInfo parameter);
    /// <summary>Sets one Cubism parameter, clamped to its model range.</summary>
    void SetParameter(string parameterId, float value);
    /// <summary>Validates and sets a batch of Cubism parameters atomically.</summary>
    void SetParameters(IReadOnlyDictionary<string, float> values);

    /// <summary>
    /// Queues a dynamic Cubism parameter value from any thread. Pending values are
    /// coalesced by case-insensitive parameter ID.
    /// </summary>
    void QueueParameter(string parameterId, float value);

    /// <summary>Queues and coalesces a batch of dynamic Cubism parameter values.</summary>
    void QueueParameters(IReadOnlyDictionary<string, float> values);

    /// <summary>Returns current Cubism part-opacity values.</summary>
    IReadOnlyList<Live2DPartInfo> GetParts();
    /// <summary>Tries to find a Cubism part by case-insensitive ID.</summary>
    bool TryGetPart(string partId, out Live2DPartInfo part);
    /// <summary>Sets one Cubism part opacity, clamped to 0 through 1.</summary>
    void SetPartOpacity(string partId, float opacity);
    /// <summary>Validates and sets a batch of Cubism part opacities atomically.</summary>
    void SetPartOpacities(IReadOnlyDictionary<string, float> values);

    /// <summary>
    /// Queues a Cubism part opacity from any thread. Pending values are coalesced
    /// by case-insensitive part ID.
    /// </summary>
    void QueuePartOpacity(string partId, float opacity);

    /// <summary>Queues and coalesces a batch of Cubism part opacity values.</summary>
    void QueuePartOpacities(IReadOnlyDictionary<string, float> values);

    /// <summary>
    /// Destroys this registered runtime instance.
    /// <para>中文：仅注册 Pack 创建且 <see cref="CanDestroy"/> 为 true 的实例允许销毁。</para>
    /// </summary>
    void Destroy();
}
