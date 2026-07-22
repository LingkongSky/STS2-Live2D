# Bundled Model Packs

Another Mod can include a `.live2dpack` in its PCK and register it in the central Live2D model library. Model packages use the `.live2dpack` extension.

## Register

```csharp
var pack = Live2DApi.RegisterPack(
    "MyMod",
    "res://MyMod/live2d/characters.live2dpack");
```

Pack models immediately appear in Live2D Model Management. Live2D owns visibility, layout, rendering, actions, hotkeys, and scene instances.
The provider Mod delegates model settings, hotkeys, and instance control to Live2D.

Assets remain provider-owned in the session cache, while library export serves player-imported assets. A player may remove and restore the local
library entry while provider assets remain in place. Live2D persists player configuration; an absent provider or disabled model enters the unavailable
state with its configuration retained.

Providers can keep character-specific behavior such as intro motions, story reactions, or state integration. Implement
`ILive2DProviderLifecycleHook` and register it before the Pack to cooperate with Live2D's unified instance system:

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

The four stages are `OnPackRegistered`, `OnModelAvailable`, `OnModelUnavailable`, and `OnPackUnregistered`. Hook registration replays existing packs
and available models in order. Retain the returned `IDisposable` and dispose it at the end of the caller's lifecycle.

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
