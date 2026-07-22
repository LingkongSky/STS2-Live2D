# Pack 形式

`.live2dpack` は Live2D パッケージの拡張子です。内容は `FormatVersion = 1` の ZIP で、他のファイル拡張子は Live2D パッケージではありません。

## 構造

```text
manifest.json
settings/
  models.json
  global.json                 # IncludesGlobalConfig=true の場合に必要
models/
  <OriginalId>/
    <model>.model3.json
    <model>.moc3
    textures/...
    motions/...
    expressions/...
    その他の参照ファイル...
```

各モデルと全依存ファイルは対応する `models/<OriginalId>/` 配下に置きます。

## manifest.json

```json
{
  "FormatVersion": 1,
  "PackageId": "example.characters",
  "Name": "Example Characters",
  "Author": "ExampleMod",
  "CreatedAt": "2026-07-15T00:00:00+08:00",
  "MinimumModVersion": "0.6.1",
  "SettingsSchemaVersion": 6,
  "IncludesGlobalConfig": false,
  "Models": [{
    "OriginalId": "character-source-id",
    "ModelKey": "character-main",
    "DisplayName": "Character",
    "EntryPath": "models/character-source-id/character.model3.json",
    "ContentHash": "sha256-or-stable-content-id"
  }]
}
```

- `PackageId` と `ModelKey` は空ではない安定 ID で、128 文字以内、制御文字禁止です。
- `SettingsSchemaVersion` は `6` が必要です。
- `EntryPath` は対応ディレクトリ内の `.model3.json` を指します。
- `ContentHash` にはモデル全体の安定 SHA-256 を推奨します。

## settings ファイル

`settings/models.json` はモデル設定配列です。ID、相対エントリ、内容ハッシュ、上書き、アクション情報、バインド配列が必要です。
`settings/global.json` は [Global 設定](./configuration) と同じ構造で、`IncludesGlobalConfig=true` の場合だけ存在します。

`RegisterPack` はグローバル設定をインポートしません。永続インポートでは取り込めます。

## 安全制限

- ZIP エントリ最大 10,000 件。
- 展開後 1 エントリ最大 512 MiB、合計 2 GiB。
- 圧縮率最大 1000:1。
- JSON メタデータ 1 件最大 16 MiB。
- 絶対パス、`.`、`..`、パストラバーサル、シンボリックリンク、重複パス禁止。
- `manifest.json` は 1 つだけ。
- 宣言されたエントリと依存ファイルはすべて必要。

Pack は Mod のビルド時に生成し、実行時に信頼できない ZIP を組み立てないでください。

## 登録 ID

同じ `OwnerModId + PackageId` で同じ内容を登録すると既存ハンドルを返します。同じ ID で異なる内容は拒否されます。
インスタンス ID は `OwnerModId + PackageId + Scene + InstanceId` です。
