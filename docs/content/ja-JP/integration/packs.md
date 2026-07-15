# モデル Pack の同梱

他の Mod は `.live2dpack` または `.livepck` を自身の PCK に含め、Live2D ランタイムから読み込めます。両拡張子は同じ ZIP 形式です。

## 読み取り専用登録と永続インポート

| 目的 | API | プレイヤーライブラリへ書き込む |
| --- | --- | --- |
| モデルを自分の Mod が管理する | `RegisterPack` | いいえ |
| プレイヤーが永続編集する | `ImportPack` | はい |

他 Mod に属するキャラクター資源には読み取り専用登録を推奨します。

## 登録と作成

```csharp
var pack = Live2DApi.RegisterPack(
    "MyMod",
    "res://MyMod/live2d/characters.live2dpack");

var info = pack.Models.First(model => model.ModelKey == "character-main");
var model = pack.CreateModel(info.ModelKey, new Live2DCreateOptions
{
    Scene = Live2DScene.MainMenu,
    InstanceId = "main-menu-character",
    InitialState = new Live2DModelUpdate
    {
        Position = new Vector2(1350f, 760f),
        Scale = Vector2.One * 0.4f,
        Opacity = 0.9f,
    },
});
```

インスタンス ID は `OwnerModId / PackId / Scene / InstanceId` です。同じモデルと ID の再作成は同じ結果を返し、異なる
`ModelKey` を同じ ID に割り当てると拒否されます。

## ライフサイクル

```csharp
model.Destroy();
pack.Unregister();
```

Destroy は 1 つのインスタンスを削除します。Unregister はその Pack の全インスタンスとセッションキャッシュを削除します。
どちらもプレイヤーモデルを削除しません。

## 入力元

OS パス、`res://`、`user://`、`ReadOnlyMemory<byte>` に対応します。Pack を自身の PCK エクスポートへ必ず含めてください。
構造と安全制限は [Pack 形式](../reference/pack-format) を参照してください。
