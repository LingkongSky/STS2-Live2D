# 测试与发布

发布检查分为文档、编译、API 使用、渲染、PCK 和产物六层。任何一层失败都不应发布。

## 文档检查

```powershell
.\Tools\check-docs.ps1
Push-Location .\docs
npm ci
npm run build
Pop-Location
```

检查内容包括中英日页面完整性、内部链接、Mod/API/Pack/Schema 版本、站点构建和已删除旧项目的残留引用。

## 编译

```powershell
dotnet build .\Live2D.csproj `
  -c Release `
  -p:Live2DCopyToGame=false
```

使用者输出目录不得包含 `Live2D.dll`。

## 渲染烟雾测试

```powershell
.\Tools\run-render-smoke.ps1 `
  -GodotPath "C:\Tools\Godot\Godot_v4.5.1-stable_mono_win64_console.exe" `
  -Sts2Dir "D:\SteamLibrary\steamapps\common\Slay the Spire 2" `
  -CaptureImage
```

测试覆盖全部混合模式、三种裁切几何、颜色 Shader、公共渲染参数、主线程调度、高频合并队列、异步等待和
Parameter/Part 写入。带 `CaptureImage` 时还会在当前 GPU 上完成真实绘制。

## 导出 PCK

```powershell
.\Tools\export-live2d-pck.ps1 `
  -Configuration Release `
  -Sts2Dir "D:\SteamLibrary\steamapps\common\Slay the Spire 2"
```

脚本使用 Godot 4.5.1 Mono 导出 `Live2D.pck`，在隔离工程中验证 Shader `10/10`，然后构建并部署运行时文件。

## Pack 流程

发布前使用真实模型 Pack 完成一次：

1. 从文件或内存注册 Pack。
2. 创建主菜单与游戏内实例。
3. 验证动作、滤镜和场景切换。
4. 销毁实例并注销 Pack。
5. 持久导入同一 Pack，验证重复检测。

## NuGet

NuGet 采用与 JMC `modPublish` 相同的“预制发布目录”思路。公共 API 或 XML 注释变化后，先在有游戏安装的本机刷新提交到仓库的引用资产：

```powershell
.\Tools\update-nuget-reference.ps1 `
  -Sts2Dir "D:\Program Files\Steam\steamapps\common\Slay the Spire 2"

git diff -- NuGet/package/ref/net9.0
.\Tools\pack-nuget.ps1 -OutputDirectory artifacts

.\Tools\test-nuget-package.ps1 `
  -PackagePath .\artifacts\STS2.Live2D.0.4.1.nupkg `
  -ExpectedVersion 0.4.1
```

包内只允许：

- `ref/net9.0/Live2D.dll` 引用程序集与 `Live2D.xml`。
- README、第三方声明和 `docs/content` 下的正式 Markdown 文档。

不得包含 `lib/` 运行时 DLL。验证脚本会创建一次性使用者项目，编译完整 API 示例，并确认输出中不会复制 `Live2D.dll`。

`NuGet/package/ref/net9.0` 是 NuGet 的预制发布目录，其中的 `Live2D.dll` 必须是元数据引用程序集，不能替换成运行时 DLL。刷新后应把 DLL、XML 和对应源码放在同一个提交中。

推送与项目版本一致的 `v*` 标签后，`publish-nuget.yml` 只打包并验证仓库中已经提交的引用资产，不编译 Live2D、不读取游戏目录，也不访问 `STS2-API-Signatures`。验证通过后通过 NuGet Trusted Publishing 的 OIDC 身份发布。NuGet.org 策略须绑定该工作流和 `nuget` environment；不再需要 `STS2_SIGNATURES_TOKEN`。

## 发布清单

1. `Live2D.json` 的游戏和 RitsuLib 最低版本正确。
2. Mod manifest、项目版本和 `Entry.ModVersion` 一致。
3. 中英日文档页面齐全、无死链，版本数字一致。
4. Release 构建与使用者示例均通过。
5. GPU 烟雾测试通过。
6. PCK 隔离验证为 Shader `10/10`。
7. NuGet 内容符合 ref-only 约束。
8. 部署 DLL 与 Release 输出 SHA-256 一致。
9. 完全重启游戏，人工检查主菜单、地图、战斗、设置、导入与快捷键。

## 文档网站

推送 `main` 后，GitHub Actions 使用项目根目录的 lockfile 构建 VitePress，并把静态产物部署到 GitHub Pages。
站点只维护当前实现，不生成旧版本页面。
