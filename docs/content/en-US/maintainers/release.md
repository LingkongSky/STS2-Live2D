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

Use `Tools/export-live2d-pck.ps1` to export and verify all ten shaders in isolation. NuGet follows JMC's prepared `modPublish` approach. After changing the public API or XML comments, refresh the committed reference assets on a machine with the game installed, then pack them:

```powershell
.\Tools\update-nuget-reference.ps1 `
  -Sts2Dir "D:\Program Files\Steam\steamapps\common\Slay the Spire 2"
git diff -- NuGet/package/ref/net9.0
.\Tools\pack-nuget.ps1 -OutputDirectory artifacts
$packageSource = (Resolve-Path .\artifacts).Path
dotnet restore .\Tools\ApiConsumerExample\Live2DApiConsumerExample.csproj `
  -p:Live2DPackageVersion=0.4.0 `
  -p:RestoreAdditionalProjectSources="$packageSource" --force
dotnet build .\Tools\ApiConsumerExample\Live2DApiConsumerExample.csproj `
  -c Release -p:Live2DPackageVersion=0.4.0 --no-restore
```

NuGet may contain only the `ref/net9.0` reference assembly/XML documentation, README, third-party notices, `docs/content` Markdown, and the buildTransitive target. It must not contain a `lib/`
runtime assembly. Setting `Live2DPackageVersion` switches the consumer example from ProjectReference to the packaged API; its output must still omit `Live2D.dll`.

`NuGet/package/ref/net9.0` is the prepared release directory. Its `Live2D.dll` must be a metadata-only reference assembly, never the runtime DLL. Commit the refreshed DLL and XML together with the corresponding source changes.

Pushing a `v*` tag that matches the project version runs `publish-nuget.yml`. It only packages and validates the committed reference assets; it does not compile Live2D, read a game installation, or access `STS2-API-Signatures`. It then publishes through NuGet Trusted Publishing (OIDC). Bind the NuGet.org policy to this workflow and the `nuget` environment. `STS2_SIGNATURES_TOKEN` is no longer required.

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
