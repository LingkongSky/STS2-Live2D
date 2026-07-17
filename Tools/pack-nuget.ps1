[CmdletBinding()]
param(
    [string]$OutputDirectory = "",
    [switch]$RequireClean
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'NuGet/STS2.Live2D.Package.csproj'
$runtimeProject = Join-Path $root 'Live2D.csproj'

if ($RequireClean) {
    $status = (& git -C $root status --porcelain | Out-String).Trim()
    if (-not [string]::IsNullOrWhiteSpace($status)) {
        throw "Working tree must be clean before release:`n$status"
    }
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $root 'artifacts'
}
$output = [IO.Path]::GetFullPath($OutputDirectory, $root)
New-Item -ItemType Directory -Force -Path $output | Out-Null

function Get-ProjectVersion([string]$path) {
    [xml]$xml = Get-Content -LiteralPath $path -Raw
    $node = $xml.SelectSingleNode('/Project/PropertyGroup/Version')
    if ($null -eq $node -or [string]::IsNullOrWhiteSpace($node.InnerText)) {
        throw "Could not read a literal Version from $path."
    }
    return $node.InnerText.Trim()
}

# Read literal XML values so CI never has to resolve the Godot SDK merely to
# compare versions.
$version = Get-ProjectVersion $project
$runtimeVersion = Get-ProjectVersion $runtimeProject
if ($runtimeVersion -ne $version) {
    throw "NuGet version '$version' does not match Live2D runtime version '$runtimeVersion'."
}

$referenceAssembly = Join-Path $root 'NuGet/package/ref/net9.0/Live2D.dll'
$referenceDocumentation = Join-Path $root 'NuGet/package/ref/net9.0/Live2D.xml'
foreach ($asset in @($referenceAssembly, $referenceDocumentation)) {
    if (-not (Test-Path -LiteralPath $asset -PathType Leaf)) {
        throw "Prepared NuGet asset is missing: $asset. Run Tools/update-nuget-reference.ps1 locally."
    }
}

$commit = (& git -C $root rev-parse HEAD | Out-String).Trim()

$restoreArguments = @(
    'restore', $project,
    '--force',
    '-p:ContinuousIntegrationBuild=true',
    "-p:RepositoryCommit=$commit"
)
& dotnet @restoreArguments
if ($LASTEXITCODE -ne 0) {
    throw "dotnet restore failed with exit code $LASTEXITCODE."
}

$packArguments = @(
    'pack', $project,
    '-o', $output,
    '--no-build',
    '--no-restore',
    '-p:ContinuousIntegrationBuild=true',
    "-p:RepositoryCommit=$commit"
)

& dotnet @packArguments
if ($LASTEXITCODE -ne 0) {
    throw "dotnet pack failed with exit code $LASTEXITCODE."
}

$package = Join-Path $output "STS2.Live2D.$version.nupkg"
if (-not (Test-Path -LiteralPath $package)) {
    throw "Expected NuGet package was not created: $package"
}

& (Join-Path $PSScriptRoot 'validate-nuget-package.ps1') `
    -PackagePath $package `
    -ExpectedVersion $version `
    -ExpectedCommit $commit

Write-Output $package
