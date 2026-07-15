# STS2 Live2D

《杀戮尖塔 2》的 Live2D 运行时 Mod，同时为其他 Mod 提供稳定的模型控制 API。

> 当前为开发预览版本。运行时 Mod 版本：`0.4.0`；公共 API 版本：`4`。

## 能力

- 导入和管理 Live2D Cubism 模型。
- 分别配置主菜单与游戏内的位置、大小、旋转、透明度和显示状态。
- 播放 Motion、Expression，绑定快捷键并控制 Parameter 与 Part。
- 支持混合模式、常用颜色滤镜和画布裁切。
- 通过 NuGet 或 `ProjectReference` 为其他 Mod 提供编译期 API。
- 注册其他 Mod 自带的 `.live2dpack` / `.livepck`，不修改玩家模型库。

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
<PackageReference Include="STS2.Live2D" Version="0.4.0" />
```

NuGet 只提供编译期引用，不会复制第二份 `Live2D.dll`。使用者仍需在 Mod 清单中声明 Live2D
运行时依赖。完整示例见 [Mod 接入文档](https://lingkongsky.github.io/STS2-Live2D/integration/getting-started)。

## 构建

```powershell
dotnet build -p:Live2DCopyToGame=false
dotnet pack -c Release -p:Live2DCopyToGame=false
```

## LICENSE
[MIT](LICENSE)
