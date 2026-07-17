# Pack 格式

`.live2dpack` 与 `.livepck` 都是 ZIP，内容完全相同。本文只描述当前 `FormatVersion = 1`；运行时不迁移旧格式。

## 目录结构

```text
manifest.json
settings/
  models.json
  global.json                 # IncludesGlobalConfig=true 时必须存在
models/
  <OriginalId>/
    <model>.model3.json
    <model>.moc3
    textures/...
    motions/...
    expressions/...
    physics/pose/其他依赖...
```

每个模型及全部引用资源必须位于同一个 `models/<OriginalId>/` 前缀下。

## manifest.json

JSON 属性名不区分大小写，推荐 PascalCase：

```json
{
  "FormatVersion": 1,
  "PackageId": "example.characters",
  "Name": "Example Characters",
  "Author": "ExampleMod",
  "Description": "Characters bundled with ExampleMod",
  "CreatedAt": "2026-07-15T00:00:00+08:00",
  "MinimumModVersion": "0.4.1",
  "SettingsSchemaVersion": 6,
  "IncludesGlobalConfig": false,
  "Models": [
    {
      "OriginalId": "character-source-id",
      "ModelKey": "character-main",
      "DisplayName": "Character",
      "EntryPath": "models/character-source-id/character.model3.json",
      "ContentHash": "sha256-or-stable-content-id"
    }
  ]
}
```

### 顶层字段

| 字段 | 要求 |
| --- | --- |
| `FormatVersion` | 必须为 `1` |
| `PackageId` | 稳定 ID；非空、最多 128 字符、无控制字符 |
| `Name` | 显示名称；为空时回退到 PackageId |
| `Author` / `Description` | 展示元数据 |
| `CreatedAt` | ISO-8601 时间戳 |
| `MinimumModVersion` | 打包方声明的最低运行时版本 |
| `SettingsSchemaVersion` | 必须为当前值 `6` |
| `IncludesGlobalConfig` | true 时必须包含 `settings/global.json` |
| `Models` | 一个或多个模型入口 |

### 模型字段

- `OriginalId` 对应 `settings/models.json` 中的 `Id`，并决定资源目录。
- `ModelKey` 是第三方 API 使用的稳定模型键。
- `EntryPath` 必须位于对应资源目录并以 `.model3.json` 结尾。
- `ContentHash` 用于内容身份与重复判断，推荐使用稳定 SHA-256。

## settings/models.json

该文件是模型配置数组。最小对象仍需包含 `Id`、显示名称、相对入口、内容哈希、覆盖对象和动作数组：

```json
[
  {
    "Id": "character-source-id",
    "DisplayName": "Character",
    "ModelRelativePath": "character.model3.json",
    "SourcePath": "",
    "ContentHash": "sha256-or-stable-content-id",
    "ImportedAt": "2026-07-15T00:00:00+08:00",
    "DisplayOrder": 0,
    "Overrides": {
      "MainMenu": {},
      "InGame": {},
      "Playback": {},
      "Rendering": {}
    },
    "AvailableActions": [],
    "ActionBindings": []
  }
]
```

Motion 动作使用 `MotionGroup` 与 `MotionIndex`；Expression 使用 `ExpressionId`。只读注册不需要快捷键，
`ActionBindings` 可以为空。

## settings/global.json

只在 `IncludesGlobalConfig=true` 时存在，结构对应 [配置结构](./configuration) 的 `Global`。`RegisterPack` 不会写入
玩家设置；`ImportPack` 可以持久导入全局配置。

## 安全限制

- 最多 10,000 个 ZIP 条目。
- 单条目解压后最大 512 MiB；总计最大 2 GiB。
- 单条目压缩比不得超过 1000:1。
- 单个 JSON 元数据最大 16 MiB。
- 禁止绝对路径、`.`、`..`、路径穿越、符号链接和重复路径。
- `manifest.json` 必须恰好存在一个。
- 所有声明的入口与模型资源必须存在。

Pack 应在 Mod 构建阶段生成，不要在游戏运行时拼接不可信 ZIP。

## 注册身份

相同 `OwnerModId + PackageId` 注册相同内容时返回已有句柄；相同身份对应不同内容时拒绝注册。运行时实例由
`OwnerModId + PackageId + Scene + InstanceId` 标识。
