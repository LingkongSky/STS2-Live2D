[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$docsRoot = Join-Path $root 'docs'
$contentRoot = Join-Path $docsRoot 'content'
$locales = @('zh-CN', 'en-US', 'ja-JP')
$requiredFiles = @(
    'README.md',
    'docs/package.json',
    'docs/package-lock.json',
    'docs/.vitepress/config.mts',
    'docs/content/zh-CN/index.md',
    'docs/content/zh-CN/guide/getting-started.md',
    'docs/content/zh-CN/guide/troubleshooting.md',
    'docs/content/zh-CN/integration/getting-started.md',
    'docs/content/zh-CN/integration/packs.md',
    'docs/content/zh-CN/reference/api.md',
    'docs/content/zh-CN/reference/configuration.md',
    'docs/content/zh-CN/reference/pack-format.md',
    'docs/content/zh-CN/maintainers/development.md',
    'docs/content/zh-CN/maintainers/release.md',
    'Tools/ApiConsumerExample/ExampleLive2DController.cs'
)

foreach ($relativePath in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $root $relativePath))) {
        throw "Required documentation file is missing: $relativePath"
    }
}

$markdownFiles = @((Join-Path $root 'README.md')) + @(
    Get-ChildItem -LiteralPath $contentRoot -Recurse -File -Filter '*.md' |
        Select-Object -ExpandProperty FullName
)

$canonicalLocaleRoot = Join-Path $contentRoot 'zh-CN'
$canonicalPages = @(
    Get-ChildItem -LiteralPath $canonicalLocaleRoot -Recurse -File -Filter '*.md' |
        ForEach-Object { [IO.Path]::GetRelativePath($canonicalLocaleRoot, $_.FullName) }
)
foreach ($locale in $locales) {
    $localeRoot = Join-Path $contentRoot $locale
    $localePages = @(
        Get-ChildItem -LiteralPath $localeRoot -Recurse -File -Filter '*.md' |
            ForEach-Object { [IO.Path]::GetRelativePath($localeRoot, $_.FullName) }
    )
    if (Compare-Object $canonicalPages $localePages) {
        throw "Documentation hierarchy differs for locale: $locale"
    }
    foreach ($relativePath in $canonicalPages) {
        $localizedPath = Join-Path $localeRoot $relativePath
        if (-not (Test-Path -LiteralPath $localizedPath)) {
            throw "Localized documentation file is missing: $locale/$relativePath"
        }
    }
}

function Test-DocumentationTarget([string]$sourceFile, [string]$target) {
    if ($target -match '^(https?://|mailto:|tel:|#)') {
        return $true
    }

    $pathPart = [uri]::UnescapeDataString(($target -split '[?#]')[0])
    if ([string]::IsNullOrWhiteSpace($pathPart)) {
        return $true
    }

    if ($pathPart.StartsWith('/')) {
        $routePath = $pathPart.TrimStart('/')
        $locale = 'zh-CN'
        if ($routePath -eq 'en' -or $routePath.StartsWith('en/')) {
            $locale = 'en-US'
            $routePath = $routePath.Substring(2).TrimStart('/')
        } elseif ($routePath -eq 'ja' -or $routePath.StartsWith('ja/')) {
            $locale = 'ja-JP'
            $routePath = $routePath.Substring(2).TrimStart('/')
        }
        $candidate = Join-Path (Join-Path $contentRoot $locale) $routePath
    } else {
        $candidate = Join-Path (Split-Path -Parent $sourceFile) $pathPart
    }

    if (Test-Path -LiteralPath $candidate) {
        return $true
    }
    if (-not [IO.Path]::HasExtension($candidate) -and
        (Test-Path -LiteralPath "$candidate.md")) {
        return $true
    }
    if (Test-Path -LiteralPath (Join-Path $candidate 'index.md')) {
        return $true
    }
    return $false
}

foreach ($file in $markdownFiles) {
    $content = Get-Content -LiteralPath $file -Raw
    $targets = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($pattern in @(
        '\[[^\]]+\]\(([^)]+)\)',
        '(?m)^\s+link:\s+["'']?([^"''\s]+)',
        '<a\s+[^>]*href=["'']([^"'']+)["'']'
    )) {
        foreach ($match in [regex]::Matches($content, $pattern)) {
            [void]$targets.Add($match.Groups[1].Value)
        }
    }
    foreach ($target in $targets) {
        if (-not (Test-DocumentationTarget $file $target)) {
            throw "Broken documentation link in $file`: $target"
        }
    }
}

$homeShape = $null
$homeHeadingLevels = $null
foreach ($locale in $locales) {
    $homePath = Join-Path $contentRoot "$locale/index.md"
    $homeContent = Get-Content -LiteralPath $homePath -Raw
    $frontmatterMatch = [regex]::Match($homeContent, '\A---\r?\n([\s\S]*?)\r?\n---')
    if (-not $frontmatterMatch.Success) {
        throw "Home page frontmatter is missing: $locale"
    }
    if ($homeContent -match '<(?:div|a|span|strong)\b') {
        throw "Home page uses layout-specific raw HTML: $locale"
    }
    if ([regex]::Matches($frontmatterMatch.Groups[1].Value, '(?m)^\s+linkText:').Count -ne 4) {
        throw "Home page must expose four native feature links: $locale"
    }

    $shape = (@(
        [regex]::Matches($frontmatterMatch.Groups[1].Value, '(?m)^(\s*)(?:-\s+)?([A-Za-z][A-Za-z0-9]*):') |
            ForEach-Object { "$($_.Groups[1].Value.Length):$($_.Groups[2].Value)" }
    ) -join '|')
    $headingLevels = (@(
        [regex]::Matches($homeContent, '(?m)^(#{1,6})\s+') |
            ForEach-Object { $_.Groups[1].Value.Length }
    ) -join ',')
    if ($null -eq $homeShape) {
        $homeShape = $shape
        $homeHeadingLevels = $headingLevels
    } elseif ($homeShape -ne $shape -or $homeHeadingLevels -ne $headingLevels) {
        throw "Home page structure differs for locale: $locale"
    }
}

$projectText = Get-Content -LiteralPath (Join-Path $root 'Live2D.csproj') -Raw
$manifest = Get-Content -LiteralPath (Join-Path $root 'Live2D.json') -Raw | ConvertFrom-Json
$apiText = Get-Content -LiteralPath (Join-Path $root 'Scripts/Api/Live2DApi.cs') -Raw
$entryText = Get-Content -LiteralPath (Join-Path $root 'Scripts/Entry.cs') -Raw
$packText = Get-Content -LiteralPath (Join-Path $root 'Scripts/Packs/Live2DPackArchive.cs') -Raw
$settingsText = Get-Content -LiteralPath (Join-Path $root 'Scripts/Configuration/Live2DSettings.cs') -Raw
$readme = Get-Content -LiteralPath (Join-Path $root 'README.md') -Raw
$apiDoc = Get-Content -LiteralPath (Join-Path $root 'docs/content/zh-CN/reference/api.md') -Raw
$packDoc = Get-Content -LiteralPath (Join-Path $root 'docs/content/zh-CN/reference/pack-format.md') -Raw
$configDoc = Get-Content -LiteralPath (Join-Path $root 'docs/content/zh-CN/reference/configuration.md') -Raw
$siteConfig = Get-Content -LiteralPath (Join-Path $root 'docs/.vitepress/config.mts') -Raw
$englishApiDoc = Get-Content -LiteralPath (Join-Path $root 'docs/content/en-US/reference/api.md') -Raw
$englishPackDoc = Get-Content -LiteralPath (Join-Path $root 'docs/content/en-US/reference/pack-format.md') -Raw
$englishConfigDoc = Get-Content -LiteralPath (Join-Path $root 'docs/content/en-US/reference/configuration.md') -Raw
$japaneseApiDoc = Get-Content -LiteralPath (Join-Path $root 'docs/content/ja-JP/reference/api.md') -Raw
$japanesePackDoc = Get-Content -LiteralPath (Join-Path $root 'docs/content/ja-JP/reference/pack-format.md') -Raw
$japaneseConfigDoc = Get-Content -LiteralPath (Join-Path $root 'docs/content/ja-JP/reference/configuration.md') -Raw

$packageVersion = [regex]::Match($projectText, '<Version>([^<]+)</Version>').Groups[1].Value
$apiVersion = [regex]::Match($apiText, 'ApiVersion\s*=\s*(\d+)').Groups[1].Value
$runtimeVersion = [regex]::Match($entryText, 'ModVersion\s*=\s*"([^"]+)"').Groups[1].Value
$packVersion = [regex]::Match($packText, 'CurrentFormatVersion\s*=\s*(\d+)').Groups[1].Value
$schemaVersion = [regex]::Match($settingsText, 'CurrentSchemaVersion\s*=\s*(\d+)').Groups[1].Value

if ([string]::IsNullOrWhiteSpace($packageVersion) -or
    $manifest.version -ne $packageVersion -or
    $runtimeVersion -ne $packageVersion) {
    throw 'Live2D.csproj, Live2D.json and runtime versions do not match.'
}
if ($readme -notmatch [regex]::Escape("运行时 Mod 版本：``$packageVersion``")) {
    throw "README runtime version is not current: $packageVersion"
}
if ($readme -notmatch [regex]::Escape("公共 API 版本：``$apiVersion``") -or
    $apiDoc -notmatch [regex]::Escape("RuntimeApiVersion == $apiVersion") -or
    $apiDoc -notmatch [regex]::Escape("RuntimeVersion == `"$packageVersion`"")) {
    throw "Public API documentation is not current: $apiVersion"
}
if ($packDoc -notmatch [regex]::Escape("FormatVersion = $packVersion")) {
    throw "Pack format documentation is not current: $packVersion"
}
if ($configDoc -notmatch [regex]::Escape("SchemaVersion`` 为 ``$schemaVersion")) {
    throw "Configuration documentation is not current: $schemaVersion"
}
if ($siteConfig -notmatch [regex]::Escape("v$packageVersion · API $apiVersion")) {
    throw 'Documentation site navigation versions are not current.'
}
foreach ($locale in $locales) {
    $releaseDoc = Get-Content -LiteralPath (Join-Path $contentRoot "$locale/maintainers/release.md") -Raw
    if ($releaseDoc -notmatch [regex]::Escape("ExpectedVersion $packageVersion")) {
        throw "NuGet consumer command is not current for locale: $locale"
    }
}
foreach ($localizedApiDoc in @($englishApiDoc, $japaneseApiDoc)) {
    if ($localizedApiDoc -notmatch [regex]::Escape("RuntimeApiVersion == $apiVersion") -or
        $localizedApiDoc -notmatch [regex]::Escape("RuntimeVersion == `"$packageVersion`"")) {
        throw "Localized public API documentation is not current: $apiVersion"
    }
}
foreach ($localizedPackDoc in @($englishPackDoc, $japanesePackDoc)) {
    if ($localizedPackDoc -notmatch [regex]::Escape("FormatVersion = $packVersion")) {
        throw "Localized Pack documentation is not current: $packVersion"
    }
}
foreach ($localizedConfigDoc in @($englishConfigDoc, $japaneseConfigDoc)) {
    if ($localizedConfigDoc -notmatch [regex]::Escape("SchemaVersion``") -or
        $localizedConfigDoc -notmatch [regex]::Escape("``$schemaVersion``")) {
        throw "Localized configuration documentation is not current: $schemaVersion"
    }
}
foreach ($localeMarker in @("label: '简体中文'", "label: 'English'", "label: '日本語'")) {
    if ($siteConfig -notmatch [regex]::Escape($localeMarker)) {
        throw "Documentation site locale is missing: $localeMarker"
    }
}

$allDocs = ($markdownFiles | ForEach-Object { Get-Content -LiteralPath $_ -Raw }) -join "`n"
if ($allDocs -match 'Live2D\.SmokeTests|[\\/]Tests[\\/]') {
    throw 'Documentation still references the removed Tests project.'
}
if ($allDocs -match 'ref/net10\.0') {
    throw 'Documentation still references the obsolete net10.0 NuGet target.'
}
if (-not (Test-Path -LiteralPath (Join-Path $root 'THIRD-PARTY-NOTICES.md'))) {
    throw 'THIRD-PARTY-NOTICES.md is required for NuGet publication.'
}

Write-Output "Documentation checks passed: Mod $packageVersion, API $apiVersion, Pack $packVersion, Schema $schemaVersion."
