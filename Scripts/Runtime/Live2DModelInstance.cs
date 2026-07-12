using Godot;
using Live2D.Scripts.Configuration;
using Live2D.Scripts.Models;

namespace Live2D.Scripts.Runtime;

public sealed class Live2DModelInstance
{
    private readonly GDCubismUserModelCS _userModel;
    private readonly Live2DModelConfig _model;
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
        root.AddChild(userModelNode);

        userModel.LoadExpressions = true;
        userModel.LoadMotions = true;
        userModel.SpeedScale = Math.Max(0f, resolved.Playback.Speed);
        userModel.PhysicsEvaluate = resolved.Playback.EnablePhysics;
        userModel.PoseUpdate = resolved.Playback.EnablePose;
        userModel.MaskViewportSize = Math.Max(0, resolved.Rendering.MaskViewportSize);
        userModel.Assets = Live2DModelRepository.GetAbsoluteModelPath(model);

        var result = new Live2DModelInstance(root, userModel, model);
        if (resolved.Playback.AutoPlayIdle)
            Callable.From(result.TryPlayIdle).CallDeferred();
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

            if (loop)
                _userModel.StartMotionLoop(action.MotionGroup, action.MotionIndex,
                    GDCubismUserModelCS.PriorityEnum.PriorityForce, true, true);
            else
                _userModel.StartMotion(action.MotionGroup, action.MotionIndex,
                    GDCubismUserModelCS.PriorityEnum.PriorityForce);
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"[{Entry.ModId}] Failed to play action for model {_model.Id}: {ex.Message}");
        }
    }

    private void TryPlayIdle()
    {
        try
        {
            var motions = _userModel.GetMotions();
            if (motions.TryGetValue("Idle", out var count) && count > 0)
                _userModel.StartMotionLoop("Idle", 0, GDCubismUserModelCS.PriorityEnum.PriorityIdle, true, true);
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"[{Entry.ModId}] Unable to start idle motion for model {_model.Id}: {ex.Message}");
        }
    }

}
