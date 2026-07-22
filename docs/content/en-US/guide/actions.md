# Actions and Hotkeys

The importer collects motions and expressions declared by model3. For VTube Studio models, it also discovers actions from `.vtube.json` and the
`expressions` and `animations` directories, then lists the combined set on the model's **Hotkeys** tab.

## Motions and expressions

- A Motion is a timed animation such as Idle, TapBody, or Wave.
- An Expression is a facial state such as smile or angry.
- Automatic idle playback searches for a Motion Group named `Idle`.
- Physics and Pose can be enabled independently.

If the action list is empty, verify that model3 declares the files and that every referenced file exists.

## Bind a hotkey

1. Open **Model Management**.
2. Select **Detailed Settings** on the model.
3. Open the **Hotkeys** tab.
4. Choose a key, target scenes, and whether the motion loops.
5. Save changes.

Bindings accept standalone letters, digits `0–9`, function keys, and key combinations.

Multiple actions can share one hotkey. They may run together, and the UI shows a conflict warning.

## Global visibility hotkey

The global visibility binding is a temporary visibility master control. The first press hides all enabled models; the next restores each model's
configured scene visibility while preserving configuration and per-model enabled state.

## Playback settings

- Negative playback speed is treated as 0.
- The action cooldown prevents duplicate triggers in a short interval.
- A looping Motion continues until stopped or replaced.
- Motion, Expression, Physics, and Pose may continue changing Cubism Parameters.
