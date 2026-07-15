# 模型控制

`ILive2DModelHandle` 提供稳定身份、可用状态、模型快照和控制方法。直接访问实时 Godot 节点的操作必须在主线程执行。

## 部分更新

一次设置多个字段时使用 `Update` 或 `Apply`：

```csharp
model.Update(update =>
{
    update.Position = new Vector2(1180f, 720f);
    update.Scale = new Vector2(-0.42f, 0.42f); // 水平翻转
    update.Opacity = 0.9f;
    update.Layer = 20;
    update.PlaybackSpeed = 1.1f;
});
```

未赋值字段不会变化。`Snapshot` 返回当前变换、显示、播放和渲染状态。

## 动作与表情

```csharp
var wave = model.Actions.First(action => action.DisplayName == "Wave");
model.PlayAction(wave, loop: false);

model.PlayMotion("TapBody", 0);
model.StopMotion();
model.SetExpression("smile");
model.ClearExpression();
```

`Actions` 在底层场景实例不可用时仍可读取。`MotionFinished` 和 `MotionEvent` 都在 Godot 主线程触发。

## Parameter 与 Part

```csharp
var parameters = model.GetParameters();
model.SetParameters(new Dictionary<string, float>
{
    ["ParamAngleX"] = 15f,
    ["ParamMouthOpenY"] = 0.6f,
});

model.SetPartOpacity("PartArmL", 0.5f);
```

批量写入会先验证所有 ID，再应用整批，不会只成功一部分。Parameter 值会限制到模型声明范围，Part 透明度会限制到
`0..1`。这些动态值不会持久化，也不会在模型重建后恢复。

## 滤镜与裁切

```csharp
model.SetBlendMode(Live2DBlendMode.Add);
model.SetFilter(new Live2DFilterSettings
{
    Tint = new Color(0.85f, 0.95f, 1f),
    Brightness = 0.05f,
    Contrast = 1.1f,
    Saturation = 0.8f,
    HueShiftDegrees = 10f,
});

model.SetMask(new Live2DMaskSettings
{
    Type = Live2DMaskType.RoundedRectangle,
    Rect = new Rect2(-420f, -760f, 840f, 920f),
    CornerRadius = 48f,
});
```

恢复默认值：

```csharp
model.ResetFilter();
model.ClearMask();
model.SetBlendMode(Live2DBlendMode.Normal);
```

## 生命周期

- `IsAvailable` 表示当前场景中是否存在已绑定实例。
- `WaitUntilAvailableAsync` 和 `WaitUntilUnavailableAsync` 提供可取消等待。
- `BecameAvailable` / `BecameUnavailable` 适合持续监听。
- `Destroy()` 只允许销毁由 Pack 创建且 `CanDestroy` 为 true 的运行时实例。

等待方法的 `await` 后不保证仍处于 Godot 主线程；继续执行模型命令时仍应使用 `InvokeAsync`。
