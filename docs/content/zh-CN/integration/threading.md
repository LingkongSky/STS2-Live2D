# 线程与高频更新

Live2D 的场景树和 Cubism 节点属于 Godot 主线程。API 把“有顺序的命令”和“可合并的连续状态”分成两条路径。

## 普通异步调用

`InvokeAsync` 适合需要完成状态、返回值或异常的操作：

```csharp
await Live2DApi.InvokeAsync(() => model.PlayMotion("TapBody", 0), cancellationToken);
var snapshot = await Live2DApi.InvokeAsync(() => model.Snapshot, cancellationToken);
```

- 已在主线程时立即执行。
- 排队期间取消会阻止回调开始。
- 已经开始的回调不会被强制中断。
- 回调异常通过返回的 Task 重新抛出。

`Post` 是低频 fire-and-forget 通知；异常只写入 Live2D 日志：

```csharp
Live2DApi.Post(() => model.SetVisible(true));
```

初始化前先检查 `Live2DApi.IsDispatcherReady`。不要在主线程同步等待一个需要主线程执行的 Task。

## 连续状态流

位置跟踪、音频响应或面捕不应为每个采样创建 Task。使用 `QueueUpdate`：

```csharp
model.QueueUpdate(update =>
{
    update.Position = trackedPosition;
    update.RotationDegrees = trackedRotation;
    update.Opacity = trackedOpacity;
});
```

同一模型尚未执行的更新会合并：不同字段全部保留，同一字段最后提交值获胜。该接口适合不需要逐次确认的状态流。

## Parameter 与 Part 队列

```csharp
model.QueueParameters(new Dictionary<string, float>
{
    ["ParamAngleX"] = angleX,
    ["ParamMouthOpenY"] = mouthOpen,
    ["ParamEyeLOpen"] = leftEyeOpen,
    ["ParamEyeROpen"] = rightEyeOpen,
});

model.QueuePartOpacity("PartArmL", armOpacity);
```

ID 以不区分大小写的方式合并，同一 ID 最后值获胜。批次执行时模型不可用则丢弃，不会排队到下一次场景。

## 选择规则

| 场景 | 使用 |
| --- | --- |
| 播放动作、设置表情、销毁实例 | `InvokeAsync` |
| 需要返回值或捕获异常 | `InvokeAsync` |
| 低频且不关心结果 | `Post` |
| 连续位置、旋转、透明度、滤镜 | `QueueUpdate` |
| 面捕、口型、部件透明度 | Parameter/Part Queue API |

Motion、Expression、Physics 和 Pose 可能在后续帧继续修改 Parameter，需要持续驱动的输入源应持续提交采样。
