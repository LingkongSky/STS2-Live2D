# モデル制御

`ILive2DModelHandle` は安定 ID、利用可能状態、スナップショット、操作メソッドを提供します。実ノードに触れる操作はメインスレッドが必要です。

## 部分更新

```csharp
model.Update(update =>
{
    update.Position = new Vector2(1180f, 720f);
    update.Scale = new Vector2(-0.42f, 0.42f);
    update.Opacity = 0.9f;
    update.Layer = 20;
    update.PlaybackSpeed = 1.1f;
});
```

指定しない項目は変化しません。`Snapshot` は変換、表示、再生、描画の現在値を返します。

## Motion と Expression

```csharp
var wave = model.Actions.First(action => action.DisplayName == "Wave");
model.PlayAction(wave);
model.PlayMotion("TapBody", 0);
model.StopMotion();
model.SetExpression("smile");
model.ClearExpression();
```

`Actions` はシーンインスタンスが利用不可でも読めます。Motion イベントは Godot メインスレッドで発生します。

## Parameter と Part

```csharp
model.SetParameters(new Dictionary<string, float>
{
    ["ParamAngleX"] = 15f,
    ["ParamMouthOpenY"] = 0.6f,
});
model.SetPartOpacity("PartArmL", 0.5f);
```

一括書き込みは全 ID を検証してから適用します。Parameter はモデル範囲、Part 不透明度は `0..1` に制限されます。
これらの動的値は保存されず、モデル再生成後にも復元されません。

## フィルターとクリッピング

```csharp
model.SetBlendMode(Live2DBlendMode.Add);
model.SetFilter(new Live2DFilterSettings
{
    Tint = new Color(0.85f, 0.95f, 1f),
    Brightness = 0.05f,
    Contrast = 1.1f,
    Saturation = 0.8f,
});
model.SetMask(new Live2DMaskSettings
{
    Type = Live2DMaskType.RoundedRectangle,
    Rect = new Rect2(-420f, -760f, 840f, 920f),
    CornerRadius = 48f,
});
```

`ResetFilter`、`ClearMask`、`Live2DBlendMode.Normal` で中立状態へ戻せます。

## ライフサイクル

- `IsAvailable` はシーンインスタンスへの接続状態です。
- 2 つの非同期待機はキャンセル可能です。
- 利用可能状態イベントは継続監視に使えます。
- `Destroy()` は `CanDestroy` が true の Pack インスタンスだけで使用できます。

非同期待機後にモデルを操作する場合も `InvokeAsync` を使用してください。
