[CmdletBinding()]
param([string]$OutputRoot = (Join-Path $env:LOCALAPPDATA 'AdaptiveTradingLab'))
. "$PSScriptRoot\Common.ps1"
try {
    $exe = Get-LabRuntime
    Write-Host 'Adaptive Trading Lab - synthetic 100-trade demo'
    Write-Host 'For NinjaTrader integration, run Setup-NinjaTrader.cmd once.'
    & $exe ([IO.Path]::GetFullPath($OutputRoot))
    if ($LASTEXITCODE -ne 0) { throw "Lab exited with code $LASTEXITCODE" }
    Write-Host ''
    Write-Host 'The demo output is synthetic. The maintained NinjaTrader strategy is ATL_EMA_Trend.'
    Write-Host 'Use scripts\Show-Telemetry.ps1 to inspect executions exported by that strategy.'
} catch { Write-Error $_; exit 1 }
