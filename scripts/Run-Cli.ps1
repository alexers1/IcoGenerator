#requires -Version 7.0
<#
.SYNOPSIS
  Build (optional) and run the CLI, forwarding remaining arguments.

.EXAMPLE
  pwsh ./scripts/Run-Cli.ps1 -- .\logo.png -o .\app.ico

.EXAMPLE
  pwsh ./scripts/Run-Cli.ps1 -Configuration Debug -- --help
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
$cliProj = Join-Path $repoRoot 'IcoGenerator.Cli\IcoGenerator.Cli.csproj'

# Allow callers to pass a literal "--" separator before CLI args.
$forward = @($RemainingArgs)
if ($forward.Count -gt 0 -and $forward[0] -eq '--') {
    $forward = $forward[1..($forward.Count - 1)]
}

Push-Location $repoRoot
try {
    $args = @(
        'run',
        '--project', $cliProj,
        '-c', $Configuration
    )
    if ($NoBuild) {
        $args += '--no-build'
    }
    $args += '--'
    if ($forward) {
        $args += $forward
    }

    Write-Step 'Run IcoGenerator CLI'
    Invoke-DotNet -Arguments $args
}
finally {
    Pop-Location
}
