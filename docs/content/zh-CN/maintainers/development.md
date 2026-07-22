# 开发与架构

本页面向维护 STS2 Live2D 本体的开发者。第三方 Mod 作者应从 [Mod 五分钟接入](../integration/getting-started) 开始。

## 功能边界

运行时负责：

- 导入 `.model3.json` 及其完整依赖。
- 管理玩家模型和其他 Mod 的只读 Pack 实例。
- 在主菜单、地图和战斗中挂载模型宿主。
- 解析全局配置与单模型覆盖。
- 提供动作、变换、渲染、Parameter 和 Part API。
- 通过 RitsuLib 注册设置页面、快捷键和游戏补丁。

运行时接收制作完成的 Cubism 模型；发布包提供 Windows x86_64 原生库。

## 运行时结构

```text
RitsuLib 配置存储
    ↓
Live2DConfigResolver
    ↓
Live2DRuntimeManager
    ├─ MainMenu host
    ├─ Map host
    └─ Combat host
          ↓
    Live2DModelInstance
          ↓
      gd_cubism
```

`Live2DMainThreadDispatcher` 作为 `SceneTree.Root` 的常驻子节点，每帧限量使用并发队列。第三方代码通过
`Live2DApi.InvokeAsync`、`Post` 或各类 Queue API 进入运行时。

## 配置职责

运行值始终按以下顺序解析：

```text
程序默认值 → 全局配置 → 单模型非空覆盖
```

`Live2DConfigResolver` 统一实现继承规则，供运行时、设置界面、预览页和 Pack 共用。
配置 Schema 固定为 `6`。

## 场景与布局

- 主菜单使用独立宿主和配置。
- 地图与战斗共用 `InGame` 配置，但使用各自宿主。
- 主菜单子页面或模态窗口打开时，主菜单模型临时隐藏。
- 保存位置使用 1920×1080 参考坐标，运行时按实际视口短边等比换算。
- 视口变化或配置刷新会重建节点；稳定 API 句柄必须重新绑定并恢复会话覆盖。

## 模型仓库

导入器校验并复制 `.moc3`、纹理、Motion、Expression、Physics、Pose 和其他 model3 依赖。VTube Studio 补充资源会合并到
受管理 model3 副本。资源暂时缺失时保留模型配置，由界面显示“模型已丢失”，运行时跳过实例和快捷键注册。

`examples/` 保存使用通用导入逻辑的测试模型，并从 Mod 与 NuGet 发布清单中排除。

## PCK 与 Shader

gd_cubism 通过固定的 `res://addons/gd_cubism/res/shader/*` 路径加载 10 个 Shader。这些资源必须进入
`Live2D.pck`，并保持 `Live2D.json` 的 `has_pck: true`。

初始化会验证全部 Shader；场景补丁在验证通过后注册。

## 公共 API 约束

- `Live2D.Api` 定义公共表面。
- NuGet 打包 `ref/net9.0` 引用程序集和 XML 文档。
- 稳定句柄通过值对象封装 Godot 节点状态。
- 队列输入必须提交时复制和校验；同字段或同 ID 使用最后值。
- 有顺序含义的动作与表情不能进入合并队列。
- API 变更必须同步 XML 注释、参考文档、示例和 CI 版本检查。

## 游戏补丁

所有游戏方法补丁通过 RitsuLib 的 `CreatePatcher`、`IPatchMethod` 和 `ApplyRequiredPatcher` 注册。

## 本地生成物

`.gitignore` 排除 Godot/.NET 缓存、NuGet 与 PCK 产物、文档依赖、测试覆盖率、崩溃转储和本地模型夹具。
本项目按资源路径加载内容，仓库追踪发布所需的
`addons/gd_cubism/bin/libgd_cubism.windows.release.x86_64.dll`，其余生成资源由 `.gitignore` 管理。

## 目录

```text
Scripts/Api/            稳定第三方 API
Scripts/Configuration/  配置模型、归一化、存储和继承
Scripts/Models/         model3 解析与受管理模型仓库
Scripts/Packs/          Pack 归档、导入与注册
Scripts/Runtime/        场景宿主、Cubism 和模型实例
Scripts/UI/             设置、预览和主菜单入口
Live2D/localization/    中文、英语和日语文本
docs/.vitepress/        站点配置、主题与构建缓存
docs/content/zh-CN/     简体中文正式文档
docs/content/en-US/     English 正式文档
docs/content/ja-JP/     日本語正式文档
Tools/                  使用示例、烟雾测试和 PCK 验证
examples/               测试模型
```

## 平台与资源

- 发布平台为 Windows x86_64。
- 多个高分辨率纹理和大型蒙版会增加显存占用。
