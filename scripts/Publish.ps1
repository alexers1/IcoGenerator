#requires -Version 7.0
<#
.SYNOPSIS
  Publish IcoGenerator as ONE self-contained single-file .exe (default).

.DESCRIPTION
  Default Target=Unified:
    artifacts/unified/<rid>/IcoGenerator.exe   (single file, ~220 MB)

  Why self-contained?
    WinUI 3 GUI + PublishSingleFile only works reliably when the .NET runtime
    AND Windows App SDK are packed into the exe. Framework-dependent single-file
    can run CLI mode, but double-click GUI crashes (Microsoft.UI.Xaml 0xc000027b).

  Behavior of IcoGenerator.exe:
    Double-click / no args  -> GUI
    With conversion args    -> CLI

.PARAMETER Target
  Unified | Cli | App | All. Default: Unified.

.EXAMPLE
  pwsh ./scripts/Publish.ps1
#>
[CmdletBinding()]
param(
    [ValidateSet('Unified', 'Cli', 'App', 'All')]
    [string]$Target = 'Unified',

    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [ValidateSet('win-x64', 'win-x86', 'win-arm64')]
    [string]$Runtime,

    # Pure CLI only. Unified is always self-contained single-file.
    [switch]$SelfContained,

    [string]$Output
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')

if ([string]::IsNullOrWhiteSpace($Runtime)) {
    $Runtime = Get-DefaultRuntime
}

if ($Target -eq 'App') {
    $Target = 'Unified'
}

Assert-DotNetSdk
$repoRoot = Get-RepoRoot
$artifactsRoot = if ($Output) {
    if (-not (Test-Path -LiteralPath $Output)) {
        New-Item -ItemType Directory -Path $Output -Force | Out-Null
    }
    (Resolve-Path -LiteralPath $Output).Path
}
else {
    Get-ArtifactsRoot -RepoRoot $repoRoot
}

function Clear-PublishDir([string]$outDir) {
    if (Test-Path -LiteralPath $outDir) {
        Remove-Item -LiteralPath $outDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null
}

function Publish-Unified {
    $outDir = Join-Path $artifactsRoot "unified\$Runtime"
    Clear-PublishDir $outDir

    Write-Step "Publish UNIFIED single-file (self-contained) -> $outDir"
    Write-Info "ONE exe: IcoGenerator.exe (GUI + CLI). Runtime is inside the file."
    $proj = Join-Path $repoRoot 'IcoGenerator.App\IcoGenerator.App.csproj'

    Invoke-DotNet -Arguments @(
        'publish', $proj,
        '-c', $Configuration,
        '-r', $Runtime,
        '--self-contained', 'true',
        '-p:SelfContained=true',
        '-p:PublishSingleFile=true',
        '-p:IncludeNativeLibrariesForSelfExtract=true',
        '-p:IncludeAllContentForSelfExtract=true',
        '-p:EnableCompressionInSingleFile=false',
        '-p:PublishReadyToRun=false',
        '-p:PublishTrimmed=false',
        '-p:WindowsAppSDKSelfContained=true',
        '-p:WindowsAppSdkBootstrapInitialize=false',
        '-p:WindowsAppSdkDeploymentManagerInitialize=false',
        '-p:WindowsPackageType=None',
        '-o', $outDir,
        '--nologo'
    )

    # NOTE: Do NOT rename IcoGenerator.App.exe — renaming single-file apphost breaks GUI.
    $exe = Join-Path $outDir 'IcoGenerator.App.exe'
    if (-not (Test-Path -LiteralPath $exe)) {
        throw "Publish did not produce IcoGenerator.App.exe in $outDir"
    }

    # Drop pdb/noise so only the exe (and readme) remain
    Get-ChildItem -LiteralPath $outDir -File |
        Where-Object { $_.Name -notin @('IcoGenerator.App.exe', 'README.txt') } |
        ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force -ErrorAction SilentlyContinue }

    $sizeMb = [math]::Round((Get-Item -LiteralPath $exe).Length / 1MB, 2)
    @"
IcoGenerator — single EXE
=========================
IcoGenerator.App.exe  ($sizeMb MB)

GUI:  double-click IcoGenerator.App.exe
CLI:  IcoGenerator.App.exe logo.png -o app.ico -s 16,32,48,256

Self-contained single-file (runtime inside the exe). No DLL folder.
First run may extract under %TEMP%\.net\

IMPORTANT: Do NOT rename this .exe (breaks WinUI single-file host).
Create a desktop shortcut named "IcoGenerator" if you want a nicer name.
"@ | Set-Content -LiteralPath (Join-Path $outDir 'README.txt') -Encoding UTF8

    Write-Ok "Single-file: $exe  ($sizeMb MB)"
    $left = @(Get-ChildItem -LiteralPath $outDir -File | Select-Object -ExpandProperty Name)
    Write-Info "Folder: $($left -join ', ')"
    return , $exe
}

function Publish-PureCli {
    $outDir = Join-Path $artifactsRoot "cli\$Runtime"
    Clear-PublishDir $outDir

    Write-Step "Publish pure CLI single-file (FDD by default) -> $outDir"
    $self = if ($SelfContained) { 'true' } else { 'false' }
    $compress = if ($SelfContained) { 'true' } else { 'false' }
    $proj = Join-Path $repoRoot 'IcoGenerator.Cli\IcoGenerator.Cli.csproj'

    Invoke-DotNet -Arguments @(
        'publish', $proj,
        '-c', $Configuration,
        '-r', $Runtime,
        '--self-contained', $self,
        "-p:SelfContained=$self",
        '-p:PublishSingleFile=true',
        '-p:IncludeNativeLibrariesForSelfExtract=true',
        "-p:EnableCompressionInSingleFile=$compress",
        '-o', $outDir,
        '--nologo'
    )

    $exe = Join-Path $outDir 'IcoGenerator.Cli.exe'
    if (-not (Test-Path -LiteralPath $exe)) {
        throw "Expected CLI output missing: $exe"
    }
    Write-Ok "CLI: $exe"
    return , $exe
}

Push-Location $repoRoot
try {
    Write-Step "IcoGenerator publish (Target=$Target, RID=$Runtime, Config=$Configuration)"
    Write-Info "Artifacts: $artifactsRoot"

    $results = [System.Collections.Generic.List[string]]::new()

    if ($Target -in @('Unified', 'All')) {
        [void]$results.Add((Publish-Unified))
    }
    if ($Target -in @('Cli', 'All')) {
        [void]$results.Add((Publish-PureCli))
    }

    Write-Step 'Publish summary'
    foreach ($r in $results) {
        Write-Host "  - $r" -ForegroundColor Green
    }
}
finally {
    Pop-Location
}
