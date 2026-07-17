[CmdletBinding()]
param(
    [string]$Sts2Dir = "",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'Live2D.csproj'
$destination = Join-Path $root 'NuGet/package/ref/net9.0'

$properties = @(
    "-p:Configuration=$Configuration",
    '-p:Live2DCopyToGame=false'
)
if (-not [string]::IsNullOrWhiteSpace($Sts2Dir)) {
    $properties += "-p:Sts2Dir=$Sts2Dir"
}

$buildArguments = @(
    'build', $project,
    '-c', $Configuration,
    '-p:Live2DCopyToGame=false'
)
if (-not [string]::IsNullOrWhiteSpace($Sts2Dir)) {
    $buildArguments += "-p:Sts2Dir=$Sts2Dir"
}

& dotnet @buildArguments
if ($LASTEXITCODE -ne 0) {
    throw "Live2D build failed with exit code $LASTEXITCODE."
}

function Get-MSBuildProperty([string]$name) {
    $output = & dotnet msbuild $project -nologo "-getProperty:$name" @properties
    if ($LASTEXITCODE -ne 0) {
        throw "Could not evaluate MSBuild property: $name"
    }
    return ($output | Select-Object -Last 1).Trim()
}

$referenceAssembly = Get-MSBuildProperty 'TargetRefPath'
$documentation = Get-MSBuildProperty 'DocumentationFile'
foreach ($asset in @($referenceAssembly, $documentation)) {
    if ([string]::IsNullOrWhiteSpace($asset) -or -not (Test-Path -LiteralPath $asset -PathType Leaf)) {
        throw "Generated NuGet asset was not found: $asset"
    }
}

New-Item -ItemType Directory -Force -Path $destination | Out-Null
Copy-Item -LiteralPath $referenceAssembly -Destination (Join-Path $destination 'Live2D.dll') -Force
Copy-Item -LiteralPath $documentation -Destination (Join-Path $destination 'Live2D.xml') -Force

Write-Output "Updated prepared NuGet assets in $destination"
Write-Output 'Review and commit Live2D.dll and Live2D.xml together with the public API changes.'
