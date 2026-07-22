# 設定構造

Live2D は RitsuLib の `settings` キーに `settings.json` を保存します。`SchemaVersion` は `6` です。編集にはゲーム内 UI を使用してください。

## 解決順序

```text
プログラム既定値 → Global → null ではないモデル Overrides
```

`null` は継承です。Filter と Mask はオブジェクト全体で選択し、子項目をマージしません。

## 最上位

```json
{
  "SchemaVersion": 6,
  "Global": {
    "Hotkeys": {},
    "MainMenu": {},
    "InGame": {},
    "Playback": {},
    "Rendering": {}
  },
  "Models": [],
  "RemovedExternalModelIds": []
}
```

## Global

`Hotkeys.ToggleVisibility` は全体表示ホットキーです。

### MainMenu / InGame

| 項目 | 意味 |
| --- | --- |
| `Visible` | シーン表示状態 |
| `Anchor` | 9 点の画面アンカー |
| `OffsetX/Y` | 1920×1080 基準キャンバスの位置 |
| `Scale` | 等倍スケール |
| `RotationDegrees` | 時計回り角度 |
| `Opacity` | `0..1` に制限 |
| `Layer` | Godot `ZIndex` |
| `MouseInteraction` | マウス入力 |

マップと戦闘は `InGame` を共有します。

### Playback

既定値は `Speed=1`、Physics / Pose 有効、自動 Idle 有効、アクションクールダウン `0.1` 秒です。

### Rendering

`MaskViewportSize` は Cubism マスクテクスチャサイズです。`BlendMode` は Normal、Add、Subtract、Multiply、PremultipliedAlpha。
`Filter` と `Mask` は公開 API と同じ範囲です。不正な保存値は制限または安全な既定値へ置換されます。

## Models

各モデルは安定 ID、`Enabled`（既定値 `true`）、表示名、管理 model3 パス、元パス記録、内容ハッシュ、インポート時刻、表示順、上書き、検出アクション、
ホットキーを保存します。アクション種別は `0` が Motion、`1` が Expression です。

`RemovedExternalModelIds` はプレイヤーが削除した提供モデルを Pack の再登録時に自動追加しないための記録です。ゲーム内の復元操作で除外を解除します。

## 実行時上書き

ハンドル更新は現在のセッションだけに適用され、`settings.json` へ書き込みません。安定状態はノード再生成後に復元されますが、
Parameter と Part は短命な動的値であり復元されません。
