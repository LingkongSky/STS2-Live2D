# Getting Started

This guide covers runtime requirements, development-build installation, and importing your first model.

::: info Installation scope
Players install the `Live2D` runtime Mod; third-party Mod authors use the `STS2.Live2D` compile-time NuGet API.
:::

## Requirements

- Windows x86_64.
- Slay the Spire 2 version `0.107.1` or later.
- STS2 RitsuLib version `0.4.56` or later.
- A valid Live2D Cubism 3/4 model.

## Install a development build

Installing a development build requires Godot 4.5.1 Mono and the .NET 9 SDK. Godot exports the shader PCK; the .NET CLI creates the complete Mod directory:

```powershell
$Godot = "C:\Tools\Godot\Godot_v4.5.1-stable_mono_win64_console.exe"
$env:STS2_DIR = "D:\SteamLibrary\steamapps\common\Slay the Spire 2"

& $Godot --headless --editor --path $PWD --quit
& $Godot --headless --path $PWD --export-pack Live2D "$PWD\Live2D.pck"
dotnet publish .\Live2D.csproj -c Release -o .\artifacts\Live2D -p:BundleMod=true
```

Copy the entire `artifacts/Live2D` directory to `mods/Live2D`. It must contain at least:

```text
Live2D.json
Live2D.dll
Live2D.pck
addons/gd_cubism/
```

Use `dotnet build` for compilation checks. Install or update from one complete `dotnet publish` directory, then fully exit and restart the game.

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
