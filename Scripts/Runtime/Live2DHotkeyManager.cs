using Live2D.Scripts.Configuration;
using STS2RitsuLib.RuntimeInput;

namespace Live2D.Scripts.Runtime;

public static class Live2DHotkeyManager
{
    private static readonly List<IRuntimeHotkeyHandle> Handles = [];

    public static void Refresh()
    {
        foreach (var handle in Handles)
            handle.Dispose();
        Handles.Clear();

        var settings = Live2DConfigStore.Get();
        RegisterGlobalHotkeys(settings.Global.Hotkeys);
        foreach (var model in settings.Models)
        {
            foreach (var binding in model.ActionBindings.Where(value => !string.IsNullOrWhiteSpace(value.KeyBinding)))
            {
                var action = FindAction(model, binding);
                if (action == null)
                    continue;

                try
                {
                    if (!RuntimeHotkeyService.TryNormalizeBinding(binding.KeyBinding, out var normalizedBinding))
                        throw new FormatException("RitsuLib could not parse the key combination.");
                    var capturedModelId = model.Id;
                    var capturedAction = action;
                    var capturedLoop = binding.Loop;
                    var capturedMainMenu = binding.MainMenu;
                    var capturedInGame = binding.InGame;
                    Handles.Add(RuntimeHotkeyService.Register(
                        normalizedBinding,
                        () => Live2DRuntimeManager.Play(
                            capturedModelId,
                            capturedAction,
                            capturedLoop,
                            capturedMainMenu,
                            capturedInGame),
                        new RuntimeHotkeyOptions
                        {
                            Id = $"{Entry.ModId}.{model.Id}.{binding.Id}",
                            MarkInputHandled = true,
                            SuppressWhenTextInputFocused = true,
                            SuppressWhenDevConsoleVisible = true,
                            DebugName = $"{model.DisplayName}: {action.DisplayName}",
                        }));
                }
                catch (Exception ex)
                {
                    Entry.Logger.Warn($"[{Entry.ModId}] Invalid hotkey '{binding.KeyBinding}' for {model.Id}: {ex.Message}");
                }
            }
        }
    }

    private static void RegisterGlobalHotkeys(GlobalHotkeyConfig hotkeys)
    {
        if (string.IsNullOrWhiteSpace(hotkeys.ToggleVisibility))
            return;

        try
        {
            if (!RuntimeHotkeyService.TryNormalizeBinding(hotkeys.ToggleVisibility, out var normalizedBinding))
                throw new FormatException("RitsuLib could not parse the key combination.");
            Handles.Add(RuntimeHotkeyService.Register(
                normalizedBinding,
                Live2DRuntimeManager.ToggleGlobalVisibility,
                new RuntimeHotkeyOptions
                {
                    Id = $"{Entry.ModId}.global.toggle_visibility",
                    MarkInputHandled = true,
                    SuppressWhenTextInputFocused = true,
                    SuppressWhenDevConsoleVisible = true,
                    DebugName = "Toggle all Live2D visibility",
                }));
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn(
                $"[{Entry.ModId}] Invalid global visibility hotkey '{hotkeys.ToggleVisibility}': {ex.Message}");
        }
    }

    private static Live2DActionDescriptor? FindAction(Live2DModelConfig model, ActionBindingConfig binding)
        => model.AvailableActions.FirstOrDefault(action =>
            action.Kind == binding.Kind
            && (action.Kind == Live2DActionKind.Expression
                ? action.ExpressionId == binding.ExpressionId
                : action.MotionGroup == binding.MotionGroup && action.MotionIndex == binding.MotionIndex));
}
