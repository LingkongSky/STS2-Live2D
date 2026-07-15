using Godot;
using Live2D.Api;
using Live2D.Scripts.Configuration;
using Live2D.Scripts.Models;

namespace Live2D.Scripts.Runtime;

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

