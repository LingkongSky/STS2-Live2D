# Getting Started

This guide covers runtime requirements, development-build installation, and importing your first model.

::: warning Release status
STS2 Live2D has not been released publicly. An official download link will be added here later; for now, only developers should use local builds.
:::

## Requirements

- Windows x86_64.
- Slay the Spire 2 version `0.107.1` or later.
- STS2 RitsuLib version `0.4.56` or later.
- A valid Live2D Cubism 3/4 model.

## Install a development build

Point `Sts2Dir` in `Live2D.csproj` at your game directory, then run:

```powershell
dotnet build -c Release
```

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
