# 公共 API 参考

当前运行时版本为 `Live2DApi.RuntimeVersion == "0.4.1"`，能力版本为 `Live2DApi.RuntimeApiVersion == 4`。
只有 `Live2D.Api` 命名空间属于稳定第三方 API；`Live2D.Scripts.*` 与 gd_cubism 类型是实现细节。

## 线程规则

| API | 调用线程 |
| --- | --- |
| `Post`、`InvokeAsync` | 任意线程 |
| `GetModels`、`GetModel`、`TryGetModel` | 任意线程 |
| 句柄身份、`Actions`、`IsAvailable`、异步等待 | 任意线程 |
| `QueueUpdate`、Parameter/Part Queue API | 任意线程 |
| Pack 导入、注册、创建、注销 | Godot 主线程 |
| `Snapshot`、`Apply`、`Set*`、播放、动态值读取、`Destroy` | Godot 主线程 |

所有 API 事件都在 Godot 主线程触发。异步等待完成后的 continuation 不保证仍在主线程。

## Live2DApi

### 运行时状态

| 成员 | 说明 |
| --- | --- |
| `RuntimeApiVersion` | 当前 API 能力版本 |
| `RuntimeVersion` | 当前 Live2D Mod 版本 |
| `IsDispatcherReady` | 主线程调度器是否就绪 |
| `IsMainThread` | 当前代码是否位于记录的 Godot 主线程 |
| `ModelAvailable` | 任意模型首次进入可用状态时触发 |

### 查询模型

- `GetModels()`：返回会话中全部稳定句柄的数组快照。
- `GetModels(ownerModId)`：按所有者过滤。
- `GetModel(modelId, scene)`：找不到时返回 null。
- `TryGetModel(...)`：无异常查询。

### 调度

- `Post(Action)`：不等待；异常写入日志。
- `InvokeAsync(Action, CancellationToken)`：返回完成、异常或取消状态。
- `InvokeAsync<T>(Func<T>, CancellationToken)`：在主线程计算并返回值。

调度器暂停时仍处理队列，每帧最多执行 512 个工作项。

### Pack

- `ImportPack(path/data)`：持久导入玩家模型库，返回导入和重复跳过数量。
- `RegisterPack(ownerModId, path/data)`：只读注册其他 Mod 的 Pack。
- 路径支持操作系统路径、`res://`、`user://`；数据重载接受 `ReadOnlyMemory<byte>`。

`res://`、`user://` 与内存数据会先复制到操作系统临时文件，导入或注册结束后无论成功失败都会删除。
只读注册需要保留的资源会复制到会话缓存，不依赖该临时文件继续存在。

## ILive2DPackHandle

| 成员 | 说明 |
| --- | --- |
| `OwnerModId` | 注册方 Mod ID |
| `PackId` | manifest 的稳定 `PackageId` |
| `Name` | Pack 显示名称 |
| `IsRegistered` | 是否仍处于注册状态 |
| `Models` | 模型元数据与动作列表 |
| `CreateModel` | 创建或获取幂等运行时实例 |
| `Unregister` | 移除该 Pack 的全部实例 |

标识符最多 128 个字符，不能包含控制字符。未提供 `InstanceId` 时会生成随机 ID。

## ILive2DModelHandle

### 身份与状态

`ModelId`、`OwnerModId`、`PackId`、`ModelKey`、`InstanceId` 和 `Scene` 描述稳定身份。
`IsAvailable` 表示底层场景实例是否存在，`CanDestroy` 表示是否允许销毁。

场景切换或节点重建不会使句柄失效。不可用期间仍可读取身份与 `Actions`；`Snapshot` 必须在主线程读取，
此时返回最后一次已知状态。

- `BecameAvailable` / `BecameUnavailable`：持续监听绑定变化。
- `WaitUntilAvailableAsync` / `WaitUntilUnavailableAsync`：无订阅竞态的可取消等待。
- `Snapshot`：当前变换、显示、播放和渲染快照。
- `Destroy()`：销毁 Pack 运行时实例。

### 状态更新

`Apply(update)` 应用部分更新；`Update(configure)` 创建更新并调用配置委托；两者都要求主线程。
`QueueUpdate` 可从任意线程提交并按字段合并。`configure` 委托在调用线程立即执行，因此只应填写更新数据，不应访问 Godot 节点。

| `Live2DModelUpdate` 字段 | 校验与行为 |
| --- | --- |
| `Position` | 两个分量必须有限 |
| `Scale` | 分量有限且非 0；负值可翻转 |
| `RotationDegrees` | 必须有限 |
| `Opacity` | 限制到 `0..1` |
| `Visible` | 根节点可见性 |
| `Layer` | Godot `ZIndex` |
| `PlaybackSpeed` | 负值按 0 处理 |
| `PhysicsEnabled` / `PoseEnabled` | Cubism 物理和 Pose |
| `MaskViewportSize` | 负值按 0 处理 |
| `BlendMode` | 必须是已定义枚举值 |
| `Filter` | 整模型颜色滤镜 |
| `Mask` | 模型局部坐标裁切 |

对应便捷方法包括 `SetPosition`、`SetScale`、`SetUniformScale`、`SetRotation`、`SetOpacity`、`SetVisible`、
`SetLayer`、播放设置和所有渲染设置。

### 播放

- `Actions`：模型声明的 Motion/Expression，只读。
- `PlayAction` / `PlayMotion` / `StopMotion`。
- `SetExpression` / `ClearExpression`。
- `MotionFinished` / `MotionEvent`。

`Snapshot.Playback` 描述当前命令状态。

### Parameter 与 Part

- `GetParameters` / `TryGetParameter` / `SetParameter(s)` / `QueueParameter(s)`。
- `GetParts` / `TryGetPart` / `SetPartOpacity/Opacities` / 对应 Queue API。

未知 ID 抛出 `KeyNotFoundException`。批量同步写入先验证全部 ID。队列按不区分大小写的 ID 合并。

## 渲染范围

| 滤镜字段 | 范围 / 默认值 |
| --- | --- |
| `Tint` | RGBA 有限；白色 |
| `Brightness` | `-1..1`；0 |
| `Contrast` | `0..4`；1 |
| `Saturation` | `0..4`；1 |
| `Grayscale` | `0..1`；0 |
| `HueShiftDegrees` | 任意有限角度；0 |
| `Invert` | `0..1`；0 |
| `Gamma` | `0.01..10`；1 |

蒙版类型为 `None`、`Rectangle`、`Ellipse`、`RoundedRectangle`。启用时 Rect 宽高必须为正，
`CornerRadius >= 0`，`SegmentsPerCorner` 范围为 `2..64`。

## 常见异常

| 异常 | 常见原因 |
| --- | --- |
| `ArgumentException` | 空白、过长或含控制字符的 ID |
| `ArgumentOutOfRangeException` | 非有限数值、非法范围或枚举 |
| `InvalidOperationException` | 错误线程、未就绪、不可用、身份冲突或已注销 |
| `KeyNotFoundException` | Parameter/Part 不存在 |
| `InvalidDataException` / `IOException` | Pack 格式或文件访问失败 |
| `OperationCanceledException` | 等待或调度被取消 |

需要观察后台异常时使用 `InvokeAsync`；`Post` 和 queued 状态流只记录执行期异常。
