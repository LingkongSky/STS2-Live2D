# 自带模型 Pack

其他 Mod 可以把 `.live2dpack` 或 `.livepck` 放进自己的 PCK，再由 Live2D 运行时读取。两种扩展名内容相同。

## 只读注册还是持久导入

| 目标 | API | 是否写入玩家模型库 |
| --- | --- | --- |
| 模型属于你的 Mod，由代码管理 | `RegisterPack` | 否 |
| 让玩家长期编辑和管理模型 | `ImportPack` | 是 |

属于其他 Mod 的角色资源应优先使用只读注册。

## 注册并创建实例

```csharp
var pack = Live2DApi.RegisterPack(
    ownerModId: "MyMod",
    packagePath: "res://MyMod/live2d/characters.live2dpack");

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

实例身份由 `OwnerModId / PackId / Scene / InstanceId` 组成。相同身份和模型的重复创建是幂等操作；同一身份指向
不同 `ModelKey` 会被拒绝。

## 生命周期

```csharp
model.Destroy();  // 只销毁该运行时实例
pack.Unregister(); // 移除该 Pack 的全部实例并释放会话缓存
```

两者都不会删除玩家已经导入的模型。重复注册相同 `OwnerModId + PackId` 和相同内容会返回已有句柄；同一身份注册
不同内容会报错。

## 路径与内存数据

注册和导入都支持：

- 操作系统文件路径。
- `res://` 与 `user://` 路径。
- `ReadOnlyMemory<byte>` 内存数据。

PCK 导出时必须确保 Pack 文件被包含在使用者 Mod 的资源中。完整归档结构和安全限制见
[Pack 格式参考](../reference/pack-format)。
