# Troubleshooting

Fully restart the game first, then search the log for `[Live2D]`.

## The Mod does not load

1. Confirm the game version satisfies `min_game_version` in `Live2D.json`.
2. Install the required RitsuLib version.
3. Check that `mods/Live2D` contains `Live2D.json`, `Live2D.dll`, `Live2D.pck`, and the gd_cubism native files.
4. Fully restart after replacing the DLL.

## The model is white or invisible

- Startup logs should report Cubism shaders `10/10`.
- Check `.moc3`, textures, relative paths, and path casing in `.model3.json`.
- Reset filters, use Normal blending, and temporarily disable canvas clipping.
- Check the target scene, opacity, scale, and master visibility state.

## The model appears in only one scene

Main Menu and In Game use separate settings; Map and Combat share In Game. A Main Menu model is temporarily hidden while another menu page or
modal is open.

## Motions or expressions are missing

- Cubism models: confirm model3 declares the action.
- VTube Studio models: confirm `.vtube.json` points to the selected model3 and assets are under `expressions` or `animations`.
- Re-import the model to refresh the managed action list.
- Check that its hotkey is enabled in the current scene.
- Check for duplicate hotkeys or conflicting looping motions.

## The model shows “Model missing”

Restore managed files for a local model, or enable the provider Mod for a provider model. Live2D preserves the configuration and reactivates the
instance and hotkeys when assets become available.

## Pack import fails

- The root must contain `manifest.json` and `settings/models.json`.
- Format values are `FormatVersion = 1` and `SettingsSchemaVersion = 6`.
- Every model resource must stay under `models/<OriginalId>/`.
- Absolute paths, `..`, symlinks, duplicate paths, and suspicious compression are rejected.

If the log says `JSON value could not be converted to List<Live2DModelConfig>`, change the Pack's `settings/models.json` root to an array. Check the
actual load path in the startup log, compare SHA-256 hashes between the publish directory and `mods/<ModId>`, and deploy one complete publish set.

See the [Pack format](../reference/pack-format) for the complete rules.

## Reporting an issue

Include the game, RitsuLib, and Live2D versions; relevant `[Live2D]` log lines; the affected scene; and whether the model or Pack can be shared.
Remove personal paths and other sensitive data before posting logs.
