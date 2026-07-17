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

`Tools/export-live2d-pck.ps1` で PCK を出力し、10 個の Shader を隔離検証します。NuGet は JMC の `modPublish` と同じ準備済みリリースディレクトリ方式です。公開 API または XML コメントを変更した後、ゲームをインストールしたローカル環境で参照アセットを更新してからパッケージします。

```powershell
.\Tools\update-nuget-reference.ps1 `
  -Sts2Dir "D:\Program Files\Steam\steamapps\common\Slay the Spire 2"
git diff -- NuGet/package/ref/net9.0
.\Tools\pack-nuget.ps1 -OutputDirectory artifacts
.\Tools\test-nuget-package.ps1 `
  -PackagePath .\artifacts\STS2.Live2D.0.4.1.nupkg `
  -ExpectedVersion 0.4.1
```

NuGet に含められるのは `ref/net9.0` の参照アセンブリ/XML、README、第三者ライセンス声明、`docs/content` の Markdown だけです。`lib/` ランタイム DLL は禁止です。検証スクリプトは一時コンシューマープロジェクトで完全な API 例をコンパイルし、出力に `Live2D.dll` が含まれないことを確認します。

`NuGet/package/ref/net9.0` は準備済みリリースディレクトリです。ここに置く `Live2D.dll` はメタデータ専用の参照アセンブリであり、ランタイム DLL ではありません。更新した DLL と XML は、対応するソース変更と同じコミットに含めます。

プロジェクトバージョンと一致する `v*` タグを push すると `publish-nuget.yml` が実行されます。ワークフローはコミット済み参照アセットのパッケージと検証だけを行い、Live2D のコンパイル、ゲームディレクトリの参照、`STS2-API-Signatures` へのアクセスは行いません。その後 NuGet Trusted Publishing (OIDC) で公開します。NuGet.org のポリシーをこのワークフローと `nuget` environment に関連付けます。`STS2_SIGNATURES_TOKEN` は不要です。

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
