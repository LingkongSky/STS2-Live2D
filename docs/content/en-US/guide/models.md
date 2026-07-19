# Model Management

The Model Management page handles player-owned models and centrally configures Managed Pack models supplied by other Mods. Each model can override global defaults.

## Import options

- Single model: select a `.model3.json` file.
- Configuration pack: select `.live2dpack`.
- Global configuration: import a pack that contains global settings.

The importer validates every texture, motion, expression, physics, and pose reference. Missing dependencies and unsafe paths are rejected.

## Global defaults and overrides

```text
Program defaults → Global settings → Non-null model overrides
```

An inherited field follows the global setting. A custom field affects only that model. Clear the override to resume inheritance. Filters and
canvas masks are inherited as whole objects rather than merged field by field.

## Main Menu and In Game

- **Main Menu** affects only the Main Menu host.
- **In Game** is shared by Map and Combat.
- The global visibility hotkey temporarily hides or restores every model without rewriting model settings.

## Rename and delete

Renaming changes only the display name, not the stable model ID. Deleting removes a player-imported model and its configuration. A
`RegisterPack` models show their provider and cannot be deleted or exported because the provider owns their assets.

## Configuration packs

Exporting from a model card includes only that model and its overrides. Import skips duplicate content by hash. Only persistent import changes
assets into the player model directory. Managed Packs persist only user choices such as layout, rendering, and hotkeys here.
