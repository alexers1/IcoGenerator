#requires -Version 7.0
<#
.SYNOPSIS
  Smoke-test the CLI converter (build if needed, then convert a sample image).

.PARAMETER Configuration
  Build configuration used if a rebuild is required. Default: Release.

.PARAMETER Sizes
  Comma-separated icon sizes. Default: 16,32,48,256

.PARAMETER SkipBuild
  Do not build; use existing output under bin/.

.EXAMPLE
  pwsh ./scripts/Test.ps1

.EXAMPLE
  pwsh ./scripts/Test.ps1 -SkipBuild -Sizes 16,32,256
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [string]$Sizes = '16,32,48,256',

    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')

Assert-DotNetSdk
$repoRoot = Get-RepoRoot

Push-Location $repoRoot
try {
    if (-not $SkipBuild) {
        Write-Step 'Build before smoke test'
        & (Join-Path $PSScriptRoot 'Build.ps1') -Configuration $Configuration
    }

    $sample = Join-Path $repoRoot 'IcoGenerator.App\Assets\StoreLogo.png'
    if (-not (Test-Path -LiteralPath $sample)) {
        throw "Sample image not found: $sample"
    }

    $outDir = Join-Path (Get-ArtifactsRoot -RepoRoot $repoRoot) 'test'
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null
    $outIco = Join-Path $outDir 'smoke-test.ico'
    if (Test-Path -LiteralPath $outIco) {
        Remove-Item -LiteralPath $outIco -Force
    }

    # Prefer unified host (same exe as product); fall back to pure CLI project.
    $hostProj = Join-Path $repoRoot 'IcoGenerator.App\IcoGenerator.App.csproj'
    if (-not (Test-Path -LiteralPath $hostProj)) {
        $hostProj = Join-Path $repoRoot 'IcoGenerator.Cli\IcoGenerator.Cli.csproj'
    }

    Write-Step 'Run conversion via unified host (CLI mode)'
    Invoke-DotNet -Arguments @(
        'run',
        '--project', $hostProj,
        '-c', $Configuration,
        '--no-build',
        '--',
        $sample,
        '-o', $outIco,
        '-s', $Sizes,
        '--skip-env-check'
    )

    if (-not (Test-Path -LiteralPath $outIco)) {
        throw "Expected output not created: $outIco"
    }

    $bytes = [System.IO.File]::ReadAllBytes($outIco)
    if ($bytes.Length -lt 6) {
        throw 'ICO file is too small to be valid.'
    }

    # ICONDIR: reserved=0, type=1 (icon)
    $reserved = [BitConverter]::ToUInt16($bytes, 0)
    $type = [BitConverter]::ToUInt16($bytes, 2)
    $count = [BitConverter]::ToUInt16($bytes, 4)

    if ($reserved -ne 0 -or $type -ne 1 -or $count -lt 1) {
        throw "Invalid ICO header (reserved=$reserved type=$type count=$count)."
    }

    Write-Ok "Smoke test passed."
    Write-Info "Output: $outIco ($($bytes.Length) bytes, $count image(s))"
}
finally {
    Pop-Location
}
