#requires -Version 7.0
<#
.SYNOPSIS
  Clean build outputs and optionally artifacts/.

.PARAMETER Configuration
  Configuration to clean. Default: both Debug and Release via full clean.

.PARAMETER All
  Also remove artifacts/, bin/, obj/ folders under the repo.

.EXAMPLE
  pwsh ./scripts/Clean.ps1

.EXAMPLE
  pwsh ./scripts/Clean.ps1 -All
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release', 'All')]
    [string]$Configuration = 'All',

    [switch]$All
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')

Assert-DotNetSdk
$repoRoot = Get-RepoRoot
$solution = Get-SolutionPath -RepoRoot $repoRoot

Push-Location $repoRoot
try {
    Write-Step 'Clean solution'

    if ($Configuration -eq 'All') {
        foreach ($cfg in @('Debug', 'Release')) {
            Write-Info "dotnet clean -c $cfg"
            & dotnet clean $solution -c $cfg -v minimal --nologo
            if ($LASTEXITCODE -ne 0) {
                throw "dotnet clean failed for $cfg"
            }
        }
    }
    else {
        Invoke-DotNet -Arguments @('clean', $solution, '-c', $Configuration, '-v', 'minimal', '--nologo')
    }

    if ($All) {
        Write-Step 'Remove bin/obj/artifacts'
        $targets = @(
            (Join-Path $repoRoot 'artifacts'),
            (Join-Path $repoRoot 'IcoGenerator.Core\bin'),
            (Join-Path $repoRoot 'IcoGenerator.Core\obj'),
            (Join-Path $repoRoot 'IcoGenerator.Cli\bin'),
            (Join-Path $repoRoot 'IcoGenerator.Cli\obj'),
            (Join-Path $repoRoot 'IcoGenerator.App\bin'),
            (Join-Path $repoRoot 'IcoGenerator.App\obj')
        )
        foreach ($path in $targets) {
            if (Test-Path -LiteralPath $path) {
                Write-Info "Remove $path"
                Remove-Item -LiteralPath $path -Recurse -Force
            }
        }
    }

    Write-Ok 'Clean finished.'
}
finally {
    Pop-Location
}
