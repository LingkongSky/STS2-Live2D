# モデル Pack の同梱

他の Mod は `.live2dpack` を自身の PCK に含め、Live2D の統一モデルライブラリへ登録できます。`.live2dpack` が唯一対応するパッケージ拡張子です。

## 登録

```csharp
var pack = Live2DApi.RegisterPack(
    "MyMod",
    "res://MyMod/live2d/characters.live2dpack");
```

Pack のモデルは Live2D のモデル管理に表示されます。表示、配置、描画、アクション、ホットキー、シーンインスタンスを Live2D が管理します。
提供側 Mod は別のモデル設定ページ、ホットキー制御、インスタンス制御を登録しません。

資源は提供側 Mod が所有し、セッションキャッシュにだけ存在するため、ライブラリからエクスポートできません。
プレイヤーは提供資源を削除せずにローカル登録項目だけを削除し、後で復元できます。Live2D はプレイヤー設定だけを永続化します。
提供側が未ロード、またはプレイヤーがモデルを無効にした場合、設定を保持したままモデルが利用不可になります。

提供側は開始 Motion、ストーリー反応、状態連携などキャラクター固有の動作を保持できます。
`ILive2DProviderLifecycleHook` を実装して Pack より先に登録し、別のインスタンス管理を作成しないでください。

```csharp
sealed class CharacterHook : ILive2DProviderLifecycleHook
{
    public void OnModelAvailable(ILive2DModelHandle model)
    {
        if (model.Scene == Live2DScene.MainMenu)
            model.PlayMotion("Intro", 0);
    }

    public void OnModelUnavailable(ILive2DModelHandle model)
    {
        // 待機中の提供側固有の非同期動作をキャンセルします。
    }
}

var lifecycle = Live2DApi.RegisterProviderHook("MyMod", new CharacterHook());
var pack = Live2DApi.RegisterPack("MyMod", "res://MyMod/live2d/characters.live2dpack");
```

段階は `OnPackRegistered`、`OnModelAvailable`、`OnModelUnavailable`、`OnPackUnregistered` の 4 つです。
後から登録した場合も既存 Pack と現在利用可能なモデルが順番に通知されます。返された `IDisposable` を保持し、不要になった時に `Dispose()` してください。

## ライフサイクル

`pack.Unregister()` は提供資源を解除してライブラリを更新します。同じ `OwnerModId + PackId + ModelKey` を再登録した時のため、
プレイヤー設定は保持されます。同じ内容の重複登録は既存ハンドルを返し、同じ ID の異なる内容は拒否されます。

## パスとエクスポート

OS パス、`res://`、`user://`、`ReadOnlyMemory<byte>` に対応します。提供側 PCK に Pack を明示的に含めてください。

```ini
export_filter="resources"
include_filter="MyMod/live2d/*.live2dpack"
exclude_filter="artifacts/**,Scripts/**,MyMod/src/**"
```

モデルが 1 つでも `settings/models.json` のルートは `[{ ... }]` の配列にします。完全な構造は
[Pack 形式](../reference/pack-format)を参照してください。
