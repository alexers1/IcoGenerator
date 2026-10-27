#requires -Version 7.0
<#
.SYNOPSIS
  Build (optional) and launch the unified host (GUI if no args; CLI if args given).

.EXAMPLE
  pwsh ./scripts/Run-App.ps1
  pwsh ./scripts/Run-App.ps1 -- .\logo.png -o .\app.ico
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',

    [switch]$NoBuild,

    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$RemainingArgs
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')

Assert-DotNetSdk
$repoRoot = Get-RepoRoot
$appProj = Join-Path $repoRoot 'IcoGenerator.App\IcoGenerator.App.csproj'

$forward = @($RemainingArgs)
if ($forward.Count -gt 0 -and $forward[0] -eq '--') {
    $forward = $forward[1..($forward.Count - 1)]
}

Push-Location $repoRoot
try {
    $dotnetArgs = @(
        'run',
        '--project', $appProj,
        '-c', $Configuration
    )
    if ($NoBuild) {
        $dotnetArgs += '--no-build'
    }
    $dotnetArgs += '--'
    if ($forward) {
        $dotnetArgs += $forward
    }

    if ($forward -and $forward.Count -gt 0) {
        Write-Step 'Run IcoGenerator unified host (CLI mode)'
    }
    else {
        Write-Step 'Run IcoGenerator unified host (GUI mode)'
    }
    Invoke-DotNet -Arguments $dotnetArgs
}
finally {
    Pop-Location
}
