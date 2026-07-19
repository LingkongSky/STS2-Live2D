using Live2D.Scripts.Configuration;
using Live2D.Scripts.Packs;
using STS2RitsuLib.RuntimeInput;

namespace Live2D.Scripts.Runtime;

internal static class Live2DHotkeyManager
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
            if (model.IsExternalPackModel &&
                !Live2DRegisteredPackRegistry.TryGetLibraryModelAsset(model, out _))
                continue;
            foreach (var binding in model.ActionBindings.Where(value => !string.IsNullOrWhiteSpace(value.KeyBinding)))
            {
                var action = FindAction(model, binding);
                if (action == null)
                    continue;

                try
                {
                    if (!TryNormalizeBinding(binding.KeyBinding, out var normalizedBinding))
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
            if (!TryNormalizeBinding(hotkeys.ToggleVisibility, out var normalizedBinding))
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

    /// <summary>
    /// RitsuLib currently parses a bare numeric token through Enum.TryParse, so
    /// "1" becomes enum value 1 instead of Godot Key.Key1 (49). Use Godot's
    /// explicit KeyN enum name for runtime registration while keeping the stored
    /// and displayed form user-friendly.
    /// </summary>
    internal static bool TryNormalizeBinding(string value, out string normalizedBinding)
        => RuntimeHotkeyService.TryNormalizeBinding(
            RewriteDigitKeyForRuntime(value),
            out normalizedBinding);

    internal static string NormalizeBindingForStorage(string value)
    {
        if (!TryNormalizeBinding(value, out var runtimeBinding))
            return value.Trim();

        var parts = runtimeBinding.Split('+');
        for (var index = 0; index < parts.Length; index++)
        {
            var token = parts[index];
            if (token.Length == 4 &&
                token.StartsWith("Key", StringComparison.OrdinalIgnoreCase) &&
                char.IsAsciiDigit(token[3]))
                parts[index] = token[3].ToString();
        }
        return string.Join('+', parts);
    }

    private static string RewriteDigitKeyForRuntime(string value)
    {
        var parts = value.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length > 0 && parts[^1].Length == 1 && char.IsAsciiDigit(parts[^1][0]))
            parts[^1] = "Key" + parts[^1];
        return string.Join('+', parts);
    }
}
