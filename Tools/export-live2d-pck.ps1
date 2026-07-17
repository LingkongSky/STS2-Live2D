param(
    [string]$GodotExecutable = "",
    [string]$Configuration = "Debug",

    [Parameter(Mandatory = $true)]
    [string]$Sts2Dir
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$pckPath = Join-Path $projectRoot "Live2D.pck"
$game = (Resolve-Path -LiteralPath $Sts2Dir).Path

if ([string]::IsNullOrWhiteSpace($GodotExecutable)) {
    $bundledGodotRoot = Join-Path $projectRoot ".tools\godot-4.5.1-mono\Godot_v4.5.1-stable_mono_win64"
    $godotCandidates = @(
        (Join-Path $bundledGodotRoot "Godot_v4.5.1-stable_mono_win64_console.exe"),
        (Join-Path $bundledGodotRoot "Godot_v4.5.1-stable_mono_win64.exe")
    )
    $GodotExecutable = $godotCandidates |
        Where-Object { Test-Path -LiteralPath $_ } |
        Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($GodotExecutable)) {
        $GodotExecutable = $godotCandidates[0]
    }
}

if (-not (Test-Path -LiteralPath $GodotExecutable)) {
    throw "Godot 4.5.1 Mono was not found: $GodotExecutable"
}

function Invoke-Godot([string[]]$Arguments) {
    # Godot's Windows binary uses the GUI subsystem, so PowerShell's call operator
    # can return before it exits. Start-Process -Wait gives a reliable exit code.
    $process = Start-Process -FilePath $GodotExecutable -ArgumentList $Arguments -Wait -PassThru -NoNewWindow
    return $process.ExitCode
}

$importExitCode = Invoke-Godot @("--headless", "--editor", "--path", $projectRoot, "--quit")
if ($importExitCode -ne 0) {
    throw "Godot failed to import project resources before export."
}

$exportExitCode = Invoke-Godot @("--headless", "--path", $projectRoot, "--export-pack", "Live2D", $pckPath)
if ($exportExitCode -ne 0 -or -not (Test-Path -LiteralPath $pckPath)) {
    throw "Godot failed to export $pckPath"
}

$verifierRoot = Join-Path $PSScriptRoot "PckVerifier"
$verifyExitCode = Invoke-Godot @("--headless", "--path", $verifierRoot, "--script", (Join-Path $verifierRoot "verify_pck.gd"), "--", $pckPath)
if ($verifyExitCode -ne 0) {
    throw "PCK verification failed: $pckPath"
}

dotnet build (Join-Path $projectRoot "Live2D.csproj") `
    -c $Configuration `
    --no-restore `
    "-p:Sts2Dir=$game"
if ($LASTEXITCODE -ne 0) {
    throw "Live2D build failed after PCK export."
}

Write-Host "Live2D release resources exported, verified, built, and deployed: $pckPath"
