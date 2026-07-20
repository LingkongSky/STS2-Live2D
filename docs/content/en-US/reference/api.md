# Public API Reference

The current runtime reports `Live2DApi.RuntimeVersion == "0.5.6"` and `Live2DApi.RuntimeApiVersion == 9`. Only `Live2D.Api` is stable public API.

## Thread rules

| API | Allowed thread |
| --- | --- |
| `Post`, `InvokeAsync` | Any |
| Model queries, handle identity, `Actions`, availability, async waits | Any |
| `QueueUpdate`, Parameter/Part Queue APIs | Any |
| Pack import, registration, unregistration, provider hook registration | Godot main thread |
| `Snapshot`, `Apply`, `Set*`, playback, dynamic reads | Godot main thread |

All API events are raised on the Godot main thread. Async continuations are not guaranteed to resume there.

## Live2DApi

### Runtime state and queries

- `RuntimeApiVersion`, `RuntimeVersion`, `PackageFileExtension`, `IsDispatcherReady`, `IsMainThread`.
- `GetModels()` and `GetModels(ownerModId)` return array snapshots.
- `GetModel` and `TryGetModel` query by runtime model ID and scene.
- `RegisterProviderHook(ownerModId, hook)` registers a provider-scoped four-stage lifecycle hook and replays existing state.

### Dispatch

- `Post(Action)` is fire-and-forget and logs exceptions.
- `InvokeAsync(Action)` and `InvokeAsync<T>(Func<T>)` expose completion, exceptions, and cancellation.
- The dispatcher runs while paused and processes at most 512 work items per frame.

### Packs

- `ImportPack(path/data)` persists models and reports imported and duplicate counts.
- `RegisterPack(ownerModId, path/data)` exposes provider-owned assets in the central Live2D library; Live2D owns settings and instances.
- `RegisterProviderHook(ownerModId, hook)` adds character-specific behavior without transferring settings or instance ownership.
- Paths may be OS, `res://`, or `user://`; data overloads accept `ReadOnlyMemory<byte>`.

Every path-based import and registration requires the `.live2dpack` extension. Other extensions are rejected even when their content is a ZIP archive.

`res://`, `user://`, and in-memory inputs are materialized to an OS temporary file. It is deleted after success or failure. Read-only registration
copies required assets into its session cache and does not depend on the temporary file afterward. Managed registration persists user configuration,
not provider-owned assets.

## ILive2DPackHandle

`OwnerModId`, `PackId`, `Name`, `IsRegistered`, and `Models` expose Pack identity and metadata. `Unregister` removes the provider assets while
keeping player configuration. Identifiers are limited to 128 characters and cannot contain control characters.

## ILive2DProviderLifecycleHook

| Stage | Timing and purpose |
| --- | --- |
| `OnPackRegistered` | Pack registered before model refresh; inspect resource metadata |
| `OnModelAvailable` | Scene model bound; provider playback is safe |
| `OnModelUnavailable` | Scene model unbound; cancel pending asynchronous behavior |
| `OnPackUnregistered` | Provider assets removed from the current session |

Hooks are isolated by `ownerModId` and run on the Godot main thread. Exceptions are logged without interrupting other hooks. Late registration
replays existing packs before currently available models. The provider must retain the returned `IDisposable`.

## ILive2DModelHandle

### Identity and availability

`ModelId`, `OwnerModId`, `PackId`, `ModelKey`, `InstanceId`, and `Scene` form stable identity. `IsAvailable` reports binding state.

Scene changes and node rebuilds do not invalidate the handle. Identity and `Actions` remain readable while unavailable; `Snapshot` is main-thread-only
and returns the last-known state when no live node is bound.

- Availability events support continuous observation.
- Async availability waits are cancellable and race-free.
- `Snapshot` exposes transform, visibility, playback, and rendering state.

### Updates

`Apply` performs a partial update and `Update` configures one; both require the main thread. `QueueUpdate` merges fields from any thread. Its
configuration callback runs immediately on the calling thread, so it should only fill update data and must not access Godot nodes.

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
