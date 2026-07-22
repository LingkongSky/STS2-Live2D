# 自带模型 Pack

其他 Mod 可以把 `.live2dpack` 放进自己的 PCK，再注册到 Live2D 统一模型库。模型资源包使用 `.live2dpack` 后缀。

## 注册

```csharp
var pack = Live2DApi.RegisterPack(
    ownerModId: "MyMod",
    packagePath: "res://MyMod/live2d/characters.live2dpack");
```

注册后，Pack 中的模型直接出现在 Live2D“模型管理”页。显示、布局、渲染、动作、快捷键和场景实例全部由 Live2D 管理；
提供方 Mod 将模型设置页、快捷键和实例控制委托给 Live2D。

资源归提供方 Mod 所有并保存在会话缓存；模型库导出面向玩家导入的资源。玩家可以删除并恢复模型库中的本地登记项，
提供方资源保持原位。Live2D 持久保存玩家配置；提供方未加载或玩家禁用模型时，模型进入不可用状态并保留配置。

提供方可保留角色专属行为，例如开场动作、剧情反应或状态联动。实现 `ILive2DProviderLifecycleHook`，并在注册 Pack 前注册 Hook，
与 Live2D 的统一实例系统协作：

```csharp
sealed class CharacterHook : ILive2DProviderLifecycleHook
{
    public void OnModelAvailable(ILive2DModelHandle model)
    {
        if (model.Scene == Live2DScene.MainMenu)
            model.PlayMotion("Intro", 0);
    }

    public void OnModelUnavailable(ILive2DModelHandle model)
    {
        // 取消仍在等待的角色专属异步行为。
    }
}

var lifecycle = Live2DApi.RegisterProviderHook("MyMod", new CharacterHook());
var pack = Live2DApi.RegisterPack("MyMod", "res://MyMod/live2d/characters.live2dpack");
```

Hook 分为 `OnPackRegistered`、`OnModelAvailable`、`OnModelUnavailable`、`OnPackUnregistered` 四个阶段。
Hook 注册时会按顺序回放已有 Pack 和可用模型。调用方保存返回的 `IDisposable`，并在自身生命周期结束时调用 `Dispose()`。

## 生命周期

`pack.Unregister()` 会注销资源并刷新模型库。玩家配置会保留，以便下次注册相同 `OwnerModId + PackId + ModelKey` 时恢复。
重复注册相同身份和相同内容会返回已有句柄；同一身份注册不同内容会报错。

## 路径与导出

注册支持操作系统路径、`res://`、`user://` 和 `ReadOnlyMemory<byte>`。PCK 导出时必须显式包含 Pack：

```ini
export_filter="resources"
include_filter="MyMod/live2d/*.live2dpack"
exclude_filter="artifacts/**,Scripts/**,MyMod/src/**"
```

`settings/models.json` 的根节点必须是数组，即使只有一个模型也必须写成 `[{ ... }]`。完整结构见
[Pack 格式参考](../reference/pack-format)。
