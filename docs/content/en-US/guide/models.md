# Model Management

The Model Management page handles player-owned models and centrally configures Managed Pack models supplied by other Mods. Each model can override global defaults.

## Import options

- Single model: select a `.model3.json` file.
- Configuration pack: select `.live2dpack`.
- Global configuration: import a pack that contains global settings.

The importer validates every texture, motion, expression, physics, and pose reference. For VTube Studio models, it also reads `.vtube.json`,
scans the `expressions` and `animations` directories, and merges assets omitted from model3 into the managed copy.

## Global defaults and overrides

```text
Program defaults → Global settings → Non-null model overrides
```

An inherited field follows the global setting. A custom field affects only that model. Clear the override to resume inheritance. Filters and
canvas masks are inherited as whole objects rather than merged field by field.

## Main Menu and In Game

- The list's **Enabled** checkbox is a persistent per-model master switch. Enabled models participate in scene rendering, physics, input, action hotkeys, and previews.
- **Main Menu** controls the Main Menu host.
- **In Game** is shared by Map and Combat.
- The global visibility hotkey temporarily hides or restores every model while preserving model settings.

## Rename and delete

Renaming updates the display name and preserves the stable model ID. Every delete requires confirmation. Deleting a player-imported model removes
its configuration and managed files. Removing a `RegisterPack` model clears Live2D's local entry while its provider Mod retains asset ownership;
**Restore Provider Models** adds it again.

When local files are missing or a provider Mod is absent, the model card shows **Model missing** and preserves layout, rendering, and hotkey settings.
Restoring the assets reactivates the model with the same configuration.

## Configuration packs

Exporting from a model card includes that model and its overrides. Persistent import skips duplicate content by hash and copies assets into the
player model directory. Managed Packs persist user choices such as layout, rendering, and hotkeys here.
