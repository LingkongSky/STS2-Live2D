# Threads and Streaming Updates

Godot scene and Cubism nodes belong to the main thread. The API separates ordered commands from mergeable continuous state.

## Awaitable calls

```csharp
await Live2DApi.InvokeAsync(() => model.PlayMotion("TapBody", 0), cancellationToken);
var snapshot = await Live2DApi.InvokeAsync(() => model.Snapshot, cancellationToken);
```

`InvokeAsync` runs immediately on the main thread, propagates callback exceptions, and prevents a queued callback from starting when cancellation
wins. A callback already running is not forcibly interrupted.

Use `Post` for low-frequency fire-and-forget notifications. Its exceptions are logged:

```csharp
Live2DApi.Post(() => model.SetVisible(true));
```

Check `Live2DApi.IsDispatcherReady` during startup. Never synchronously wait on the main thread for work that needs that same thread.

## Continuous state

```csharp
model.QueueUpdate(update =>
{
    update.Position = trackedPosition;
    update.RotationDegrees = trackedRotation;
    update.Opacity = trackedOpacity;
});
```

Pending updates for one model are coalesced. Different fields are retained, and the last submission wins for the same field.

## Parameter and part queues

```csharp
model.QueueParameters(new Dictionary<string, float>
{
    ["ParamAngleX"] = angleX,
    ["ParamMouthOpenY"] = mouthOpen,
});
model.QueuePartOpacity("PartArmL", armOpacity);
```

IDs merge case-insensitively. A batch is discarded if the model is unavailable when execution begins.

## Which API to use

| Work | API |
| --- | --- |
| Motions, expressions, instance destruction | `InvokeAsync` |
| Return values and observable exceptions | `InvokeAsync` |
| Low-frequency notification | `Post` |
| Position, rotation, opacity, or filter streams | `QueueUpdate` |
| Face tracking, lip sync, part opacity | Parameter/Part Queue APIs |

Motion, Expression, Physics, and Pose may modify Parameters again on later frames, so continuous inputs should continue submitting samples.
