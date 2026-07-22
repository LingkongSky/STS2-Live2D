---
layout: home
pageClass: live2d-home

hero:
  name: STS2 Live2D
  text: 『Slay the Spire 2』で Live2D を使う
  tagline: >-
    Live2D Cubism モデルをインポートして管理し、メニューやゲーム画面でモーションを再生、描画を調整、または公開 API から他の Mod でリアルタイム制御できます。
  actions:
    - theme: brand
      text: プレイヤーガイド
      link: /ja/guide/getting-started
    - theme: alt
      text: Mod 連携ガイド
      link: /ja/integration/getting-started

features:
  - title: プレイヤー向け
    details: >-
      動作条件を確認し、最初のモデルをインポートして、メニューまたはゲーム画面での位置、拡大率、アクションを設定します。
    link: /ja/guide/getting-started
    linkText: 設定を始める
  - title: Mod 連携
    details: >-
      ProjectReference または NuGet でコンパイル用 API を追加し、Live2D ランタイム Mod への依存を宣言します。
    link: /ja/integration/getting-started
    linkText: 導入手順を見る
  - title: モデル Pack
    details: >-
      .live2dpack を PCK に同梱し、Live2D の統一モデルライブラリへ登録します。
    link: /ja/integration/packs
    linkText: モデルを同梱する
  - title: API リファレンス
    details: >-
      モデルハンドル、再生、変換、フィルター、マスク、Parameter、Part、メインスレッド実行 API を確認します。
    link: /ja/reference/api
    linkText: API を確認する
---

## バージョンと配布

::: info 配布範囲
`STS2.Live2D` NuGet パッケージは他の Mod 向けコンパイル時 API、`Live2D` ランタイム Mod はプレイヤー環境を提供します。
:::

- **ランタイムバージョン：** `0.6.0`
- **公開 API：** `9`
- **Pack 形式：** `1`
- **対応プラットフォーム：** Windows x86_64

## 主な機能

- **シーン連携：** メインメニューとゲーム内モデルを個別に管理し、マップ、戦闘、UI 遷移をまたいで表示状態を維持します。
- **モデル制御：** 位置、拡大率、回転、透明度、レイヤー、モーション、表情、Parameter、Part をリアルタイムに変更します。
- **描画制御：** ブレンドモード、一般的な色フィルター、矩形・楕円・角丸矩形によるキャンバスクリッピングに対応します。
- **外部 Mod 拡張：** 安定ハンドル、メインスレッド実行、高頻度更新の統合、Mod 同梱モデル Pack の登録 API を提供します。

## 対象範囲と要件

STS2 Live2D は実行可能な Live2D Cubism モデルを読み込み、制御します。モデル資源には有効な `.model3.json`、`.moc3`、
テクスチャ、およびマニフェストが参照するすべての依存ファイルが含まれます。
