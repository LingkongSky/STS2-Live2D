# 开始使用

本页帮助玩家确认运行条件、安装开发构建并导入第一个模型。

::: info 安装范围
玩家安装 `Live2D` 运行时 Mod；第三方 Mod 作者使用 `STS2.Live2D` NuGet 编译期 API。
:::

## 运行要求

- Windows x86_64。
- 《杀戮尖塔 2》`0.107.1` 或更高版本。
- STS2 RitsuLib `0.4.56` 或更高版本。
- 有效的 Live2D Cubism 3/4 模型资源。

## 安装开发构建

安装开发构建需要 Godot 4.5.1 Mono 和 .NET 9 SDK。Godot 负责导出 Shader PCK，.NET CLI 负责生成完整 Mod 目录：

```powershell
$Godot = "C:\Tools\Godot\Godot_v4.5.1-stable_mono_win64_console.exe"
$env:STS2_DIR = "D:\SteamLibrary\steamapps\common\Slay the Spire 2"

& $Godot --headless --editor --path $PWD --quit
& $Godot --headless --path $PWD --export-pack Live2D "$PWD\Live2D.pck"
dotnet publish .\Live2D.csproj -c Release -o .\artifacts\Live2D -p:BundleMod=true
```

将 `artifacts/Live2D` 整个目录复制为游戏的 `mods/Live2D`。该目录至少应包含：

```text
Live2D.json
Live2D.dll
Live2D.pck
addons/gd_cubism/
```

`dotnet build` 用于编译检查。安装或更新使用同一次 `dotnet publish` 生成的完整目录，随后完全退出并重启游戏。

## 导入第一个模型

1. 从主菜单打开“Live2D 设置”。
2. 进入“模型管理”。
3. 选择“添加 Live2D 模型”。
4. 选择模型的 `.model3.json` 文件。
5. 等待模型出现在模型库中，然后选择“预览并调整”。

导入器会把 `.moc3`、纹理、动作、表情、物理和 Pose 等依赖复制到受管理目录，并由模型管理页维护这些文件。

## 完成首次配置

在预览页选择“主菜单”或“游戏内”，然后：

- 左键拖动模型调整位置。
- 使用滚轮调整缩放。
- 使用 `Shift + 滚轮` 调整旋转。
- 切换预览分辨率检查常见屏幕比例。
- 选择“保存修改”。

地图和战斗共用“游戏内”配置。主菜单打开其他子页面或模态窗口时，主菜单模型会临时隐藏。

## 下一步

- [整理和配置多个模型](./models)
- [调整透明度、滤镜和裁切](./appearance)
- [设置动作与快捷键](./actions)
- [解决模型不显示等问题](./troubleshooting)
