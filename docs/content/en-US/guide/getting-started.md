# Getting Started

This guide covers runtime requirements, development-build installation, and importing your first model.

::: warning Installation scope
The NuGet package is compile-time only and does not contain the player runtime. Build the current runtime from this repository and avoid files from unknown sources.
:::

## Requirements

- Windows x86_64.
- Slay the Spire 2 version `0.107.1` or later.
- STS2 RitsuLib version `0.4.56` or later.
- A valid Live2D Cubism 3/4 model.

## Install a development build

The build discovers the Steam game directory automatically. Run:

```powershell
dotnet build -c Release
```

If discovery fails, set the `STS2_DIR` environment variable or run
`dotnet build -c Release -p:Sts2Dir="game directory"`.

The build copies the runtime to `mods/Live2D`. That directory must contain at least:

```text
Live2D.json
Live2D.dll
Live2D.pck
addons/gd_cubism/
```

Fully exit and restart the game after installing or replacing the DLL.

## Import your first model

1. Open **Live2D Settings** from the Main Menu.
2. Open **Model Management**.
3. Select **Add Live2D Model**.
4. Choose the model's `.model3.json`.
5. When the model appears in the library, select **Preview and Adjust**.

The importer copies the `.moc3`, textures, motions, expressions, physics, pose, and other referenced files into the managed model directory.

## First layout

Choose **Main Menu** or **In Game** in the preview editor, then:

- Drag with the left mouse button to move the model.
- Use the mouse wheel to scale it.
- Use `Shift + mouse wheel` to rotate it.
- Test common preview resolutions.
- Select **Save Changes**.

Map and Combat share the **In Game** configuration. Main Menu models are temporarily hidden while another menu page or modal is open.

## Next steps

- [Manage multiple models](./models)
- [Configure filters and clipping](./appearance)
- [Set up actions and hotkeys](./actions)
- [Troubleshoot common problems](./troubleshooting)
