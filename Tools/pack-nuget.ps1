[CmdletBinding()]
param(
    [string]$Sts2Dir = "",
    [string]$Sts2ApiSignatureRoot = "",
    [string]$Configuration = "Release",
    [string]$OutputDirectory = "",
    [switch]$RequireClean
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'Live2D.csproj'

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

$msbuildProperties = @()
if (-not [string]::IsNullOrWhiteSpace($Sts2Dir)) {
    $msbuildProperties += "-p:Sts2Dir=$Sts2Dir"
}
if (-not [string]::IsNullOrWhiteSpace($Sts2ApiSignatureRoot)) {
    $msbuildProperties += "-p:Sts2ApiSignatureRoot=$Sts2ApiSignatureRoot"
}

$versionOutput = & dotnet msbuild $project -nologo -getProperty:PackageVersion @msbuildProperties
if ($LASTEXITCODE -ne 0) {
    throw 'Could not evaluate PackageVersion.'
}
$version = ($versionOutput | Select-Object -Last 1).Trim()
if ([string]::IsNullOrWhiteSpace($version)) {
    throw 'PackageVersion evaluated to an empty value.'
}

$packArguments = @(
    'pack', $project,
    '-c', $Configuration,
    '-o', $output,
    '--force',
    '-p:Live2DCopyToGame=false',
    '-p:ContinuousIntegrationBuild=true'
) + $msbuildProperties

& dotnet @packArguments
if ($LASTEXITCODE -ne 0) {
    throw "dotnet pack failed with exit code $LASTEXITCODE."
}

$package = Join-Path $output "STS2.Live2D.$version.nupkg"
if (-not (Test-Path -LiteralPath $package)) {
    throw "Expected NuGet package was not created: $package"
}

$commit = (& git -C $root rev-parse HEAD | Out-String).Trim()
& (Join-Path $PSScriptRoot 'validate-nuget-package.ps1') `
    -PackagePath $package `
    -ExpectedVersion $version `
    -ExpectedCommit $commit

Write-Output $package
