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

- `Live2DConfigResolver` centralizes inheritance for every consumer.
- Main Menu has separate settings; Map and Combat share In Game settings.
- Layout is stored against 1920×1080 and scaled by the shorter viewport axis.
- Stable API handles rebind and restore session overrides after node rebuilds.
- `Live2D.Api` defines the public surface.
- The NuGet package contains the `ref/net9.0` reference assembly and XML documentation.
- Ordered playback commands use their dedicated command path; coalescing queues carry state updates.
- Configuration schema is fixed at `6`.
- Missing local or provider assets preserve model configuration, surface a missing state in the UI, and pause instances and hotkeys until assets return.

## PCK and shaders

gd_cubism loads ten shaders from fixed `res://addons/gd_cubism/res/shader/*` paths. They must be included in `Live2D.pck`, and
`Live2D.json` keeps `has_pck: true`. Scene patches are installed after shader validation succeeds.

## Game patches

Register patches through RitsuLib `CreatePatcher`, `IPatchMethod`, and `ApplyRequiredPatcher`.

## Local generated files

`.gitignore` excludes Godot/.NET caches, NuGet and PCK artifacts, documentation dependencies, coverage output, crash dumps, and local
model fixtures. The repository tracks the required `addons/gd_cubism/bin/libgd_cubism.windows.release.x86_64.dll`; `.gitignore` manages the
remaining generated resources.

## Source layout

```text
Scripts/Api/            Public API
Scripts/Configuration/  Settings, normalization, persistence, resolution
Scripts/Models/         model3 parser and managed repository
Scripts/Packs/          Archive import and model-library registration
Scripts/Runtime/        Scene hosts, Cubism, and model instances
Scripts/UI/             Settings and preview pages
docs/.vitepress/        Site configuration and theme
docs/content/zh-CN/     Simplified Chinese content
docs/content/en-US/     English content
docs/content/ja-JP/     Japanese content
Tools/                  Consumers, smoke tests, and PCK verification
examples/               Model fixtures
```

## Platform and resources

Releases target Windows x86_64. Large textures and masks increase GPU memory usage.
