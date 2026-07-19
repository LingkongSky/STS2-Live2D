---
layout: home
pageClass: live2d-home

hero:
  name: STS2 Live2D
  text: 在《杀戮尖塔 2》中使用 Live2D
  tagline: >-
    导入并管理 Live2D Cubism 模型，在主菜单和游戏场景中播放动作、调整渲染，或通过公共 API 由其他 Mod 实时控制。
  actions:
    - theme: brand
      text: 玩家指南
      link: /guide/getting-started
    - theme: alt
      text: Mod 接入指南
      link: /integration/getting-started

features:
  - title: 玩家使用
    details: >-
      了解安装要求，导入第一个模型，并配置主菜单或游戏内的显示位置、大小与动作。
    link: /guide/getting-started
    linkText: 开始配置
  - title: Mod 集成
    details: >-
      通过项目引用或 NuGet 添加编译期 API，并声明 Live2D 运行时 Mod 依赖。
    link: /integration/getting-started
    linkText: 查看接入流程
  - title: 模型 Pack
    details: >-
      将 .live2dpack 放入自己的 PCK，注册到 Live2D 统一模型库。
    link: /integration/packs
    linkText: 分发模型
  - title: API 参考
    details: >-
      查询模型句柄、动作、变换、滤镜、蒙版、Parameter、Part 与主线程调度接口。
    link: /reference/api
    linkText: 浏览 API
---

## 当前状态

::: warning 发布范围
`STS2.Live2D` NuGet 包只提供第三方 Mod 的编译期 API。玩家仍需单独安装 Live2D 运行时 Mod；请勿把 NuGet 包当作运行时文件。
:::

- **运行时版本：** `0.5.5`
- **公共 API：** `4`
- **Pack 格式：** `1`
- **支持平台：** Windows x86_64

## 核心能力

- **场景集成：** 分别管理主菜单和游戏内模型，并在地图、战斗与界面切换时维护显示状态。
- **模型控制：** 实时修改位置、大小、旋转、透明度、层级、动作、表情、Parameter 和 Part。
- **渲染控制：** 支持混合模式、常用颜色滤镜，以及矩形、椭圆和圆角矩形画布裁切。
- **第三方扩展：** 提供稳定句柄、主线程调度、高频更新合并和其他 Mod 自带模型 Pack 的注册接口。

## 范围与要求

STS2 Live2D 负责加载和控制 Live2D Cubism 模型，不提供模型制作或源格式转换。模型必须包含有效的
`.model3.json`、`.moc3`、纹理以及清单引用的全部依赖文件。
