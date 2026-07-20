# Bundled Model Packs

Another Mod can include a `.live2dpack` in its PCK and register it in the central Live2D model library. `.live2dpack` is the only supported package extension.

## Register

```csharp
var pack = Live2DApi.RegisterPack(
    "MyMod",
    "res://MyMod/live2d/characters.live2dpack");
```

Pack models immediately appear in Live2D Model Management. Live2D owns visibility, layout, rendering, actions, hotkeys, and scene instances.
The provider Mod should not add a second model settings page, hotkey controller, or instance controller.

Assets remain provider-owned and exist only in the session cache, so they cannot be exported from the library. A player may remove the local
library entry without deleting provider assets and can restore it later. Live2D persists only the player's configuration. If the provider is absent
or the player disables the model, it becomes unavailable while its configuration remains saved.

Providers can keep character-specific behavior such as intro motions, story reactions, or state integration. Implement
`ILive2DProviderLifecycleHook` and register it before the Pack; do not create a second instance system:

```csharp
sealed class CharacterHook : ILive2DProviderLifecycleHook
{
    public void OnModelAvailable(ILive2DModelHandle model)
    {
        if (model.Scene == Live2DScene.MainMenu)
            model.PlayMotion("Intro", 0);
    }

    public void OnModelUnavailable(ILive2DModelHandle model)
    {
        // Cancel pending provider-specific asynchronous behavior.
    }
}

var lifecycle = Live2DApi.RegisterProviderHook("MyMod", new CharacterHook());
var pack = Live2DApi.RegisterPack("MyMod", "res://MyMod/live2d/characters.live2dpack");
```

The four stages are `OnPackRegistered`, `OnModelAvailable`, `OnModelUnavailable`, and `OnPackUnregistered`. Late registration immediately replays
existing packs and currently available models in order. Keep the returned `IDisposable` alive and dispose it when no longer needed.

## Lifecycle

`pack.Unregister()` removes the provider assets and refreshes the library. Player configuration remains available for the next registration of the
same `OwnerModId + PackId + ModelKey`. Registering identical content twice returns the existing handle; different content under the same identity is rejected.

## Paths and export

Registration accepts OS paths, `res://`, `user://`, and `ReadOnlyMemory<byte>`. Include the Pack explicitly in the provider PCK:

```ini
export_filter="resources"
include_filter="MyMod/live2d/*.live2dpack"
exclude_filter="artifacts/**,Scripts/**,MyMod/src/**"
```

The root of `settings/models.json` must be an array, including for one model: `[{ ... }]`. See the
[Pack format reference](../reference/pack-format) for the complete structure.
