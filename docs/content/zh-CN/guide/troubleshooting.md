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

## 模型只在部分场景出现

主菜单和游戏内使用独立配置；地图与战斗共用游戏内配置。主菜单打开其他子页面或模态窗口时模型会临时隐藏，
这不是配置丢失。

## 动作或表情没有出现

- 确认 model3 中声明了对应 Motion/Expression。
- 确认引用文件已经随模型一起导入。
- 检查动作快捷键是否启用了当前场景。
- 检查是否存在相同快捷键或循环动作冲突。

## Pack 导入失败

- Pack 根目录必须直接包含 `manifest.json` 和 `settings/models.json`。
- 当前只接受 `FormatVersion = 1` 与 `SettingsSchemaVersion = 6`。
- 所有模型资源必须位于对应 `models/<OriginalId>/` 下。
- 绝对路径、`..`、符号链接、重复路径和异常压缩内容都会被拒绝。

完整规则见 [Pack 格式](../reference/pack-format)。

## 报告问题

请附上游戏版本、RitsuLib 版本、Live2D 版本、相关 `[Live2D]` 日志、问题出现的场景，以及模型或 Pack
是否可以公开。不要公开包含个人路径或其他敏感信息的完整日志。
