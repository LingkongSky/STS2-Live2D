# 动作与快捷键

模型导入时会收集 model3 中声明的 Motion 和 Expression。VTube Studio 模型还会从 `.vtube.json`、`expressions` 和
`animations` 目录补充动作与表情，并显示在模型详细设置的“快捷键”页。

## Motion 与 Expression

- Motion 是带时间轴的动作，例如 Idle、TapBody 或 Wave。
- Expression 是表情状态，例如 smile 或 angry。
- 自动 Idle 会寻找名为 `Idle` 的 Motion Group。
- Physics 和 Pose 可分别启用或关闭。

如果动作列表为空，先检查 `.model3.json` 是否实际声明了 Motion 或 Expression，以及引用文件是否存在。

## 绑定快捷键

1. 打开“模型管理”。
2. 在目标模型上选择“详细设置”。
3. 进入“快捷键”页。
4. 为动作选择按键，并决定主菜单、游戏内和循环范围。
5. 保存修改。

快捷键支持单独的字母、数字 `0–9`、功能键和组合键。

同一模型可以为多个动作设置相同快捷键；触发时这些动作可能同时执行，界面会显示冲突提示。

## 全局显示快捷键

“全局配置”中的显示快捷键是临时显示总控。第一次触发临时隐藏全部已启用模型，再次触发恢复
各模型原有的场景显示状态；模型配置和各自的启用状态保持不变。

## 播放设置

- 播放速度小于 0 时按 0 处理。
- 动作冷却用于避免一次输入在短时间内重复触发。
- 循环 Motion 会持续播放，直到停止或被其他播放命令替换。
- Motion、Expression、Physics 和 Pose 都可能持续修改 Cubism Parameter。
