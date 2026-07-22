# Testing and Release

A release passes documentation, compilation, API consumption, rendering, PCK, and package checks before tagging.

## Documentation and compilation

```powershell
Push-Location .\docs
npm ci
npm run build
Pop-Location
```

`dotnet build` performs compilation checks; `dotnet publish` produces release binaries. Consumer builds use the ref-only API package and resolve the runtime through the Mod dependency.

## Rendering smoke test

```powershell
$Godot = "C:\Tools\Godot\Godot_v4.5.1-stable_mono_win64_console.exe"
$env:STS2_DIR = "D:\SteamLibrary\steamapps\common\Slay the Spire 2"
dotnet build .\Tools\RenderSmoke\Live2DRenderSmoke.csproj
& $Godot --headless --path .\Tools\RenderSmoke --rendering-method gl_compatibility
```

This covers blend modes, clipping geometry, filters, dispatch, coalesced queues, async waits, Parameters, Parts, and real GPU drawing.

## PCK and NuGet

Export the PCK with Godot, verify all ten shaders in isolation, and collect the complete mod with `dotnet publish`:

```powershell
& $Godot --headless --editor --path $PWD --quit
& $Godot --headless --path $PWD --export-pack Live2D "$PWD\Live2D.pck"
& $Godot --headless --path .\Tools\PckVerifier `
  --script verify_pck.gd -- "$PWD\Live2D.pck"
dotnet publish .\Live2D.csproj -c Release -o .\artifacts\Live2D -p:BundleMod=true

dotnet build .\Live2D.csproj -c Release -t:RefreshNuGetReference
git diff -- NuGet/package/ref/net9.0
dotnet pack .\NuGet\STS2.Live2D.Package.csproj -c Release -o .\artifacts
```

Install or publish the complete `artifacts/Live2D` directory from one publish run.

NuGet contains the `ref/net9.0` reference assembly/XML documentation, README, third-party notices, and `docs/content` Markdown. The release workflow
validates the ref-only structure with a disposable consumer project.

`NuGet/package/ref/net9.0` is the prepared release directory. Its `Live2D.dll` is the metadata-only reference assembly. Commit the refreshed DLL and XML together with the corresponding source changes.

Pushing a `v*` tag that matches the project version runs `publish-nuget.yml`, which packages and validates the committed reference assets and publishes
through NuGet Trusted Publishing (OIDC). Bind the NuGet.org policy to this workflow and the `nuget` environment.

## Release checklist

1. Manifest requirements and all version numbers match.
2. Chinese, English, and Japanese docs build without broken links.
3. Release and consumer builds pass.
4. GPU smoke test passes.
5. PCK verification reports shaders `10/10`.
6. A real Pack completes registration, model availability/unavailability hooks, unregistration, and persistent-import checks.
7. Deployed and Release DLL hashes match.
8. A full game restart passes Main Menu, Map, Combat, settings, import, and hotkey checks.

The site publishes the documentation from the `main` branch.
