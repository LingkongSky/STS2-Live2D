# 故障排查

先完全重启游戏，然后在日志中搜索 `[Live2D]`。如果仍无法解决，按下面最接近的现象检查。

## Mod 没有加载

1. 确认游戏版本满足 `Live2D.json` 的 `min_game_version`。
2. 确认已安装清单要求的 RitsuLib。
3. 确认 `mods/Live2D` 同时存在 `Live2D.json`、`Live2D.dll`、`Live2D.pck` 和 gd_cubism 原生文件。
4. 更新 DLL 后完全退出并重启游戏。

## 模型全白或完全不显示

- 初始化日志应显示 Cubism Shader `10/10`；资源不完整时运行时不会安装场景补丁。
- 检查 `.model3.json` 引用的 `.moc3`、纹理和路径大小写。
- 恢复中性滤镜、正常混合模式，并暂时关闭画布裁切。
- 检查模型的目标场景、透明度、缩放和全局显示快捷键状态。

## 模型或蒙版模糊

- 先恢复正常混合、中性滤镜并关闭画布蒙版；该状态会直接绘制模型，不经过合成画布。
- 画布蒙版与 `MaskViewportSize` 是两套功能。只有 Cubism Drawable 自身的蒙版边缘模糊时才提高后者。
- `MaskViewportSize` 优先使用“自动”；固定 4096 会增加显存占用，但不会提高模型纹理本身的分辨率。
- 使用“预览并编辑”选择与实际窗口接近的参考分辨率，确认缩放后再判断清晰度。

## 蒙版参数看不到效果

- 确认已勾选模型蒙版覆盖，或点击“启用蒙版并适配模型边界”。
- 确认类型不是“无”。圆角半径只适用于圆角矩形，调整半径时界面会自动切换类型。
- 开启“在画布上直接编辑蒙版”，拖动移动蒙版、滚轮缩放，并观察滑条与数字是否同步。

## 模型只在部分场景出现

主菜单和游戏内使用独立配置；地图与战斗共用游戏内配置。主菜单打开其他子页面或模态窗口时模型会临时隐藏，
这不是配置丢失。

## 动作或表情没有出现

- Cubism 模型：确认 model3 中声明了对应 Motion/Expression。
- VTube Studio 模型：确认 `.vtube.json` 指向正确 model3，资源位于 `expressions` 或 `animations` 目录。
- 重新导入模型，让受管理副本刷新动作列表。
- 检查动作快捷键是否启用了当前场景。
- 检查是否存在相同快捷键或循环动作冲突。

## 模型显示“已丢失”

本地模型请恢复受管理目录中的资源；提供方模型请启用对应 Mod。Live2D 会保留模型配置，资源可用后自动恢复实例和快捷键。

## Pack 导入失败

- Pack 根目录必须直接包含 `manifest.json` 和 `settings/models.json`。
- 格式版本为 `FormatVersion = 1`、`SettingsSchemaVersion = 6`。
- 所有模型资源必须位于对应 `models/<OriginalId>/` 下。
- 绝对路径、`..`、符号链接、重复路径和异常压缩内容都会被拒绝。

如果日志包含 `JSON value could not be converted to List<Live2DModelConfig>`，请把 Pack 内的 `settings/models.json` 根节点改为数组。
同时检查启动日志中的实际加载路径，比较发布目录与 `mods/<ModId>` 内 DLL/PCK 的 SHA-256，并部署同一次发布的完整文件集。

完整规则见 [Pack 格式](../reference/pack-format)。

## 报告问题

请附上游戏版本、RitsuLib 版本、Live2D 版本、相关 `[Live2D]` 日志、问题出现的场景，以及模型或 Pack
是否可以公开。提交前请清理日志中的个人路径和其他敏感信息。
