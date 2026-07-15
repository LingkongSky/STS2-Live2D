using Godot;
using Live2D.Api;
using Live2D.Scripts.Configuration;
using Live2D.Scripts.Models;
using Live2D.Scripts.Runtime;
using STS2RitsuLib;
using STS2RitsuLib.Settings;

namespace Live2D.Scripts.UI;

internal sealed partial class Live2DPreviewCanvas : Control
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

