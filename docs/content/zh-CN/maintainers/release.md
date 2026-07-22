# 测试与发布

发布检查覆盖文档、编译、API 使用、渲染、PCK 和产物六层，全部通过后创建版本标签。

## 文档检查

```powershell
Push-Location .\docs
npm ci
npm run build
Pop-Location
```

检查中英日页面、内部链接、Mod/API/Pack/Schema 版本和站点构建结果。

## 编译

`dotnet build` 执行编译检查；`dotnet publish` 生成发布产物。使用者输出通过 ref-only 引用保持运行时依赖边界。

## 渲染烟雾测试

```powershell
$Godot = "C:\Tools\Godot\Godot_v4.5.1-stable_mono_win64_console.exe"
$env:STS2_DIR = "D:\SteamLibrary\steamapps\common\Slay the Spire 2"
dotnet build .\Tools\RenderSmoke\Live2DRenderSmoke.csproj
& $Godot --headless --path .\Tools\RenderSmoke --rendering-method gl_compatibility
```

测试覆盖全部混合模式、三种裁切几何、颜色 Shader、公共渲染参数、主线程调度、高频合并队列、异步等待和
Parameter/Part 写入。带 `CaptureImage` 时还会在当前 GPU 上完成真实绘制。

## 导出 PCK

```powershell
& $Godot --headless --editor --path $PWD --quit
& $Godot --headless --path $PWD --export-pack Live2D "$PWD\Live2D.pck"
& $Godot --headless --path .\Tools\PckVerifier `
  --script verify_pck.gd -- "$PWD\Live2D.pck"
dotnet publish .\Live2D.csproj -c Release -o .\artifacts\Live2D -p:BundleMod=true
```

Godot 4.5.1 Mono 导出 `Live2D.pck`，隔离工程验证 Shader `10/10`，随后 .NET CLI 输出完整 Mod 目录。
发布或本地安装时复制同一次 publish 生成的整个 `artifacts/Live2D`。

## Pack 流程

发布前使用真实模型 Pack 完成一次：

1. 从文件或内存注册 Pack。
2. 在主菜单与游戏内场景绑定模型，并验证提供方生命周期 Hook 顺序。
3. 验证动作、滤镜和场景切换。
4. 验证模型失效阶段并注销 Pack。
5. 持久导入同一 Pack，验证重复检测。

## NuGet

NuGet 使用仓库中的预制引用资产。公共 API 或 XML 注释变化后，在有游戏安装的本机刷新引用资产：

```powershell
dotnet build .\Live2D.csproj -c Release -t:RefreshNuGetReference
git diff -- NuGet/package/ref/net9.0
dotnet pack .\NuGet\STS2.Live2D.Package.csproj -c Release -o .\artifacts
```

包内容：

- `ref/net9.0/Live2D.dll` 引用程序集与 `Live2D.xml`。
- README、第三方声明和 `docs/content` 下的正式 Markdown 文档。

发布工作流验证包结构为 ref-only，并通过一次性使用者项目确认运行时 DLL 由 Mod 依赖提供。

`NuGet/package/ref/net9.0` 是 NuGet 的预制发布目录，其中的 `Live2D.dll` 是元数据引用程序集。刷新后的 DLL、XML 和对应源码进入同一个提交。

推送与项目版本一致的 `v*` 标签后，`publish-nuget.yml` 打包并验证仓库中的引用资产，再通过 NuGet Trusted Publishing
OIDC 发布。NuGet.org 策略绑定该工作流和 `nuget` environment。

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
站点发布 `main` 分支文档。
