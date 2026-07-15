# Five-minute Mod Setup

This guide adds a compile-time reference, declares the runtime dependency, and performs the first model update. Only `Live2D.Api` is public API.

## 1. Add a compile-time reference

Use the ref-only NuGet package:

```xml
<PackageReference Include="STS2.Live2D" Version="0.4.0" />
```

For development in the same workspace:

```xml
<ProjectReference Include="..\STS2-Live2D\Live2D.csproj"
                  Private="false"
                  AdditionalProperties="Live2DCopyToGame=false" />
```

Neither option copies a second `Live2D.dll` to the consumer output.

## 2. Declare the runtime dependency

```json
{
  "dependencies": [
    { "id": "Live2D", "min_version": "0.4.0" }
  ]
}
```

Players must install the Live2D runtime. Live2D declares its own RitsuLib dependency.

## 3. Get a model

```csharp
using Live2D.Api;

var model = Live2DApi.GetModel("model-id", Live2DScene.MainMenu);
if (model is null)
    return;

await model.WaitUntilAvailableAsync(cancellationToken);
```

Handles are stable. If settings, viewport changes, or scene transitions rebuild the node, the same handle binds to the replacement.

## 4. Control it on the main thread

```csharp
using Godot;

await Live2DApi.InvokeAsync(() =>
{
    model.Update(update =>
    {
        update.Position = new Vector2(1200f, 760f);
        update.Scale = Vector2.One * 0.45f;
        update.RotationDegrees = -5f;
        update.Opacity = 0.85f;
        update.Visible = true;
    });
    model.PlayMotion("TapBody", 0);
}, cancellationToken);
```

Runtime overrides do not modify the player's settings and disappear when the game exits.

## Continue

- [Model control](./model-api)
- [Threads and streaming updates](./threading)
- [Bundle models with your Mod](./packs)
- [Public API reference](../reference/api)

`Tools/ApiConsumerExample` in the repository is a buildable example.
