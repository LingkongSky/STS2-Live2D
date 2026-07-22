# テストとリリース

ドキュメント、コンパイル、API コンシューマー、描画、PCK、パッケージの全チェック成功後にタグを作成します。

## ドキュメントとコンパイル

```powershell
Push-Location .\docs
npm ci
npm run build
Pop-Location
```

`dotnet build` はコンパイル確認、`dotnet publish` はリリース DLL の生成に使用します。コンシューマーは ref-only API パッケージを使用し、ランタイムを Mod 依存関係から解決します。

## 描画スモークテスト

```powershell
$Godot = "C:\Tools\Godot\Godot_v4.5.1-stable_mono_win64_console.exe"
$env:STS2_DIR = "D:\SteamLibrary\steamapps\common\Slay the Spire 2"
dotnet build .\Tools\RenderSmoke\Live2DRenderSmoke.csproj
& $Godot --headless --path .\Tools\RenderSmoke --rendering-method gl_compatibility
```

ブレンド、クリッピング、フィルター、ディスパッチ、統合キュー、非同期待機、Parameter、Part、GPU 描画を検証します。

## PCK と NuGet

Godot で PCK を出力し、10 個の Shader を隔離検証した後、`dotnet publish` で完全な Mod を出力します。NuGet は準備済みリリースディレクトリ方式です。

```powershell
& $Godot --headless --editor --path $PWD --quit
& $Godot --headless --path $PWD --export-pack Live2D "$PWD\Live2D.pck"
& $Godot --headless --path .\Tools\PckVerifier `
  --script verify_pck.gd -- "$PWD\Live2D.pck"
dotnet publish .\Live2D.csproj -c Release -o .\artifacts\Live2D -p:BundleMod=true

dotnet build .\Live2D.csproj -c Release -t:RefreshNuGetReference
git diff -- NuGet/package/ref/net9.0
dotnet pack .\NuGet\STS2.Live2D.Package.csproj -c Release -o .\artifacts
```

インストールまたは公開には、同じ publish で生成した `artifacts/Live2D` 全体を使用します。

NuGet は `ref/net9.0` の参照アセンブリ/XML、README、第三者ライセンス声明、`docs/content` の Markdown を含みます。
リリースワークフローは一時コンシューマープロジェクトで ref-only 構造を検証します。

`NuGet/package/ref/net9.0` は準備済みリリースディレクトリです。ここに置く `Live2D.dll` はメタデータ専用の参照アセンブリです。
更新した DLL と XML は、対応するソース変更と同じコミットに含めます。

プロジェクトバージョンと一致する `v*` タグを push すると `publish-nuget.yml` がコミット済み参照アセットをパッケージ化・検証し、
NuGet Trusted Publishing (OIDC) で公開します。NuGet.org のポリシーをこのワークフローと `nuget` environment に関連付けます。

## リリースチェック

1. Manifest 要件とすべてのバージョンが一致。
2. 中国語、英語、日本語の文書がリンク切れなくビルド可能。
3. Release とコンシューマービルドが成功。
4. GPU スモークテストが成功。
5. PCK 検証が Shader `10/10`。
6. 実 Pack の登録、モデル利用可能・利用不可 Hook、登録解除、永続インポートが成功。
7. 配置 DLL と Release DLL のハッシュが一致。
8. ゲーム完全再起動後、メインメニュー、マップ、戦闘、設定、インポート、ホットキーを確認。

サイトは `main` ブランチの文書を公開します。
