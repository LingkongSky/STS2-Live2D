using Godot;
using Live2D.Api;
using Live2D.Scripts.Configuration;
using Live2D.Scripts.Models;
using Live2D.Scripts.Packs;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace Live2D.Scripts.Runtime;

internal sealed partial class Live2DViewportWatcher : Node
{
    private WeakReference<Node>? _host;
    private Live2DSceneKind _scene;
    private Vector2 _lastViewportSize;
    private double _elapsed;

    public Live2DViewportWatcher()
    {
        ProcessMode = ProcessModeEnum.Always;
    }

    public void Configure(Live2DSceneKind scene, Node host)
    {
        _scene = scene;
        _host = new WeakReference<Node>(host);
        _lastViewportSize = host.GetViewport()?.GetVisibleRect().Size ?? Live2DLayout.ReferenceViewportSize;
    }

    public override void _Process(double delta)
    {
        _elapsed += delta;
        if (_elapsed < 0.25d)
            return;
        _elapsed = 0d;

        if (_host == null || !_host.TryGetTarget(out var host) || !GodotObject.IsInstanceValid(host))
        {
            QueueFree();
            return;
        }

        var size = host.GetViewport()?.GetVisibleRect().Size ?? Live2DLayout.ReferenceViewportSize;
        if (size.IsEqualApprox(_lastViewportSize))
            return;
        _lastViewportSize = size;
        Live2DRuntimeManager.RefreshForViewportChange(_scene, host, size);
    }
}

