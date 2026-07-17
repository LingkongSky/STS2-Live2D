# Development and Architecture

This section is for maintainers of the Live2D runtime. Third-party authors should start with [Mod integration](../integration/getting-started).

## Runtime responsibilities

- Import model3 and every referenced dependency.
- Manage player models and read-only Pack instances.
- Attach hosts to Main Menu, Map, and Combat.
- Resolve global settings and model overrides.
- Expose transform, playback, rendering, Parameter, and Part APIs.
- Register settings, hotkeys, and patches through RitsuLib.

## Structure

```text
RitsuLib configuration
    ↓
Live2DConfigResolver
    ↓
Live2DRuntimeManager
    ├─ MainMenu host
    ├─ Map host
    └─ Combat host
          ↓
    Live2DModelInstance
          ↓
      gd_cubism
```

The main-thread dispatcher is a persistent child of `SceneTree.Root` and consumes a bounded concurrent queue every frame.

## Architectural rules

- Only `Live2DConfigResolver` implements inheritance.
- Main Menu has separate settings; Map and Combat share In Game settings.
- Layout is stored against 1920×1080 and scaled by the shorter viewport axis.
- Stable API handles rebind and restore session overrides after node rebuilds.
- Only `Live2D.Api` is public surface.
- The NuGet package contains only the `ref/net9.0` reference assembly and XML documentation.
- Ordered playback commands never enter coalescing queues.
- Current configuration accepts Schema 6 only; there is no migration layer.

## PCK and shaders

gd_cubism loads ten shaders from fixed `res://addons/gd_cubism/res/shader/*` paths. They must be included in `Live2D.pck`, and
`Live2D.json` must keep `has_pck: true`. Scene patches are not installed when shader validation fails.

## Game patches

Register patches through RitsuLib `CreatePatcher`, `IPatchMethod`, and `ApplyRequiredPatcher`. Do not create Harmony instances directly.

## Local generated files

`.gitignore` excludes Godot/.NET caches, NuGet and PCK artifacts, documentation dependencies, coverage output, crash dumps, and local
model fixtures. This project loads resources by path, so generated external `*.uid` files are not committed. The required
`addons/gd_cubism/bin/libgd_cubism.windows.release.x86_64.dll` is explicitly kept under version control.

## Source layout

```text
Scripts/Api/            Public API
Scripts/Configuration/  Settings, normalization, persistence, resolution
Scripts/Models/         model3 parser and managed repository
Scripts/Packs/          Archive import and read-only registration
Scripts/Runtime/        Scene hosts, Cubism, and model instances
Scripts/UI/             Settings and preview pages
docs/.vitepress/        Site configuration and theme
docs/content/zh-CN/     Simplified Chinese content
docs/content/en-US/     English content
docs/content/ja-JP/     Japanese content
Tools/                  Consumers, smoke tests, and PCK verification
examples/               Unpublished model fixtures
```

Current releases target Windows x86_64. Large textures and masks increase GPU memory usage.
