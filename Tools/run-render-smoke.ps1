[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$GodotPath,

    [Parameter(Mandatory = $true)]
    [string]$Sts2Dir,

    [switch]$CaptureImage
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $PSScriptRoot 'RenderSmoke'
$projectFile = Join-Path $project 'Live2DRenderSmoke.csproj'
$godot = (Resolve-Path -LiteralPath $GodotPath).Path
$game = (Resolve-Path -LiteralPath $Sts2Dir).Path

if (Test-Path -LiteralPath $projectFile) {
    throw "Temporary project file already exists: $projectFile"
}

$projectXml = @'
<Project Sdk="Godot.NET.Sdk/4.5.1">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>true</ImplicitUsings>
    <Nullable>enable</Nullable>
    <NoWarn>$(NoWarn);CS0436</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\Live2D.csproj" AdditionalProperties="Live2DCopyToGame=false" />
    <PackageReference Include="STS2.RitsuLib.Compat.0.107.1" Version="0.4.56" />
    <Reference Include="sts2" HintPath="$(Sts2Dir)\data_sts2_windows_x86_64\sts2.dll" Private="true" />
  </ItemGroup>
</Project>
'@
[IO.File]::WriteAllText($projectFile, $projectXml, [Text.UTF8Encoding]::new($false))

try {
    dotnet build $projectFile `
        -p:Live2DCopyToGame=false `
        "-p:Sts2Dir=$game"
    if ($LASTEXITCODE -ne 0) {
        throw "Render smoke project build failed with exit code $LASTEXITCODE."
    }

    $headlessOutput = & $godot `
        --headless `
        --path $project `
        --rendering-method gl_compatibility 2>&1 | Out-String
    Write-Output $headlessOutput.TrimEnd()
    if ($LASTEXITCODE -ne 0 -or $headlessOutput -notmatch 'LIVE2D_RENDER_SMOKE_OK') {
        throw "Headless render smoke failed with exit code $LASTEXITCODE."
    }

    if (-not $CaptureImage) {
        return
    }

    $artifacts = Join-Path $root 'artifacts'
    New-Item -ItemType Directory -Force -Path $artifacts | Out-Null
    $capturePath = Join-Path $artifacts 'render-smoke.png'
    $gpuOutput = & $godot `
        --path $project `
        --rendering-method gl_compatibility `
        --position 20000,20000 `
        -- "--capture-path=$capturePath" 2>&1 | Out-String
    Write-Output $gpuOutput.TrimEnd()
    if ($LASTEXITCODE -ne 0 -or
        $gpuOutput -notmatch 'LIVE2D_RENDER_SMOKE_OK' -or
        -not (Test-Path -LiteralPath $capturePath)) {
        throw "GPU render smoke failed with exit code $LASTEXITCODE."
    }

    Write-Output "Render smoke image: $capturePath"
}
finally {
    Remove-Item -LiteralPath $projectFile -Force -ErrorAction SilentlyContinue
}
