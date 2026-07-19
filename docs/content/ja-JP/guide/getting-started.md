# はじめに

動作条件、開発ビルドのインストール、最初のモデルのインポート手順を説明します。

::: warning インストール範囲
NuGet パッケージはコンパイル専用で、プレイヤー用ランタイムを含みません。現在のランタイムはこのリポジトリからビルドしてください。
:::

## 動作条件

- Windows x86_64。
- 『Slay the Spire 2』`0.107.1` 以降。
- STS2 RitsuLib `0.4.56` 以降。
- 有効な Live2D Cubism 3/4 モデル。

## 開発ビルドをインストール

開発ビルドには Godot 4.5.1 Mono と .NET 9 SDK が必要です。Godot で Shader PCK を出力し、.NET CLI で完全な Mod ディレクトリを生成します。

```powershell
$Godot = "C:\Tools\Godot\Godot_v4.5.1-stable_mono_win64_console.exe"
$env:STS2_DIR = "D:\SteamLibrary\steamapps\common\Slay the Spire 2"

& $Godot --headless --editor --path $PWD --quit
& $Godot --headless --path $PWD --export-pack Live2D "$PWD\Live2D.pck"
dotnet publish .\Live2D.csproj -c Release -o .\artifacts\Live2D -p:BundleMod=true
```

`artifacts/Live2D` ディレクトリ全体を `mods/Live2D` へコピーします。最低限、次のファイルが必要です。

```text
Live2D.json
Live2D.dll
Live2D.pck
addons/gd_cubism/
```

通常の `dotnet build` はコンパイルのみで、Mod を配置しません。同じビルドの DLL、PCK、ネイティブファイルをまとめて置換し、ゲームを完全に終了してから再起動してください。

## 最初のモデルをインポート

1. メインメニューから「Live2D 設定」を開きます。
2. 「モデル管理」を開きます。
3. 「Live2D モデルを追加」を選びます。
4. モデルの `.model3.json` を選びます。
5. モデルが一覧に表示されたら「プレビューして調整」を選びます。

インポーターは `.moc3`、テクスチャ、Motion、Expression、Physics、Pose などの参照ファイルを管理ディレクトリへコピーします。

## 初回レイアウト

プレビューで「メインメニュー」または「ゲーム内」を選択し、次の操作を行います。

- 左ドラッグで位置を変更。
- ホイールで拡大・縮小。
- `Shift + ホイール` で回転。
- 複数のプレビュー解像度で確認。
- 「変更を保存」を選択。

マップと戦闘は「ゲーム内」設定を共有します。メインメニューで別ページやダイアログを開くと、モデルは一時的に非表示になります。

## 次に読むページ

- [複数モデルを管理する](./models)
- [フィルターとクリッピングを設定する](./appearance)
- [アクションとホットキーを設定する](./actions)
- [問題を解決する](./troubleshooting)
