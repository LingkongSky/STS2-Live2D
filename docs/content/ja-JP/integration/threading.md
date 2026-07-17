# スレッドと高頻度更新

Godot シーンと Cubism ノードはメインスレッドに属します。API は順序を持つコマンドと、統合可能な連続状態を分けています。

## 完了を待つ呼び出し

```csharp
await Live2DApi.InvokeAsync(() => model.PlayMotion("TapBody", 0), cancellationToken);
var snapshot = await Live2DApi.InvokeAsync(() => model.Snapshot, cancellationToken);
```

`InvokeAsync` はメインスレッド上なら即時実行し、例外とキャンセルを返します。実行開始後のコールバックは強制中断されません。

低頻度で結果が不要な通知には `Post` を使用します。例外はログへ記録されます。

```csharp
Live2DApi.Post(() => model.SetVisible(true));
```

起動中は `Live2DApi.IsDispatcherReady` を確認してください。メインスレッドを必要とする Task を同じスレッドで同期待機しないでください。

## 連続状態

```csharp
model.QueueUpdate(update =>
{
    update.Position = trackedPosition;
    update.RotationDegrees = trackedRotation;
    update.Opacity = trackedOpacity;
});
```

同じモデルの未実行更新は統合されます。異なる項目は保持され、同じ項目は最後の値が優先されます。
`QueueUpdate(Action<Live2DModelUpdate>)` のコールバックは呼び出し元スレッドですぐ実行され、完成したデータだけがキューへ入ります。
コールバック内で `Snapshot` や他の Godot オブジェクトへアクセスしないでください。

## Parameter / Part キュー

```csharp
model.QueueParameters(new Dictionary<string, float>
{
    ["ParamAngleX"] = angleX,
    ["ParamMouthOpenY"] = mouthOpen,
});
model.QueuePartOpacity("PartArmL", armOpacity);
```

ID は大文字小文字を区別せず統合されます。実行時にモデルが利用不可なら、そのバッチは破棄されます。

## API の選び方

| 処理 | API |
| --- | --- |
| Motion、Expression、インスタンス破棄 | `InvokeAsync` |
| 戻り値や例外が必要 | `InvokeAsync` |
| 低頻度通知 | `Post` |
| 位置、回転、不透明度、フィルターの連続入力 | `QueueUpdate` |
| 顔トラッキング、リップシンク、Part 不透明度 | Parameter/Part Queue API |

Motion、Expression、Physics、Pose は後続フレームで Parameter を変更するため、連続入力はサンプルを継続送信してください。
