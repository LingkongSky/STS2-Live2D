# Mod 五分钟接入

本页完成最小编译期引用、运行时依赖声明和模型控制。`Live2D.Api` 命名空间提供第三方公共 API。

## 1. 添加编译期引用

推荐使用 ref-only NuGet 包：

```xml
<PackageReference Include="STS2.Live2D" Version="0.6.1" />
```

在同一工作区开发时也可以引用源码：

```xml
<ProjectReference Include="..\STS2-Live2D\Live2D.csproj"
                  Private="false"
                  />
```

两种方式都使用编译期引用程序集，运行时由清单中的 `Live2D` Mod 依赖提供。

## 2. 声明运行时依赖

在使用者 Mod 清单中声明 Live2D：

```json
{
  "dependencies": [
    { "id": "Live2D", "min_version": "0.6.1" }
  ]
}
```

玩家安装 Live2D 运行时。Live2D 自身声明 RitsuLib 间接依赖。

## 3. 获取模型

玩家模型以稳定模型 ID 和场景区分：

```csharp
using Live2D.Api;

var model = Live2DApi.GetModel("model-id", Live2DScene.MainMenu);
if (model is null)
    return;

await model.WaitUntilAvailableAsync(cancellationToken);
```

句柄是稳定对象。设置刷新、窗口变化或场景重建底层节点后，同一句柄会自动绑定到新节点。

## 4. 在主线程控制模型

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

运行期覆盖属于游戏会话，玩家的 `settings.json` 保持原值。模型节点重建时，句柄会恢复会话覆盖。

## 5. 选择下一条路径

- [完整控制模型](./model-api)
- [后台线程与高频输入](./threading)
- [随自己的 Mod 分发模型](./packs)
- [公共 API 参考](../reference/api)

仓库中的 `Tools/ApiConsumerExample` 提供完整的接入源码示例。
