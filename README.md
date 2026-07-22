# STS2 Live2D

《杀戮尖塔 2》的 Live2D 运行时 Mod，同时为其他 Mod 提供稳定的模型控制 API。

运行时 Mod 版本：`0.6.0`；公共 API 版本：`9`。

## 能力

- 导入和管理 Live2D Cubism 模型。
- 分别配置主菜单与游戏内的位置、大小、旋转、透明度和显示状态。
- 播放 Motion、Expression，绑定快捷键并控制 Parameter 与 Part。
- 支持混合模式、常用颜色滤镜和画布裁切。
- 通过 NuGet 或 `ProjectReference` 为其他 Mod 提供编译期 API。
- 将其他 Mod 自带的 `.live2dpack` 注册到统一模型库。

## 文档

- [简体中文](https://lingkongsky.github.io/STS2-Live2D/) · [English](https://lingkongsky.github.io/STS2-Live2D/en/) · [日本語](https://lingkongsky.github.io/STS2-Live2D/ja/)
- [玩家指南](https://lingkongsky.github.io/STS2-Live2D/guide/getting-started)
- [Mod 五分钟接入](https://lingkongsky.github.io/STS2-Live2D/integration/getting-started)
- [公共 API 参考](https://lingkongsky.github.io/STS2-Live2D/reference/api)
- [开发与发布](https://lingkongsky.github.io/STS2-Live2D/maintainers/development)

本地启动文档站：

```powershell
cd docs
npm install
npm run dev
```

## 最小引用

```xml
<PackageReference Include="STS2.Live2D" Version="0.6.0" />
```

NuGet 提供编译期引用程序集。使用者在 Mod 清单中声明 `Live2D` 运行时依赖；完整示例见
[Mod 接入文档](https://lingkongsky.github.io/STS2-Live2D/integration/getting-started)。


## 构建

构建会从 Steam 自动发现游戏目录。若游戏位于无法自动识别的库，可设置 `STS2_DIR`
环境变量，或在命令行直接传入 `Sts2Dir`：

正式 Mod 产物由 Godot 导出资源 PCK，再由 `dotnet publish` 收集程序集、manifest、PCK 和 Cubism 原生文件：

```powershell
$Godot = "D:\program\Godot\Godot_v4.5.1-stable_mono_win64_console.exe"
$env:STS2_DIR = "D:\SteamLibrary\steamapps\common\Slay the Spire 2"

& $Godot --headless --editor --path $PWD --quit
& $Godot --headless --path $PWD --export-pack Live2D "$PWD\Live2D.pck"
dotnet publish .\Live2D.csproj -c Release -o .\artifacts\Live2D -p:BundleMod=true
```

将同一次 `dotnet publish` 生成的 `artifacts/Live2D` 整个目录复制到游戏的 `mods/Live2D`。
`dotnet build` 用于编译检查，`dotnet publish` 生成完整部署产物。

```powershell
dotnet build .\Live2D.csproj -c Release -t:RefreshNuGetReference
dotnet pack .\NuGet\STS2.Live2D.Package.csproj -c Release -o .\artifacts
```

## LICENSE

[MIT](https://github.com/LingkongSky/STS2-Live2D/blob/main/LICENSE)

[第三方声明](THIRD-PARTY-NOTICES.md)
