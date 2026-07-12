using Godot;
using Live2D.Scripts.Configuration;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace Live2D.Scripts.Runtime;

public static class Live2DRuntimeManager
{
    private const string HostNodeName = "Live2DModels";
    private const string ViewportWatcherNodeName = "Live2DViewportWatcher";
    private static readonly Dictionary<Live2DSceneKind, WeakReference<Node>> Hosts = new();
    private static readonly Dictionary<Live2DSceneKind, List<Live2DModelInstance>> Instances = new();
    private static bool? _mainMenuModelsVisible;
    private static bool _globalVisibilitySuppressed;

    public static void Attach(Live2DSceneKind scene, Node host)
    {
        if (!GodotObject.IsInstanceValid(host))
            return;
        Hosts[scene] = new WeakReference<Node>(host);
        if (scene == Live2DSceneKind.MainMenu)
            _mainMenuModelsVisible = null;
        EnsureViewportWatcher(scene, host);
        Entry.Logger.Info($"[{Entry.ModId}] Attaching Live2D host for {scene}: {host.GetPath()}.");
        Rebuild(scene, host);
    }

    public static void RefreshMainMenuVisibility()
    {
        if (!Hosts.TryGetValue(Live2DSceneKind.MainMenu, out var weakHost) ||
            !weakHost.TryGetTarget(out var host) ||
            host is not NMainMenu mainMenu ||
            !GodotObject.IsInstanceValid(mainMenu))
            return;

        var container = mainMenu.GetNodeOrNull<Node2D>(HostNodeName);
        if (container is null || !GodotObject.IsInstanceValid(container))
            return;

        var submenuOpen = mainMenu.SubmenuStack?.SubmenusOpen ?? false;
        var modalOpen = NModalContainer.Instance?.OpenModal != null;
        var visible = !_globalVisibilitySuppressed && !submenuOpen && !modalOpen;
        container.Visible = visible;

        if (_mainMenuModelsVisible == visible)
            return;

        _mainMenuModelsVisible = visible;
        Entry.Logger.Info(
            $"[{Entry.ModId}] Main menu models are now {(visible ? "visible" : "hidden")} " +
            $"(submenuOpen={submenuOpen}, modalOpen={modalOpen}).");
    }

    public static void ToggleGlobalVisibility()
    {
        _globalVisibilitySuppressed = !_globalVisibilitySuppressed;
        foreach (var (scene, weakHost) in Hosts.ToArray())
        {
            if (!weakHost.TryGetTarget(out var host) ||
                !GodotObject.IsInstanceValid(host) ||
                !host.IsInsideTree())
                continue;

            if (scene == Live2DSceneKind.MainMenu)
            {
                RefreshMainMenuVisibility();
                continue;
            }

            var container = host.GetNodeOrNull<Node2D>(HostNodeName);
            if (container != null && GodotObject.IsInstanceValid(container))
                container.Visible = !_globalVisibilitySuppressed;
        }

        Entry.Logger.Info(
            $"[{Entry.ModId}] Global Live2D visibility toggled: " +
            $"{(_globalVisibilitySuppressed ? "hidden" : "visible")}.");
    }

    public static void RefreshAll()
    {
        foreach (var (scene, weakHost) in Hosts.ToArray())
        {
            if (weakHost.TryGetTarget(out var host) && GodotObject.IsInstanceValid(host) && host.IsInsideTree())
                Callable.From(() => Rebuild(scene, host)).CallDeferred();
            else
                Hosts.Remove(scene);
        }
    }

    internal static void RefreshForViewportChange(Live2DSceneKind scene, Node host, Vector2 viewportSize)
    {
        if (!GodotObject.IsInstanceValid(host) || !host.IsInsideTree())
            return;
        Entry.Logger.Info(
            $"[{Entry.ModId}] Viewport changed for {scene}: {viewportSize.X:0}x{viewportSize.Y:0}; rebuilding models.");
        Callable.From(() => Rebuild(scene, host)).CallDeferred();
    }

    public static void Play(
        string modelId,
        Live2DActionDescriptor action,
        bool loop,
        bool mainMenu = true,
        bool inGame = true)
    {
        foreach (var (scene, sceneInstances) in Instances)
        {
            var allowed = scene switch
            {
                Live2DSceneKind.MainMenu => mainMenu,
                Live2DSceneKind.InGame => inGame,
                _ => false,
            };
            if (!allowed)
                continue;

            foreach (var instance in sceneInstances.Where(value => value.ModelId == modelId && value.IsUsable))
                instance.Play(action, loop);
        }
    }

    private static void Rebuild(Live2DSceneKind scene, Node host)
    {
        if (!GodotObject.IsInstanceValid(host) || !host.IsInsideTree())
            return;

        var oldRoot = host.GetNodeOrNull<Node2D>(HostNodeName);
        if (oldRoot != null)
        {
            host.RemoveChildSafely(oldRoot);
            oldRoot.QueueFreeSafely();
        }

        var container = new Node2D { Name = HostNodeName };
        host.AddChild(container);
        var created = new List<Live2DModelInstance>();
        Instances[scene] = created;

        Live2DSettings settings;
        try
        {
            settings = Live2DConfigStore.Get();
        }
        catch (Exception ex)
        {
            Entry.Logger.Error($"[{Entry.ModId}] Unable to read settings while rebuilding {scene}: {ex}");
            return;
        }

        var viewportSize = host.GetViewport()?.GetVisibleRect().Size ?? new Vector2(1920f, 1080f);
        foreach (var model in settings.Models.OrderBy(value => value.DisplayOrder))
        {
            try
            {
                var resolved = Live2DConfigResolver.Resolve(settings.Global, model.Overrides);
                var sceneConfig = Live2DConfigResolver.ForScene(resolved, scene);
                if (!sceneConfig.Visible)
                    continue;

                var instance = Live2DModelInstance.Create(model, resolved, sceneConfig, viewportSize);
                container.AddChild(instance.Root);
                created.Add(instance);
                Entry.Logger.Info($"[{Entry.ModId}] Created model '{model.DisplayName}' ({model.Id}) in {scene}.");
            }
            catch (Exception ex)
            {
                Entry.Logger.Error($"[{Entry.ModId}] Failed to create model {model.Id} in {scene}: {ex}");
            }
        }

        if (scene == Live2DSceneKind.MainMenu)
            RefreshMainMenuVisibility();
        else
            container.Visible = !_globalVisibilitySuppressed;
    }

    private static void EnsureViewportWatcher(Live2DSceneKind scene, Node host)
    {
        var watcher = host.GetNodeOrNull<Live2DViewportWatcher>(ViewportWatcherNodeName);
        if (watcher == null)
        {
            watcher = new Live2DViewportWatcher { Name = ViewportWatcherNodeName };
            host.AddChild(watcher);
        }
        watcher.Configure(scene, host);
    }
}

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
