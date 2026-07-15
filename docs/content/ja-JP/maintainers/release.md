# テストとリリース

ドキュメント、コンパイル、API コンシューマー、描画、PCK、パッケージの全チェックが必要です。

## ドキュメントとコンパイル

```powershell
.\Tools\check-docs.ps1
Push-Location .\docs
npm ci
npm run build
Pop-Location
dotnet build .\Live2D.csproj -c Release -p:Live2DCopyToGame=false
dotnet build .\Tools\ApiConsumerExample\Live2DApiConsumerExample.csproj `
  -c Release -p:Live2DCopyToGame=false
```

コンシューマー出力に `Live2D.dll` が含まれてはいけません。

## 描画スモークテスト

```powershell
.\Tools\run-render-smoke.ps1 `
  -GodotPath "C:\Tools\Godot\Godot_v4.5.1-stable_mono_win64_console.exe" `
  -Sts2Dir "D:\SteamLibrary\steamapps\common\Slay the Spire 2" `
  -CaptureImage
```

ブレンド、クリッピング、フィルター、ディスパッチ、統合キュー、非同期待機、Parameter、Part、GPU 描画を検証します。

## PCK と NuGet

`Tools/export-live2d-pck.ps1` で PCK を出力し、10 個の Shader を隔離検証します。NuGet は次で作成します。

```powershell
dotnet pack .\Live2D.csproj -c Release -p:Live2DCopyToGame=false -o artifacts
$packageSource = (Resolve-Path .\artifacts).Path
dotnet restore .\Tools\ApiConsumerExample\Live2DApiConsumerExample.csproj `
  -p:Live2DPackageVersion=0.4.0 `
  -p:RestoreAdditionalProjectSources="$packageSource" --force
dotnet build .\Tools\ApiConsumerExample\Live2DApiConsumerExample.csproj `
  -c Release -p:Live2DPackageVersion=0.4.0 --no-restore
```

NuGet に含められるのは `ref/net10.0` DLL/XML、README、Markdown 文書、buildTransitive target だけです。`lib/` ランタイム DLL は禁止です。`Live2DPackageVersion` を指定するとコンシューマー例は ProjectReference ではなく生成済み NuGet を検証し、出力に `Live2D.dll` を含めません。

## リリースチェック

1. Manifest 要件とすべてのバージョンが一致。
2. 中国語、英語、日本語の文書がリンク切れなくビルド可能。
3. Release とコンシューマービルドが成功。
4. GPU スモークテストが成功。
5. PCK 検証が Shader `10/10`。
6. 実 Pack の登録、作成、破棄、登録解除、永続インポートが成功。
7. 配置 DLL と Release DLL のハッシュが一致。
8. ゲーム完全再起動後、メインメニュー、マップ、戦闘、設定、インポート、ホットキーを確認。

サイトは最新文書だけを公開し、旧バージョンページは生成しません。
