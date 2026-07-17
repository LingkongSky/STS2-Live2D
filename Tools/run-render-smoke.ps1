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
$godot = (Resolve-Path -LiteralPath $GodotPath).Path
$game = (Resolve-Path -LiteralPath $Sts2Dir).Path

dotnet build (Join-Path $project 'Live2DRenderSmoke.csproj') `
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
