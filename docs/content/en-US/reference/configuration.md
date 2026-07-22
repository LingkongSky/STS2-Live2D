# Configuration Structure

Live2D stores `settings.json` through RitsuLib under the `settings` key. `SchemaVersion` is `6`. Prefer the in-game UI for edits.

## Resolution

```text
Program defaults → Global → Non-null model Overrides
```

`null` means inherit. Filter and Mask are selected as complete objects rather than merged by subfield.

## Top level

```json
{
  "SchemaVersion": 6,
  "Global": {
    "Hotkeys": {},
    "MainMenu": {},
    "InGame": {},
    "Playback": {},
    "Rendering": {}
  },
  "Models": [],
  "RemovedExternalModelIds": []
}
```

## Global

`Hotkeys.ToggleVisibility` stores the master visibility binding.

### MainMenu / InGame

| Field | Meaning |
| --- | --- |
| `Visible` | Scene visibility |
| `Anchor` | One of nine screen anchor points |
| `OffsetX/Y` | Offset on a 1920×1080 reference canvas |
| `Scale` | Uniform scale |
| `RotationDegrees` | Clockwise rotation |
| `Opacity` | Clamped to `0..1` |
| `Layer` | Godot `ZIndex` |
| `MouseInteraction` | Mouse input enabled |

Map and Combat both use `InGame`.

### Playback

Defaults are `Speed=1`, Physics and Pose enabled, automatic Idle enabled, and a `0.1` second action cooldown.

### Rendering

`MaskViewportSize` controls Cubism mask texture size. `BlendMode` supports Normal, Add, Subtract, Multiply, and PremultipliedAlpha. User-facing
mask fields are `Type`, `X/Y`, `Width/Height`, and `CornerRadius`; ellipse and rounded edges are evaluated analytically without a rendering-segment
setting. `Filter` and `Mask` otherwise match the public API ranges. Invalid persisted values are clamped or replaced with safe defaults.

## Models

Each model stores stable ID, `Enabled` (default `true`), display name, managed model3 path, source record, content hash, import time, display order,
overrides, discovered actions, and hotkey bindings. Action kind is `0` for Motion and `1` for Expression.

`RemovedExternalModelIds` prevents provider models explicitly removed by the player from being re-added on a repeated Pack registration. The in-game restore action clears these exclusions.

## Runtime overrides

Handle updates affect the current session only and do not write `settings.json`. Stable state is restored after node rebuilds; Parameter and Part
values are short-lived and are not restored.
