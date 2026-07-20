# 配置结构

Live2D 使用 RitsuLib 全局数据存储，键为 `settings`，文件名为 `settings.json`。当前 `SchemaVersion` 为 `6`。
通常应通过游戏内设置修改，不建议在运行时手工编辑。

## 解析规则

```text
程序默认值 → Global → 单模型 Overrides 的非 null 字段
```

`null` 表示继承。Filter 与 Mask 按完整对象覆盖，不逐个子字段混合。

## 顶层结构

```json
{
  "SchemaVersion": 6,
  "Global": {
    "Hotkeys": {},
    "MainMenu": {},
    "InGame": {},
    "Playback": {},
    "Rendering": {}
  },
  "Models": [],
  "RemovedExternalModelIds": []
}
```

## Global

### Hotkeys

`ToggleVisibility` 保存全部 Live2D 模型的显示总开关键绑定。

### MainMenu / InGame

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `Visible` | bool | 是否显示 |
| `Anchor` | enum | 九点锚点 |
| `OffsetX` / `OffsetY` | float | 1920×1080 参考画布偏移 |
| `Scale` | float | 等比缩放 |
| `RotationDegrees` | float | 顺时针角度 |
| `Opacity` | float | 限制到 `0..1` |
| `Layer` | int | Godot `ZIndex` |
| `MouseInteraction` | bool | 是否接收鼠标输入 |

锚点枚举包括四角、四边中心和正中心。地图与战斗都使用 `InGame`。

### Playback

| 字段 | 默认值 |
| --- | --- |
| `Speed` | `1`；负值按 0 处理 |
| `EnablePhysics` | `true` |
| `EnablePose` | `true` |
| `AutoPlayIdle` | `true` |
| `ActionCooldownSeconds` | `0.1` |

### Rendering

| 字段 | 说明 |
| --- | --- |
| `MaskViewportSize` | Cubism Drawable 蒙版纹理尺寸；0 自动 |
| `BlendMode` | `Normal`、`Add`、`Subtract`、`Multiply`、`PremultipliedAlpha` |
| `Filter` | 整模型合成后的颜色滤镜 |
| `Mask` | 模型局部坐标画布裁切 |

Filter 范围与公共 API 相同。Mask 包含 `Type`、`X/Y`、`Width/Height`、`CornerRadius` 和
`SegmentsPerCorner`。非法持久值会被限制到有效范围或恢复安全默认值。

## Models

每个模型包含：

| 字段 | 说明 |
| --- | --- |
| `Id` | 管理模型稳定 ID |
| `Enabled` | 持久单模型总开关；默认为 `true` |
| `DisplayName` | 界面显示名称 |
| `ModelRelativePath` | 受管理目录中的 model3 相对路径 |
| `SourcePath` | 原始导入路径记录 |
| `ContentHash` | 内容身份与重复检测 |
| `ImportedAt` / `DisplayOrder` | 导入时间与排序 |
| `Overrides` | 场景、播放和渲染覆盖 |
| `AvailableActions` | Motion/Expression 元数据 |
| `ActionBindings` | 快捷键绑定 |

`AvailableActions.Kind` 当前为 `0`（Motion）或 `1`（Expression）。动作绑定还包含场景范围、循环标志与
RitsuLib KeyBinding。

`RemovedExternalModelIds` 记录用户从模型库删除的提供方模型身份，防止相同 Pack 再次注册时立即将其加入。游戏内的恢复按钮会清空对应排除记录。

## 运行时覆盖

`ILive2DModelHandle.Apply`、`QueueUpdate` 与 `Set*` 只修改当前会话，不写入 `settings.json`。句柄会在节点重建时
恢复这些覆盖；Parameter 与 Part 是更短生命周期的动态值，不属于配置，也不会恢复。
