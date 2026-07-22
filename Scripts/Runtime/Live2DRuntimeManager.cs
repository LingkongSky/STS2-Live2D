using Godot;
using Live2D.Api;
using Live2D.Scripts.Configuration;
using Live2D.Scripts.Models;
using Live2D.Scripts.Packs;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace Live2D.Scripts.Runtime;

internal static class Live2DRuntimeManager
{
    private const string HostNodeName = "Live2DModels";
    private const string ViewportWatcherNodeName = "Live2DViewportWatcher";
    private static readonly Dictionary<Live2DSceneKind, WeakReference<Node>> Hosts = new();
    private static readonly Dictionary<Live2DSceneKind, List<Live2DModelInstance>> Instances = new();
    private static readonly HashSet<Live2DSceneKind> PendingRebuilds = [];
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
                QueueRebuild(scene);
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
        QueueRebuild(scene);
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

        Live2DApi.UnbindScene(scene);
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
        var apiScene = Live2DApi.ToApiScene(scene);
        var definitions = new List<Live2DRuntimeModelDefinition>();
        foreach (var model in settings.Models)
        {
            try
            {
                if (!model.Enabled)
                    continue;
                if (!Live2DModelRepository.IsModelAvailable(model, out _))
                    continue;
                definitions.Add(new Live2DRuntimeModelDefinition(
                    new Live2DRuntimeModelIdentity(
                        model.Id,
                        model.IsExternalPackModel ? model.ExternalOwnerModId : Entry.ModId,
                        model.IsExternalPackModel ? model.ExternalPackId : null,
                        model.IsExternalPackModel ? model.ExternalModelKey : model.Id,
                        model.Id,
                        apiScene),
                    model,
                    Live2DModelRepository.GetAbsoluteModelPath(model)));
            }
            catch (Exception ex)
            {
                Entry.Logger.Error(
                    $"[{Entry.ModId}] Failed to resolve user model {model.Id} in {scene}: {ex}");
            }
        }
        foreach (var definition in definitions.OrderBy(value => value.Config.DisplayOrder))
        {
            var model = definition.Config;
            try
            {
                var resolved = Live2DConfigResolver.Resolve(settings.Global, model.Overrides);
                var sceneConfig = Live2DConfigResolver.ForScene(resolved, scene);
                var instance = Live2DModelInstance.Create(definition, resolved, sceneConfig, viewportSize);
                container.AddChild(instance.Root);
                created.Add(instance);
                Live2DApi.Bind(definition, instance);
                Entry.Logger.Info(
                    $"[{Entry.ModId}] Created model '{model.DisplayName}' " +
                    $"({definition.Identity.OwnerModId}/{definition.Identity.InstanceId}) in {scene}.");
            }
            catch (Exception ex)
            {
                Entry.Logger.Error(
                    $"[{Entry.ModId}] Failed to create model {definition.Identity.RuntimeId} in {scene}: {ex}");
            }
        }

        if (scene == Live2DSceneKind.MainMenu)
            RefreshMainMenuVisibility();
        else
            container.Visible = !_globalVisibilitySuppressed;
    }

    private static void QueueRebuild(Live2DSceneKind scene)
    {
        if (!PendingRebuilds.Add(scene))
            return;

        Callable.From(() =>
        {
            PendingRebuilds.Remove(scene);
            if (!Hosts.TryGetValue(scene, out var weakHost) ||
                !weakHost.TryGetTarget(out var currentHost) ||
                !GodotObject.IsInstanceValid(currentHost) ||
                !currentHost.IsInsideTree())
            {
                Hosts.Remove(scene);
                return;
            }

            Rebuild(scene, currentHost);
        }).CallDeferred();
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
