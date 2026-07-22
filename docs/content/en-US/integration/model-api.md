# Model Control

`ILive2DModelHandle` provides stable identity, availability, snapshots, and commands. Operations that touch live Godot nodes require the main thread.

## Partial updates

```csharp
model.Update(update =>
{
    update.Position = new Vector2(1180f, 720f);
    update.Scale = new Vector2(-0.42f, 0.42f);
    update.Opacity = 0.9f;
    update.Layer = 20;
    update.PlaybackSpeed = 1.1f;
});
```

Unset fields remain unchanged. `Snapshot` returns current transform, visibility, playback, and rendering state.

## Motions and expressions

```csharp
var wave = model.Actions.First(action => action.DisplayName == "Wave");
model.PlayAction(wave);
model.PlayMotion("TapBody", 0);
model.StopMotion();
model.SetExpression("smile");
model.ClearExpression();
```

`Actions` remains readable while the scene instance is unavailable. Motion events are raised on the Godot main thread; subscribers are isolated and failures are logged.

## Parameters and parts

```csharp
model.SetParameters(new Dictionary<string, float>
{
    ["ParamAngleX"] = 15f,
    ["ParamMouthOpenY"] = 0.6f,
});
model.SetPartOpacity("PartArmL", 0.5f);
```

Batch writes validate every ID before applying anything. Parameter values are clamped to declared ranges and part opacity to `0..1`. These
dynamic values are not persisted or restored after a model rebuild.

## Filters and clipping

```csharp
model.SetBlendMode(Live2DBlendMode.Add);
model.SetFilter(new Live2DFilterSettings
{
    Tint = new Color(0.85f, 0.95f, 1f),
    Brightness = 0.05f,
    Contrast = 1.1f,
    Saturation = 0.8f,
});
model.SetMask(new Live2DMaskSettings
{
    Type = Live2DMaskType.RoundedRectangle,
    Rect = new Rect2(-420f, -760f, 840f, 920f),
    CornerRadius = 48f,
});
```

Use `ResetFilter`, `ClearMask`, and `Live2DBlendMode.Normal` to restore neutral rendering.

## Lifecycle

- `IsAvailable` reports whether a scene instance is bound.
- `WaitUntilAvailableAsync` and `WaitUntilUnavailableAsync` are cancellable waits.
- Availability events are suitable for continuous observation.

After an asynchronous wait, use `InvokeAsync` before issuing further model commands.
