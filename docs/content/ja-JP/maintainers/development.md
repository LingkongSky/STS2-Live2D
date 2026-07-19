# 開発とアーキテクチャ

Live2D ランタイム本体のメンテナー向けです。第三者 Mod 作者は [Mod 連携](../integration/getting-started) から始めてください。

## ランタイムの責務

- model3 と全参照ファイルのインポート。
- プレイヤーモデルと読み取り専用 Pack インスタンスの管理。
- メインメニュー、マップ、戦闘へのホスト追加。
- グローバル設定とモデル上書きの解決。
- 変換、再生、描画、Parameter、Part API の提供。
- RitsuLib による設定、ホットキー、パッチ登録。

## 構造

```text
RitsuLib configuration
    ↓
Live2DConfigResolver
    ↓
Live2DRuntimeManager
    ├─ MainMenu host
    ├─ Map host
    └─ Combat host
          ↓
    Live2DModelInstance
          ↓
      gd_cubism
```

メインスレッドディスパッチャーは `SceneTree.Root` の常駐子ノードで、毎フレーム件数制限付きキューを処理します。

## アーキテクチャ規則

- 継承規則を実装するのは `Live2DConfigResolver` だけです。
- メインメニューは独立設定、マップと戦闘はゲーム内設定を共有します。
- レイアウトは 1920×1080 基準で保存し、ビューポート短辺で換算します。
- 安定 API ハンドルはノード再生成後に再接続し、セッション上書きを復元します。
- 公開 API は `Live2D.Api` だけです。
- NuGet は `ref/net9.0` の参照アセンブリと XML 文書だけを含みます。
- 順序を持つ再生コマンドを統合キューへ入れません。
- 設定 Schema は `6` です。

## PCK と Shader

gd_cubism は固定された `res://addons/gd_cubism/res/shader/*` から 10 個の Shader を読み込みます。すべて `Live2D.pck` に含め、
`Live2D.json` の `has_pck: true` を維持します。検証失敗時はシーンパッチを登録しません。

## ゲームパッチ

RitsuLib の `CreatePatcher`、`IPatchMethod`、`ApplyRequiredPatcher` を使用します。Harmony を直接作成しないでください。

## ローカル生成物

`.gitignore` は Godot/.NET キャッシュ、NuGet/PCK 成果物、文書依存、カバレッジ、クラッシュダンプ、ローカルモデルを除外します。
このプロジェクトはパスで資源を読み込むため、生成された外部 `*.uid` はコミットしません。ただし配布に必要な
`addons/gd_cubism/bin/libgd_cubism.windows.release.x86_64.dll` は明示的に追跡します。

## ソース構成

```text
Scripts/Api/            公開 API
Scripts/Configuration/  設定、正規化、保存、解決
Scripts/Models/         model3 パーサーと管理リポジトリ
Scripts/Packs/          Pack インポートとモデルライブラリ登録
Scripts/Runtime/        シーンホスト、Cubism、モデルインスタンス
Scripts/UI/             設定とプレビュー
docs/.vitepress/        サイト設定とテーマ
docs/content/zh-CN/     簡体字中国語コンテンツ
docs/content/en-US/     英語コンテンツ
docs/content/ja-JP/     日本語コンテンツ
Tools/                  サンプル、スモークテスト、PCK 検証
examples/               非公開テストモデル
```

現在は Windows x86_64 のみです。大きなテクスチャとマスクは GPU メモリ使用量を増やします。
