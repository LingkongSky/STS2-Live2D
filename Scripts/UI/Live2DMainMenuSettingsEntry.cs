using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using STS2RitsuLib.Settings;

namespace Live2D.Scripts.UI;

internal static class Live2DMainMenuSettingsEntry
{
    private const string ButtonNodeName = "Live2DSettingsButton";
    private static readonly MethodInfo? MainMenuButtonFocused = typeof(NMainMenu).GetMethod(
        "MainMenuButtonFocused", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly MethodInfo? MainMenuButtonUnfocused = typeof(NMainMenu).GetMethod(
        "MainMenuButtonUnfocused", BindingFlags.Instance | BindingFlags.NonPublic);

    public static void EnsureAttached(NMainMenu mainMenu)
    {
        try
        {
            if (!GodotObject.IsInstanceValid(mainMenu))
                return;

            var buttonContainer = mainMenu.GetNodeOrNull<VBoxContainer>("%MainMenuTextButtons");
            var settingsButton = buttonContainer?.GetNodeOrNull<NMainMenuTextButton>("SettingsButton");
            if (buttonContainer == null || settingsButton == null)
                return;

            var button = buttonContainer.GetNodeOrNull<NMainMenuTextButton>(ButtonNodeName);
            if (button == null)
            {
                button = settingsButton.Duplicate(
                    (int)(Node.DuplicateFlags.Groups | Node.DuplicateFlags.Scripts)) as NMainMenuTextButton;
                if (button == null)
                    throw new InvalidOperationException("Unable to clone the main-menu settings button.");

                button.Name = ButtonNodeName;
                button.Connect(
                    NClickableControl.SignalName.Released,
                    Callable.From<NButton>(_ => OpenLive2DSettings()));
                ConnectFocusFeedback(mainMenu, button);
                buttonContainer.AddChild(button);
                Entry.Logger.Info($"[{Entry.ModId}] Added the Live2D settings entry to the main menu.");
            }

            buttonContainer.MoveChild(button, settingsButton.GetIndex() + 1);
            if (button.GetNodeOrNull<Label>("Label") is { } label)
                label.Text = Live2DLocalization.Get("main_menu.settings_entry", "Live2D Settings");
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"[{Entry.ModId}] Failed to add the main-menu settings entry: {ex.Message}");
        }
    }

    private static void OpenLive2DSettings()
    {
        var result = ModSettingsNavigator.RequestOpenByIds(Entry.ModId, null, null, null);
        if (!result.Success)
            Entry.Logger.Warn($"[{Entry.ModId}] Unable to open Live2D settings: {result.Message}");
    }

    private static void ConnectFocusFeedback(NMainMenu mainMenu, NMainMenuTextButton button)
    {
        if (MainMenuButtonFocused != null)
        {
            button.Connect(
                NClickableControl.SignalName.Focused,
                Callable.From<NMainMenuTextButton>(focused =>
                    Callable.From(() =>
                    {
                        MainMenuButtonFocused.Invoke(mainMenu, [focused]);
                    }).CallDeferred()));
        }

        if (MainMenuButtonUnfocused != null)
        {
            button.Connect(
                NClickableControl.SignalName.Unfocused,
                Callable.From<NMainMenuTextButton>(unfocused =>
                {
                    MainMenuButtonUnfocused.Invoke(mainMenu, [unfocused]);
                }));
        }
    }
}
