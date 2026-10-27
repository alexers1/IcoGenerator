#requires -Version 7.0
<#
.SYNOPSIS
  Root convenience launcher for IcoGenerator tasks.

.DESCRIPTION
  Tasks:
    build     - build solution
    clean     - clean outputs
    publish   - publish unified (default) or cli
    test      - CLI smoke test
    run-cli   - run pure CLI project
    run-app   - run unified host (GUI if no args)
    run       - alias of run-app (unified)

.EXAMPLE
  pwsh ./build.ps1
  pwsh ./build.ps1 publish
  pwsh ./build.ps1 publish -Target Unified -Runtime win-x64
  pwsh ./build.ps1 run -- .\logo.png -o .\app.ico
#>
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$knownTasks = @('build', 'clean', 'publish', 'test', 'run-cli', 'run-app', 'run')
$map = @{
    'build'   = 'Build.ps1'
    'clean'   = 'Clean.ps1'
    'publish' = 'Publish.ps1'
    'test'    = 'Test.ps1'
    'run-cli' = 'Run-Cli.ps1'
    'run-app' = 'Run-App.ps1'
    'run'     = 'Run-App.ps1'
}

$task = 'build'
$startIndex = 0

if ($args.Count -gt 0 -and $knownTasks -contains [string]$args[0]) {
    $task = [string]$args[0]
    $startIndex = 1
}
elseif ($args.Count -gt 0 -and [string]$args[0] -notin $knownTasks -and [string]$args[0] -notlike '-*') {
    throw "Unknown task '$($args[0])'. Expected one of: $($knownTasks -join ', ')"
}

$forward = @()
if ($args.Count -gt $startIndex) {
    $forward = $args[$startIndex..($args.Count - 1)]
}

$scriptPath = Join-Path $PSScriptRoot "scripts\$($map[$task])"
if (-not (Test-Path -LiteralPath $scriptPath)) {
    throw "Script not found: $scriptPath"
}

& $scriptPath @forward
if ($null -ne $LASTEXITCODE -and $LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
