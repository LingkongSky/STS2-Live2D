using System.Reflection;
using Godot;
using Live2D.Api;
using Live2D.Scripts.Configuration;
using Live2D.Scripts.Packs;
using Live2D.Scripts.Runtime;
using Live2D.Scripts.UI;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using STS2RitsuLib;
using STS2RitsuLib.Interop;
using STS2RitsuLib.Patching.Core;
using STS2RitsuLib.Patching.Models;

namespace Live2D;

[ModInitializer(nameof(Initialize))]
internal static class Entry
{
    public const string ModId = "Live2D";
    public const string ModVersion = "0.4.1";
    public static readonly MegaCrit.Sts2.Core.Logging.Logger Logger = RitsuLibFramework.CreateLogger(ModId);

    public static void Initialize()
    {
        Live2DApi.InitializeDispatcher(exception =>
            Logger.Error($"[{ModId}] Unhandled exception in a posted Live2D callback: {exception}"));
        Live2DRegisteredPackRegistry.ConfigureLogging(message => Logger.Info(message));
        Live2DConfigStore.Initialize();
        Live2DSettingsUi.Register();
        if (!CubismExtensionLoader.EnsureLoaded())
        {
            Logger.Error($"[{ModId}] Cubism runtime resources are incomplete; Live2D scene patches will not be applied.");
            return;
        }

        var assembly = Assembly.GetExecutingAssembly();
        RitsuLibFramework.EnsureGodotScriptsRegistered(assembly, Logger);
        ModTypeDiscoveryHub.RegisterModAssembly(ModId, assembly);

        var patcher = RitsuLibFramework.CreatePatcher(ModId, "scene-hosts", "Live2D scene hosts");
        patcher.RegisterPatch<MainMenuReadyPatch>();
        patcher.RegisterPatch<MainMenuSubmenuChangedPatch>();
        patcher.RegisterPatch<ModalContainerAddPatch>();
        patcher.RegisterPatch<ModalContainerClearPatch>();
        patcher.RegisterPatch<MapReadyPatch>();
        patcher.RegisterPatch<CombatReadyPatch>();
        if (!RitsuLibFramework.ApplyRequiredPatcher(
                patcher,
                () => Logger.Error($"[{ModId}] Required scene patches failed; Live2D runtime is disabled.")))
            return;

        Live2DHotkeyManager.Refresh();
        Logger.Info($"[{ModId}] Initialized.");
    }
}

internal sealed class MainMenuReadyPatch : IPatchMethod
{
    public static string PatchId => "live2d_main_menu_ready_host";
    public static string Description => "Attach the Live2D runtime host to the main menu";
    public static bool IsCritical => true;
    public static ModPatchTarget[] GetTargets() => [new(typeof(NMainMenu), nameof(NMainMenu._Ready))];

    public static void Postfix(NMainMenu __instance)
    {
        Live2DMainMenuSettingsEntry.EnsureAttached(__instance);
        Live2DRuntimeManager.Attach(Live2DSceneKind.MainMenu, __instance);
    }
}

internal sealed class MapReadyPatch : IPatchMethod
{
    public static string PatchId => "live2d_map_ready_host";
    public static string Description => "Attach the Live2D runtime host to the map";
    public static bool IsCritical => true;
    public static ModPatchTarget[] GetTargets() => [new(typeof(NMapScreen), nameof(NMapScreen._Ready))];

    public static void Postfix(NMapScreen __instance)
        => Live2DRuntimeManager.Attach(Live2DSceneKind.InGame, __instance);
}

internal sealed class MainMenuSubmenuChangedPatch : IPatchMethod
{
    public static string PatchId => "live2d_main_menu_submenu_visibility";
    public static string Description => "Hide main-menu Live2D models while a submenu is open";
    public static bool IsCritical => true;
    public static ModPatchTarget[] GetTargets() => [new(typeof(NMainMenu), "OnSubmenuStackChanged")];

    public static void Postfix(NMainMenu __instance)
    {
        Live2DMainMenuSettingsEntry.EnsureAttached(__instance);
        Live2DRuntimeManager.RefreshMainMenuVisibility();
    }
}

internal sealed class ModalContainerAddPatch : IPatchMethod
{
    public static string PatchId => "live2d_main_menu_modal_open_visibility";
    public static string Description => "Hide main-menu Live2D models while a modal is open";
    public static bool IsCritical => true;
    public static ModPatchTarget[] GetTargets() => [new(typeof(NModalContainer), nameof(NModalContainer.Add))];

    public static void Postfix()
        => Live2DRuntimeManager.RefreshMainMenuVisibility();
}

internal sealed class ModalContainerClearPatch : IPatchMethod
{
    public static string PatchId => "live2d_main_menu_modal_close_visibility";
    public static string Description => "Restore main-menu Live2D models after a modal closes";
    public static bool IsCritical => true;
    public static ModPatchTarget[] GetTargets() => [new(typeof(NModalContainer), nameof(NModalContainer.Clear))];

    public static void Postfix()
        => Live2DRuntimeManager.RefreshMainMenuVisibility();
}

internal sealed class CombatReadyPatch : IPatchMethod
{
    public static string PatchId => "live2d_combat_ready_host";
    public static string Description => "Attach the Live2D runtime host to combat rooms";
    public static bool IsCritical => true;
    public static ModPatchTarget[] GetTargets() => [new(typeof(NCombatRoom), nameof(NCombatRoom._Ready))];

    public static void Postfix(NCombatRoom __instance)
        => Live2DRuntimeManager.Attach(Live2DSceneKind.InGame, __instance);
}
