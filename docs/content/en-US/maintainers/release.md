# Testing and Release

A release must pass documentation, compilation, API consumption, rendering, PCK, and package checks.

## Documentation and compilation

```powershell
.\Tools\check-docs.ps1
Push-Location .\docs
npm ci
npm run build
Pop-Location
dotnet build .\Live2D.csproj -c Release -p:Live2DCopyToGame=false
dotnet build .\Tools\ApiConsumerExample\Live2DApiConsumerExample.csproj `
  -c Release -p:Live2DCopyToGame=false
```

The consumer output must not contain `Live2D.dll`.

## Rendering smoke test

```powershell
.\Tools\run-render-smoke.ps1 `
  -GodotPath "C:\Tools\Godot\Godot_v4.5.1-stable_mono_win64_console.exe" `
  -Sts2Dir "D:\SteamLibrary\steamapps\common\Slay the Spire 2" `
  -CaptureImage
```

This covers blend modes, clipping geometry, filters, dispatch, coalesced queues, async waits, Parameters, Parts, and real GPU drawing.

## PCK and NuGet

Use `Tools/export-live2d-pck.ps1` to export and verify all ten shaders in isolation. Pack NuGet with:

```powershell
dotnet pack .\Live2D.csproj -c Release -p:Live2DCopyToGame=false -o artifacts
$packageSource = (Resolve-Path .\artifacts).Path
dotnet restore .\Tools\ApiConsumerExample\Live2DApiConsumerExample.csproj `
  -p:Live2DPackageVersion=0.4.0 `
  -p:RestoreAdditionalProjectSources="$packageSource" --force
dotnet build .\Tools\ApiConsumerExample\Live2DApiConsumerExample.csproj `
  -c Release -p:Live2DPackageVersion=0.4.0 --no-restore
```

NuGet may contain only `ref/net10.0` DLL/XML documentation, README, Markdown docs, and the buildTransitive target. It must not contain a `lib/`
runtime assembly. Setting `Live2DPackageVersion` switches the consumer example from ProjectReference to the packaged API; its output must still omit `Live2D.dll`.

## Release checklist

1. Manifest requirements and all version numbers match.
2. Chinese, English, and Japanese docs build without broken links.
3. Release and consumer builds pass.
4. GPU smoke test passes.
5. PCK verification reports shaders `10/10`.
6. A real Pack completes registration, creation, destruction, unregistration, and persistent-import checks.
7. Deployed and Release DLL hashes match.
8. A full game restart passes Main Menu, Map, Combat, settings, import, and hotkey checks.

The site publishes only current documentation; it does not generate old-version pages.
