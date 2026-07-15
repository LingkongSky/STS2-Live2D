# Public API Reference

The current runtime reports `Live2DApi.RuntimeVersion == "0.4.0"` and `Live2DApi.RuntimeApiVersion == 4`. Only `Live2D.Api` is stable public API.

## Thread rules

| API | Allowed thread |
| --- | --- |
| `Post`, `InvokeAsync` | Any |
| Model queries, handle identity, `Actions`, availability, async waits | Any |
| `QueueUpdate`, Parameter/Part Queue APIs | Any |
| Pack import, registration, creation, unregistration | Godot main thread |
| `Snapshot`, `Apply`, `Set*`, playback, dynamic reads, `Destroy` | Godot main thread |

All API events are raised on the Godot main thread. Async continuations are not guaranteed to resume there.

## Live2DApi

### Runtime state and queries

- `RuntimeApiVersion`, `RuntimeVersion`, `IsDispatcherReady`, `IsMainThread`.
- `GetModels()` and `GetModels(ownerModId)` return array snapshots.
- `GetModel` and `TryGetModel` query by runtime model ID and scene.
- `ModelAvailable` is raised when any model first becomes available.

### Dispatch

- `Post(Action)` is fire-and-forget and logs exceptions.
- `InvokeAsync(Action)` and `InvokeAsync<T>(Func<T>)` expose completion, exceptions, and cancellation.
- The dispatcher runs while paused and processes at most 512 work items per frame.

### Packs

- `ImportPack(path/data)` persists models and reports imported and duplicate counts.
- `RegisterPack(ownerModId, path/data)` registers a read-only Pack.
- Paths may be OS, `res://`, or `user://`; data overloads accept `ReadOnlyMemory<byte>`.

## ILive2DPackHandle

`OwnerModId`, `PackId`, `Name`, `IsRegistered`, and `Models` expose Pack identity and metadata. `CreateModel` creates an idempotent runtime instance;
`Unregister` removes every instance from the Pack. Identifiers are limited to 128 characters and cannot contain control characters.

## ILive2DModelHandle

### Identity and availability

`ModelId`, `OwnerModId`, `PackId`, `ModelKey`, `InstanceId`, and `Scene` form stable identity. `IsAvailable` reports binding state and `CanDestroy`
reports lifecycle authority.

- Availability events support continuous observation.
- Async availability waits are cancellable and race-free.
- `Snapshot` exposes transform, visibility, playback, and rendering state.
- `Destroy` removes a permitted Pack runtime instance.

### Updates

`Apply` performs a partial update, `Update` configures one, and `QueueUpdate` merges fields from any thread.

| Field | Validation |
| --- | --- |
| `Position` | Both components finite |
| `Scale` | Finite, non-zero; negative values flip |
| `RotationDegrees` | Finite |
| `Opacity` | Clamped to `0..1` |
| `Visible` | Root visibility |
| `Layer` | Godot `ZIndex` |
| `PlaybackSpeed` | Negative values become 0 |
| `PhysicsEnabled` / `PoseEnabled` | Cubism behavior |
| `MaskViewportSize` | Negative values become 0 |
| `BlendMode` | Defined enum value |
| `Filter` | Whole-model color filter |
| `Mask` | Model-local canvas clipping |

Convenience methods cover transform, visibility, playback, and rendering fields.

### Playback and dynamic values

- `Actions`, `PlayAction`, `PlayMotion`, `StopMotion`, expressions, and motion events.
- Parameter get/try/set/batch/queue methods.
- Part get/try/set opacity/batch/queue methods.

Unknown IDs throw `KeyNotFoundException`. Synchronous batches validate every ID before applying. Queues merge IDs case-insensitively.

## Rendering limits

| Filter | Range / default |
| --- | --- |
| Tint | Finite RGBA / white |
| Brightness | `-1..1` / 0 |
| Contrast | `0..4` / 1 |
| Saturation | `0..4` / 1 |
| Grayscale | `0..1` / 0 |
| Hue shift | Any finite angle / 0 |
| Invert | `0..1` / 0 |
| Gamma | `0.01..10` / 1 |

Masks are `None`, `Rectangle`, `Ellipse`, or `RoundedRectangle`. Enabled rectangles require positive size, non-negative corner radius, and
`SegmentsPerCorner` from `2..64`.

## Common exceptions

| Exception | Cause |
| --- | --- |
| `ArgumentException` | Blank, long, or control-character identifier |
| `ArgumentOutOfRangeException` | Non-finite value, invalid range, enum, or motion index |
| `InvalidOperationException` | Wrong thread, unavailable runtime, identity conflict, or unregistered Pack |
| `KeyNotFoundException` | Unknown Parameter or Part |
| `InvalidDataException` / `IOException` | Pack or file failure |
| `OperationCanceledException` | Cancelled wait or dispatch |
