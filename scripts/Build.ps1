#requires -Version 7.0
<#
.SYNOPSIS
  Restore and build the IcoGenerator solution (unified GUI+CLI host + optional pure CLI).

.PARAMETER Configuration
  Debug or Release. Default: Release.

.PARAMETER Clean
  Run `dotnet clean` before building.

.PARAMETER NoRestore
  Skip restore.

.PARAMETER Verbosity
  MSBuild verbosity.

.EXAMPLE
  pwsh ./scripts/Build.ps1
  pwsh ./scripts/Build.ps1 -Configuration Debug -Clean
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [switch]$Clean,

    [switch]$NoRestore,

    [ValidateSet('quiet', 'minimal', 'normal', 'detailed', 'diagnostic')]
    [string]$Verbosity = 'minimal'
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')

Assert-DotNetSdk
$repoRoot = Get-RepoRoot
$solution = Get-SolutionPath -RepoRoot $repoRoot

Push-Location $repoRoot
try {
    Write-Step "IcoGenerator build ($Configuration)"
    Write-Info "Repo: $repoRoot"
    Write-Info "Solution: $solution"
    Write-Info ("dotnet: " + (dotnet --version))
    Write-Info "Unified host project: IcoGenerator.App -> IcoGenerator.exe (GUI + CLI)"

    if ($Clean) {
        Write-Step 'Clean'
        Invoke-DotNet -Arguments @('clean', $solution, '-c', $Configuration, '-v', $Verbosity)
        Write-Ok
    }

    if (-not $NoRestore) {
        Write-Step 'Restore'
        Invoke-DotNet -Arguments @('restore', $solution, '-v', $Verbosity)
        Write-Ok
    }

    Write-Step 'Build'
    $buildArgs = @(
        'build', $solution,
        '-c', $Configuration,
        '-v', $Verbosity,
        '--nologo'
    )
    if ($NoRestore) {
        $buildArgs += '--no-restore'
    }
    Invoke-DotNet -Arguments $buildArgs
    Write-Ok "Build succeeded ($Configuration)."
}
finally {
    Pop-Location
}
