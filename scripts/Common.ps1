Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$script:RepoRoot = Split-Path $PSScriptRoot -Parent

function Assert-WindowsX64 {
    if ($env:OS -ne 'Windows_NT' -or -not [Environment]::Is64BitOperatingSystem) {
        throw 'Adaptive Trading Lab requires 64-bit Windows.'
    }
    if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64' -or $env:PROCESSOR_ARCHITEW6432 -eq 'ARM64') {
        throw 'This package targets x64 Windows. Native ARM64 is not supported.'
    }
}

function Get-NinjaTraderHome([string]$RequestedPath) {
    if ($RequestedPath) { $path = [IO.Path]::GetFullPath($RequestedPath) }
    else { $path = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'NinjaTrader 8' }
    if (-not (Test-Path -LiteralPath (Join-Path $path 'bin\Custom\NinjaTrader.Custom.csproj'))) {
        throw "NinjaTrader user folder not found: $path. Start NinjaTrader once, or pass -NinjaTraderHome with its Documents folder (not Program Files)."
    }
    return $path
}
