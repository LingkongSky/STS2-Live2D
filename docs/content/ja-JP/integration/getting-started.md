# Mod を 5 分で導入

コンパイル参照、実行時依存、最初のモデル操作を追加します。安定した公開 API は `Live2D.Api` 名前空間だけです。

## 1. コンパイル参照を追加

ref-only NuGet パッケージを推奨します。

```xml
<PackageReference Include="STS2.Live2D" Version="0.5.6" />
```

同じワークスペースで開発する場合：

```xml
<ProjectReference Include="..\STS2-Live2D\Live2D.csproj"
                  Private="false"
                  />
```

どちらもコンシューマー出力へ 2 つ目の `Live2D.dll` をコピーしません。

## 2. 実行時依存を宣言

```json
{
  "dependencies": [
    { "id": "Live2D", "min_version": "0.5.6" }
  ]
}
```

プレイヤー側には Live2D ランタイムが必要です。RitsuLib 依存は Live2D 自身が宣言します。

## 3. モデルを取得

```csharp
using Live2D.Api;

var model = Live2DApi.GetModel("model-id", Live2DScene.MainMenu);
if (model is null)
    return;

await model.WaitUntilAvailableAsync(cancellationToken);
```

ハンドルは安定オブジェクトです。設定更新、画面サイズ変更、シーン遷移でノードが再生成されても、同じハンドルが再接続します。

## 4. メインスレッドで操作

```csharp
using Godot;

await Live2DApi.InvokeAsync(() =>
{
    model.Update(update =>
    {
        update.Position = new Vector2(1200f, 760f);
        update.Scale = Vector2.One * 0.45f;
        update.RotationDegrees = -5f;
        update.Opacity = 0.85f;
        update.Visible = true;
    });
    model.PlayMotion("TapBody", 0);
}, cancellationToken);
```

実行時上書きはプレイヤー設定へ書き込まれず、ゲーム終了時に消えます。

## 次に読むページ

- [モデル制御](./model-api)
- [スレッドと高頻度更新](./threading)
- [モデルを Mod に同梱](./packs)
- [公開 API リファレンス](../reference/api)

リポジトリの `Tools/ApiConsumerExample` には完全な統合ソース例があります。
