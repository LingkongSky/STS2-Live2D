using Godot;
using Live2D.Api;

namespace Live2DApiConsumerExample;

/// <summary>
/// Minimal third-party integration example. Direct members run on the Godot main thread;
/// the Async example can safely be called from a worker thread.
/// A real consumer can replace the ProjectReference with STS2.Live2D PackageReference.
/// </summary>
public sealed class ExampleLive2DController : IDisposable
{
    private ILive2DPackHandle? _pack;
    private ILive2DModelHandle? _model;

    public ILive2DModelHandle RegisterAndCreate(string resourcePackPath)
    {
        if (Live2DApi.RuntimeApiVersion < 4)
            throw new NotSupportedException("This integration requires Live2D API version 4.");

        _pack = Live2DApi.RegisterPack("ExampleMod", resourcePackPath);
        var modelInfo = _pack.Models.First();
        _model = _pack.CreateModel(modelInfo.ModelKey, new Live2DCreateOptions
        {
            Scene = Live2DScene.InGame,
            InstanceId = "example-character",
            InitialState = new Live2DModelUpdate
            {
                Position = new Vector2(1350f, 760f),
                Scale = Vector2.One * 0.4f,
                Opacity = 0.9f,
                Filter = new Live2DFilterSettings { Saturation = 0.9f },
            },
        });
        _model.BecameAvailable += OnModelAvailable;
        _model.MotionFinished += OnMotionFinished;
        return _model;
    }

    public void MoveAndFade(Vector2 position, float opacity)
    {
        var model = RequireModel();
        model.Update(update =>
        {
            update.Position = position;
            update.Opacity = opacity;
        });
    }

    public Task MoveAndFadeFromAnyThreadAsync(
        Vector2 position,
        float opacity,
        CancellationToken cancellationToken = default)
        => Live2DApi.InvokeAsync(
            () => MoveAndFade(position, opacity),
            cancellationToken);

    /// <summary>
    /// High-frequency state path: multiple pending calls are merged into one
    /// main-thread Apply, so callers do not need to await every tracking sample.
    /// </summary>
    public void TrackFromAnyThread(Vector2 position, float rotationDegrees, float opacity)
        => RequireModel().QueueUpdate(update =>
        {
            update.Position = position;
            update.RotationDegrees = rotationDegrees;
            update.Opacity = opacity;
        });

    public void DriveFaceFromAnyThread(float angleX, float mouthOpen, float eyeOpen)
        => RequireModel().QueueParameters(new Dictionary<string, float>
        {
            ["ParamAngleX"] = angleX,
            ["ParamMouthOpenY"] = mouthOpen,
            ["ParamEyeLOpen"] = eyeOpen,
            ["ParamEyeROpen"] = eyeOpen,
        });

    public async Task WaitAndShowAsync(CancellationToken cancellationToken = default)
    {
        var model = await RequireModel()
            .WaitUntilAvailableAsync(cancellationToken)
            .ConfigureAwait(false);
        await Live2DApi.InvokeAsync(
            () => model.SetVisible(true),
            cancellationToken).ConfigureAwait(false);
    }

    public bool PlayAction(string displayName, bool loop = false)
    {
        var model = RequireModel();
        var action = model.Actions.FirstOrDefault(candidate =>
            string.Equals(candidate.DisplayName, displayName, StringComparison.OrdinalIgnoreCase));
        if (action is null || !model.IsAvailable)
            return false;
        model.PlayAction(action, loop);
        return true;
    }

    public Live2DPlaybackSnapshot Playback => RequireModel().Snapshot.Playback;

    public void Dispose()
    {
        if (_model is not null)
        {
            _model.BecameAvailable -= OnModelAvailable;
            _model.MotionFinished -= OnMotionFinished;
            if (_model.CanDestroy)
                _model.Destroy();
            _model = null;
        }
        _pack?.Unregister();
        _pack = null;
    }

    private ILive2DModelHandle RequireModel()
        => _model ?? throw new InvalidOperationException("RegisterAndCreate must be called first.");

    private static void OnModelAvailable(ILive2DModelHandle model)
    {
        // Pending InitialState and later runtime overrides have already been restored here.
    }

    private static void OnMotionFinished(ILive2DModelHandle model)
    {
        // Snapshot.Playback.HasMotion is false before this callback is raised.
    }
}
