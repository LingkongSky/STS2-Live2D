[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackagePath,

    [Parameter(Mandatory = $true)]
    [string]$ExpectedVersion
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$package = (Resolve-Path -LiteralPath $PackagePath).Path
$packageDirectory = Split-Path -Parent $package
$expectedName = "STS2.Live2D.$ExpectedVersion.nupkg"
if ([IO.Path]::GetFileName($package) -ne $expectedName) {
    throw "Expected package '$expectedName', got '$package'."
}

$tempRoot = Join-Path ([IO.Path]::GetTempPath()) "sts2-live2d-package-smoke-$([guid]::NewGuid().ToString('N'))"
$projectDirectory = Join-Path $tempRoot 'Live2DPackageSmoke'
$projectFile = Join-Path $projectDirectory 'Live2DPackageSmoke.csproj'
$nugetConfig = Join-Path $tempRoot 'NuGet.Config'

try {
    dotnet new classlib --name Live2DPackageSmoke --output $projectDirectory --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "Could not create temporary consumer project (exit code $LASTEXITCODE)."
    }

    $projectXml = [IO.File]::ReadAllText($projectFile)
    $projectXml = [regex]::Replace(
        $projectXml,
        '<TargetFramework>[^<]+</TargetFramework>',
        '<TargetFramework>net9.0</TargetFramework>')
    [IO.File]::WriteAllText($projectFile, $projectXml, [Text.UTF8Encoding]::new($false))

    Remove-Item -LiteralPath (Join-Path $projectDirectory 'Class1.cs')
    Copy-Item -LiteralPath (Join-Path $root 'Tools/ApiConsumerExample/ExampleLive2DController.cs') `
        -Destination $projectDirectory

    dotnet add $projectFile package STS2.Live2D `
        --version $ExpectedVersion `
        --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "Could not add the packaged API (exit code $LASTEXITCODE)."
    }

    $escapedPackageDirectory = [Security.SecurityElement]::Escape($packageDirectory)
    $nugetConfigXml = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="package-under-test" value="$escapedPackageDirectory" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
</configuration>
"@
    [IO.File]::WriteAllText($nugetConfig, $nugetConfigXml, [Text.UTF8Encoding]::new($false))

    dotnet restore $projectFile --configfile $nugetConfig
    if ($LASTEXITCODE -ne 0) {
        throw "Could not restore the packaged API (exit code $LASTEXITCODE)."
    }

    dotnet build $projectFile -c Release --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "Temporary consumer build failed (exit code $LASTEXITCODE)."
    }

    $output = Join-Path $projectDirectory 'bin/Release/net9.0'
    if (Test-Path -LiteralPath (Join-Path $output 'Live2D.dll')) {
        throw 'Compile-only Live2D.dll was copied to the consumer output.'
    }

    Write-Output "NuGet consumer validation passed: STS2.Live2D $ExpectedVersion."
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
