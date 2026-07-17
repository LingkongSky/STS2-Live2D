# Bundled Model Packs

Another Mod can include a `.live2dpack` or `.livepck` in its PCK and ask the Live2D runtime to read it. Both extensions use the same ZIP format.

## Read-only registration or import

| Goal | API | Writes to player library |
| --- | --- | --- |
| The model belongs to your Mod | `RegisterPack` | No |
| The player should edit it permanently | `ImportPack` | Yes |

Prefer read-only registration for character assets owned by another Mod.

## Register and create

```csharp
var pack = Live2DApi.RegisterPack(
    "MyMod",
    "res://MyMod/live2d/characters.live2dpack");

var info = pack.Models.First(model => model.ModelKey == "character-main");
var model = pack.CreateModel(info.ModelKey, new Live2DCreateOptions
{
    Scene = Live2DScene.MainMenu,
    InstanceId = "main-menu-character",
    InitialState = new Live2DModelUpdate
    {
        Position = new Vector2(1350f, 760f),
        Scale = Vector2.One * 0.4f,
        Opacity = 0.9f,
    },
});
```

`OwnerModId / PackId / Scene / InstanceId` identifies an instance. Creating the same identity for the same model is idempotent; pointing it at a
different `ModelKey` is rejected.

## Lifecycle

```csharp
model.Destroy();
pack.Unregister();
```

Destroy removes one runtime instance. Unregister removes all instances from that Pack and releases its session cache. Neither deletes player models.

## Sources

Registration and import accept OS paths, `res://`, `user://`, and `ReadOnlyMemory<byte>`. Ensure the Pack is included in your PCK export. See the
[Pack format reference](../reference/pack-format) for archive structure and security limits.

The runtime materializes `res://`, `user://`, and memory inputs in the OS temporary directory. Temporary files are deleted after success or failure;
`RegisterPack` copies required assets into an independent session cache that is released by `Unregister`.
