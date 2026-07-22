using Godot;

namespace Live2D.Scripts.UI;

/// <summary>
/// Captures action hotkeys during the regular input phase so bare keys such as
/// 1-9 cannot be consumed by the surrounding game UI before reaching the editor.
/// </summary>
internal sealed partial class Live2DActionKeyBindingControl : HBoxContainer
{
    private static WeakReference<Live2DActionKeyBindingControl>? _activeCapture;

    private readonly Action<string> _onChanged;
    private readonly Button _captureButton;
    private bool _capturing;
    private string _currentValue;

    internal Live2DActionKeyBindingControl(string initialValue, Action<string> onChanged)
    {
        _currentValue = initialValue;
        _onChanged = onChanged;
        CustomMinimumSize = new Vector2(360f, 0f);
        SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
        AddThemeConstantOverride("separation", 6);

        _captureButton = new Button
        {
            Text = DisplayValue(initialValue),
            CustomMinimumSize = new Vector2(290f, 0f),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _captureButton.Pressed += BeginCapture;
        AddChild(_captureButton);

        var clearButton = new Button
        {
            Text = "×",
            CustomMinimumSize = new Vector2(54f, 0f),
        };
        clearButton.Pressed += () => ApplyBinding("");
        AddChild(clearButton);

        SetProcessInput(false);
    }

    public override void _Input(InputEvent @event)
    {
        if (!_capturing || @event is not InputEventKey keyEvent || keyEvent.IsEcho() || !keyEvent.Pressed)
            return;

        GetViewport().SetInputAsHandled();
        switch (keyEvent.Keycode)
        {
            case Key.Escape:
                EndCapture();
                return;
            case Key.Backspace or Key.Delete:
                ApplyBinding("");
                return;
        }

        if (IsModifierKey(keyEvent.Keycode))
        {
            _captureButton.Text = BuildModifierPrefix(keyEvent) + "…";
            return;
        }

        var binding = BuildBinding(keyEvent);
        if (!string.IsNullOrWhiteSpace(binding))
            ApplyBinding(binding);
    }

    public override void _ExitTree()
    {
        if (_activeCapture?.TryGetTarget(out var active) == true && ReferenceEquals(active, this))
            _activeCapture = null;
        base._ExitTree();
    }

    internal static string BuildBinding(InputEventKey keyEvent)
    {
        var key = keyEvent.Keycode != Key.None ? keyEvent.Keycode : keyEvent.PhysicalKeycode;
        if (key == Key.None || IsModifierKey(key))
            return "";

        var parts = new List<string>();
        if (keyEvent.CtrlPressed)
            parts.Add("Ctrl");
        if (keyEvent.AltPressed)
            parts.Add("Alt");
        if (keyEvent.ShiftPressed)
            parts.Add("Shift");
        if (keyEvent.MetaPressed)
            parts.Add("Meta");
        parts.Add(key.ToString());
        return string.Join('+', parts);
    }

    private void BeginCapture()
    {
        if (_activeCapture?.TryGetTarget(out var active) == true && !ReferenceEquals(active, this))
            active.EndCapture();
        _activeCapture = new WeakReference<Live2DActionKeyBindingControl>(this);
        _capturing = true;
        _captureButton.Text = "…";
        _captureButton.GrabFocus();
        SetProcessInput(true);
    }

    private void ApplyBinding(string value)
    {
        _currentValue = value;
        EndCapture();
        try
        {
            _onChanged(value);
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"[{Entry.ModId}] Failed to update action hotkey: {ex.Message}");
        }
    }

    private void EndCapture()
    {
        _capturing = false;
        SetProcessInput(false);
        _captureButton.Text = DisplayValue(_currentValue);
        if (_activeCapture?.TryGetTarget(out var active) == true && ReferenceEquals(active, this))
            _activeCapture = null;
    }

    private static string BuildModifierPrefix(InputEventKey keyEvent)
    {
        var parts = new List<string>();
        if (keyEvent.CtrlPressed || keyEvent.Keycode.ToString().Contains("ctrl", StringComparison.OrdinalIgnoreCase))
            parts.Add("Ctrl+");
        if (keyEvent.AltPressed || keyEvent.Keycode.ToString().Contains("alt", StringComparison.OrdinalIgnoreCase))
            parts.Add("Alt+");
        if (keyEvent.ShiftPressed || keyEvent.Keycode.ToString().Contains("shift", StringComparison.OrdinalIgnoreCase))
            parts.Add("Shift+");
        if (keyEvent.MetaPressed || keyEvent.Keycode.ToString().Contains("meta", StringComparison.OrdinalIgnoreCase))
            parts.Add("Meta+");
        return string.Concat(parts);
    }

    private static bool IsModifierKey(Key key)
    {
        var name = key.ToString();
        return name.Contains("shift", StringComparison.OrdinalIgnoreCase)
               || name.Contains("ctrl", StringComparison.OrdinalIgnoreCase)
               || name.Contains("control", StringComparison.OrdinalIgnoreCase)
               || name.Contains("alt", StringComparison.OrdinalIgnoreCase)
               || name.Contains("meta", StringComparison.OrdinalIgnoreCase)
               || name.Contains("command", StringComparison.OrdinalIgnoreCase);
    }

    private static string DisplayValue(string value) => string.IsNullOrWhiteSpace(value) ? "—" : value;
}
