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

function Get-LabRuntime {
    Assert-WindowsX64
    $archive = Join-Path $script:RepoRoot 'vendor\AdaptiveTradingLab-windows-x64.zip'
    $expected = '7D2583C60E0FAD11443859A8723288E2A8444A7F41D37C8A0047C6CC7052D695'
    if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $expected) {
        throw 'The supplied runtime archive checksum does not match. Restore the original vendor ZIP.'
    }
    $runtime = Join-Path $script:RepoRoot '.runtime\windows-x64'
    $marker = Join-Path $runtime '.extracted'
    if (-not (Test-Path -LiteralPath $marker)) {
        New-Item -ItemType Directory -Path $runtime -Force | Out-Null
        Expand-Archive -LiteralPath $archive -DestinationPath $runtime -Force
        Set-Content -LiteralPath $marker -Value $expected -Encoding ASCII
    }
    $exe = Join-Path $runtime 'AdaptiveTradingLab.Cli.exe'
    if (-not (Test-Path -LiteralPath $exe)) { throw 'Runtime is incomplete. Remove .runtime and launch again.' }
    return $exe
}
