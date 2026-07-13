using Godot;
using Live2D.Scripts.Configuration;
using Live2D.Scripts.Models;

namespace Live2D.Scripts.Runtime;

public sealed class Live2DModelInstance
{
    private readonly GDCubismUserModelCS _userModel;
    private readonly Live2DModelConfig _model;
    private Live2DPlaybackController? _playbackController;
    private string _activeExpression = "";

    public Node2D Root { get; }
    public string ModelId => _model.Id;
    public bool IsUsable => GodotObject.IsInstanceValid(Root) && Root.IsInsideTree() && Root.IsVisibleInTree();

    private Live2DModelInstance(Node2D root, GDCubismUserModelCS userModel, Live2DModelConfig model)
    {
        Root = root;
        _userModel = userModel;
        _model = model;
    }

    public static Live2DModelInstance Create(
        Live2DModelConfig model,
        ResolvedLive2DConfig resolved,
        SceneDisplayConfig sceneConfig,
        Vector2 viewportSize)
    {
        var root = new Node2D
        {
            Name = $"Model_{model.Id}",
            ProcessMode = Node.ProcessModeEnum.Always,
            Position = Live2DLayout.ResolvePosition(
                viewportSize,
                sceneConfig.Anchor,
                sceneConfig.OffsetX,
                sceneConfig.OffsetY),
            Scale = Vector2.One * Live2DLayout.ResolveModelScale(sceneConfig.Scale, viewportSize),
            RotationDegrees = sceneConfig.RotationDegrees,
            ZIndex = sceneConfig.Layer,
            Modulate = new Color(1f, 1f, 1f, Math.Clamp(sceneConfig.Opacity, 0f, 1f)),
        };

        var userModel = new GDCubismUserModelCS();
        var userModelNode = userModel.GetInternalObject();
        userModelNode.Name = "GDCubismUserModel";
        userModelNode.ProcessMode = Node.ProcessModeEnum.Always;
        root.AddChild(userModelNode);

        userModel.LoadExpressions = true;
        userModel.LoadMotions = true;
        userModel.PlaybackProcessMode = GDCubismUserModelCS.MotionProcessCallbackEnum.Idle;
        userModel.SpeedScale = Math.Max(0f, resolved.Playback.Speed);
        userModel.PhysicsEvaluate = resolved.Playback.EnablePhysics;
        userModel.PoseUpdate = resolved.Playback.EnablePose;
        userModel.MaskViewportSize = Math.Max(0, resolved.Rendering.MaskViewportSize);
        AddEffectNode(userModelNode, "GDCubismEffectBreath");
        AddEffectNode(userModelNode, "GDCubismEffectEyeBlink");
        userModel.Assets = Live2DModelRepository.GetAbsoluteModelPath(model);

        var result = new Live2DModelInstance(root, userModel, model);
        var playbackController = new Live2DPlaybackController
        {
            Name = "Live2DPlaybackController",
            ProcessMode = Node.ProcessModeEnum.Always,
        };
        playbackController.Configure(result, resolved.Playback.AutoPlayIdle);
        result._playbackController = playbackController;
        root.AddChild(playbackController);
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

            _playbackController?.BeforeMotion(loop);
            if (loop)
                _userModel.StartMotionLoop(action.MotionGroup, action.MotionIndex,
                    GDCubismUserModelCS.PriorityEnum.PriorityForce, true, true);
            else
                _userModel.StartMotion(action.MotionGroup, action.MotionIndex,
                    GDCubismUserModelCS.PriorityEnum.PriorityForce);
        }
        catch (Exception ex)
        {
            _playbackController?.RequestIdle();
            Entry.Logger.Warn($"[{Entry.ModId}] Failed to play action for model {_model.Id}: {ex.Message}");
        }
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

internal enum IdleStartResult
{
    Started,
    NoIdleMotion,
    Retry,
}

internal sealed partial class Live2DPlaybackController : Node
{
    private const double RetryIntervalSeconds = 0.2d;
    private const int VerificationFrameCount = 3;

    private Live2DModelInstance? _owner;
    private bool _autoPlayIdle;
    private bool _resumeIdleAfterMotion;
    private int _verificationFramesLeft;
    private double _retryDelay;

    public void Configure(Live2DModelInstance owner, bool autoPlayIdle)
    {
        _owner = owner;
        _autoPlayIdle = autoPlayIdle;
    }

    public override void _Ready()
    {
        if (_owner == null)
        {
            SetProcess(false);
            return;
        }

        _owner.ConnectMotionFinished(OnMotionFinished);
        if (_autoPlayIdle)
            RequestIdle();
        else
            SetProcess(false);
    }

    public override void _ExitTree()
    {
        if (_owner != null)
            _owner.DisconnectMotionFinished(OnMotionFinished);
    }

    public override void _Process(double delta)
    {
        if (!_autoPlayIdle || _owner == null)
        {
            SetProcess(false);
            return;
        }

        if (_verificationFramesLeft > 0)
        {
            // Cubism 资源异步加载：发起待机后等待数帧确认队列，未入队时再短暂重试。
            if (_owner.HasQueuedMotion())
            {
                Entry.Logger.Info($"[{Entry.ModId}] Idle motion started for model {_owner.ModelId}.");
                _verificationFramesLeft = 0;
                SetProcess(false);
                return;
            }

            if (--_verificationFramesLeft > 0)
                return;

            _retryDelay = RetryIntervalSeconds;
        }

        if (_retryDelay > 0d)
        {
            _retryDelay -= delta;
            return;
        }

        switch (_owner.TryStartIdle())
        {
            case IdleStartResult.Started:
                _verificationFramesLeft = VerificationFrameCount;
                break;
            case IdleStartResult.Retry:
                _retryDelay = RetryIntervalSeconds;
                break;
            case IdleStartResult.NoIdleMotion:
                Entry.Logger.Info($"[{Entry.ModId}] Model {_owner.ModelId} has no Idle motion group.");
                SetProcess(false);
                break;
        }
    }

    public void BeforeMotion(bool loop)
    {
        // 非循环动作结束后恢复待机；循环动作由用户再次操作切换，不主动打断。
        _resumeIdleAfterMotion = _autoPlayIdle && !loop;
        _verificationFramesLeft = 0;
        _retryDelay = 0d;
        SetProcess(false);
    }

    public void RequestIdle()
    {
        if (!_autoPlayIdle)
            return;
        _resumeIdleAfterMotion = false;
        _verificationFramesLeft = 0;
        _retryDelay = 0d;
        SetProcess(true);
    }

    private void OnMotionFinished()
    {
        if (_resumeIdleAfterMotion)
            RequestIdle();
    }
}
