# 公開 API リファレンス

現在のランタイムは `Live2DApi.RuntimeVersion == "0.5.5"`、能力バージョンは `Live2DApi.RuntimeApiVersion == 9` です。
安定した公開 API は `Live2D.Api` だけです。

## スレッド規則

| API | 呼び出し可能スレッド |
| --- | --- |
| `Post`、`InvokeAsync` | 任意 |
| モデル検索、ハンドル ID、`Actions`、利用可能状態、非同期待機 | 任意 |
| `QueueUpdate`、Parameter/Part Queue API | 任意 |
| Pack のインポート、登録、解除、提供側 Hook 登録 | Godot メインスレッド |
| `Snapshot`、`Apply`、`Set*`、再生、動的値の取得 | Godot メインスレッド |

すべての API イベントは Godot メインスレッドで発生します。非同期待機後の継続処理はメインスレッドとは限りません。

## Live2DApi

### ランタイム状態と検索

- `RuntimeApiVersion`、`RuntimeVersion`、`PackageFileExtension`、`IsDispatcherReady`、`IsMainThread`。
- `GetModels()` と所有者指定版は配列スナップショットを返します。
- `GetModel` / `TryGetModel` はモデル ID とシーンで検索します。
- `RegisterProviderHook(ownerModId, hook)` は提供側ごとの 4 段階ライフサイクル Hook を登録し、既存状態も通知します。

### ディスパッチ

- `Post(Action)` は完了を待たず、例外をログへ記録します。
- `InvokeAsync(Action)` と `InvokeAsync<T>(Func<T>)` は完了、例外、キャンセルを返します。
- 一時停止中も動作し、1 フレーム最大 512 件を処理します。

### Pack

- `ImportPack(path/data)` はプレイヤーライブラリへ永続インポートします。
- `RegisterPack(ownerModId, path/data)` は提供側資源を Live2D の統一ライブラリへ登録し、設定とインスタンスを Live2D が管理します。
- `RegisterProviderHook(ownerModId, hook)` は設定やインスタンスの所有権を移さずにキャラクター固有動作を追加します。
- OS、`res://`、`user://` パス、および `ReadOnlyMemory<byte>` に対応します。

パス指定のインポートと登録は `.live2dpack` 拡張子のみ受け付けます。内容が ZIP でも他の拡張子は拒否されます。

`res://`、`user://`、メモリ入力は OS の一時ファイルへ展開され、成功・失敗に関係なく処理後に削除されます。
登録に必要な資源はセッションキャッシュへコピーされ、一時ファイルには依存しません。Managed 登録ではユーザー設定だけを永続化し、提供側資源はコピーしません。

## ILive2DPackHandle

`OwnerModId`、`PackId`、`Name`、`IsRegistered`、`Models` が ID とメタデータを公開します。`Unregister` はプレイヤー設定を保持したまま
提供側資源を解除します。ID は 128 文字以内で制御文字を含められません。

## ILive2DProviderLifecycleHook

| 段階 | タイミングと用途 |
| --- | --- |
| `OnPackRegistered` | モデル更新前に Pack 登録済み。資源メタデータを参照 |
| `OnModelAvailable` | シーンモデル接続済み。提供側 Motion を安全に再生可能 |
| `OnModelUnavailable` | シーンモデル切断済み。待機中の非同期動作をキャンセル |
| `OnPackUnregistered` | 提供側資源が現在のセッションから削除済み |

Hook は `ownerModId` ごとに分離され、Godot メインスレッドで実行されます。例外はログへ記録され、他の Hook を中断しません。
後から登録した場合は既存 Pack、現在利用可能なモデルの順で通知されます。返された `IDisposable` を保持してください。

## ILive2DModelHandle

### ID と利用可能状態

`ModelId`、`OwnerModId`、`PackId`、`ModelKey`、`InstanceId`、`Scene` が安定 ID を構成します。`IsAvailable` は接続状態、

シーン切り替えやノード再生成でもハンドルは無効になりません。利用不可の間も ID と `Actions` は読み取り可能です。
`Snapshot` はメインスレッド専用で、ノード未接続時は最後に確認した状態を返します。

- 利用可能状態イベントは継続監視向けです。
- 2 つの非同期待機はキャンセル可能で、購読競合がありません。
- `Snapshot` は変換、表示、再生、描画状態を返します。

### 状態更新

`Apply` は部分更新、`Update` は設定コールバックで、どちらもメインスレッド専用です。`QueueUpdate` は任意スレッドから項目を統合します。
設定コールバックは呼び出し元スレッドですぐ実行されるため、更新データだけを設定し、Godot ノードへアクセスしないでください。

| 項目 | 検証 |
| --- | --- |
| `Position` | 両成分が有限 |
| `Scale` | 有限かつ 0 以外。負値は反転 |
| `RotationDegrees` | 有限 |
| `Opacity` | `0..1` に制限 |
| `Visible` | ルート表示状態 |
| `Layer` | Godot `ZIndex` |
| `PlaybackSpeed` | 負値は 0 |
| `PhysicsEnabled` / `PoseEnabled` | Cubism 動作 |
| `MaskViewportSize` | 負値は 0 |
| `BlendMode` | 定義済み列挙値 |
| `Filter` | モデル全体の色フィルター |
| `Mask` | モデルローカルのクリッピング |

### 再生と動的値

- `Actions`、`PlayAction`、`PlayMotion`、`StopMotion`、Expression、Motion イベント。
- Parameter の取得、Try、設定、一括設定、キュー。
- Part の取得、Try、不透明度設定、一括設定、キュー。

不明 ID は `KeyNotFoundException` です。同期一括処理は全 ID を先に検証し、キューは大文字小文字を区別せず統合します。

## 描画範囲

| Filter | 範囲 / 既定値 |
| --- | --- |
| Tint | 有限 RGBA / 白 |
| Brightness | `-1..1` / 0 |
| Contrast | `0..4` / 1 |
| Saturation | `0..4` / 1 |
| Grayscale | `0..1` / 0 |
| Hue shift | 有限角度 / 0 |
| Invert | `0..1` / 0 |
| Gamma | `0.01..10` / 1 |

Mask は `None`、`Rectangle`、`Ellipse`、`RoundedRectangle`。有効時は正の幅・高さ、0 以上の角半径、`2..64` の分割数が必要です。

## 主な例外

| 例外 | 原因 |
| --- | --- |
| `ArgumentException` | 空、長すぎる、制御文字を含む ID |
| `ArgumentOutOfRangeException` | 非有限値、不正な範囲・列挙値・Motion index |
| `InvalidOperationException` | スレッド、利用不可、ID 競合、登録解除済み |
| `KeyNotFoundException` | 不明な Parameter / Part |
| `InvalidDataException` / `IOException` | Pack またはファイル障害 |
| `OperationCanceledException` | 待機またはディスパッチのキャンセル |
