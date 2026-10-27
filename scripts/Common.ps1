#requires -Version 7.0
<#
.SYNOPSIS
  Shared helpers for IcoGenerator PowerShell 7 build scripts.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-RepoRoot {
    <#
    .SYNOPSIS
      Returns the repository root (parent of /scripts).
    #>
    $scriptsDir = $PSScriptRoot
    if (-not $scriptsDir) {
        $scriptsDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    }
    return (Resolve-Path (Join-Path $scriptsDir '..')).Path
}

function Get-SolutionPath {
    param([string]$RepoRoot = (Get-RepoRoot))
    $path = Join-Path $RepoRoot 'IcoGenerator.slnx'
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Solution not found: $path"
    }
    return $path
}

function Assert-DotNetSdk {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw "dotnet CLI not found. Install .NET 10 SDK and ensure it is on PATH."
    }

    $sdkList = & dotnet --list-sdks 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to query SDKs: $sdkList"
    }

    $hasNet10 = $sdkList | Where-Object { $_ -match '^10\.' }
    if (-not $hasNet10) {
        Write-Warning "No .NET 10 SDK detected. Installed SDKs:`n$($sdkList -join "`n")"
        throw "IcoGenerator requires .NET 10 SDK (see global.json)."
    }
}

function Write-Step {
    param(
        [Parameter(Mandatory)]
        [string]$Message
    )
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Write-Ok {
    param([string]$Message = 'Done.')
    Write-Host "    $Message" -ForegroundColor Green
}

function Write-Info {
    param([string]$Message)
    Write-Host "    $Message" -ForegroundColor DarkGray
}

function Invoke-DotNet {
    <#
    .SYNOPSIS
      Runs dotnet with the given arguments and fails on non-zero exit.
      Host-writes stdout so callers can safely capture return values.
    #>
    param(
        [Parameter(Mandatory)]
        [string[]]$Arguments
    )

    Write-Info ("dotnet " + ($Arguments -join ' '))

    # Pipe through Out-Host so pipeline callers (e.g. Publish-Cli return values)
    # are not polluted by MSBuild logs.
    & dotnet @Arguments 2>&1 | ForEach-Object {
        if ($_ -is [System.Management.Automation.ErrorRecord]) {
            Write-Host $_.Exception.Message -ForegroundColor Red
        }
        else {
            Write-Host $_
        }
    }

    if ($null -eq $LASTEXITCODE) {
        return
    }
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet exited with code $LASTEXITCODE"
    }
}

function Get-ArtifactsRoot {
    param([string]$RepoRoot = (Get-RepoRoot))
    $dir = Join-Path $RepoRoot 'artifacts'
    if (-not (Test-Path -LiteralPath $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
    return $dir
}

function Get-DefaultRuntime {
    switch ([System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture) {
        'X64'   { return 'win-x64' }
        'X86'   { return 'win-x86' }
        'Arm64' { return 'win-arm64' }
        default { return 'win-x64' }
    }
}
