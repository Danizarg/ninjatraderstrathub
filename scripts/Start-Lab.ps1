[CmdletBinding()]
param([string]$OutputRoot = (Join-Path $env:LOCALAPPDATA 'AdaptiveTradingLab'))
. "$PSScriptRoot\Common.ps1"
try {
    $exe = Get-LabRuntime
    Write-Host 'Adaptive Trading Lab - synthetic 100-trade demo'
    Write-Host 'For NinjaTrader integration, run Setup-NinjaTrader.cmd once.'
    $lines = @(& $exe ([IO.Path]::GetFullPath($OutputRoot)))
    $code = $LASTEXITCODE
    $lines | ForEach-Object { Write-Host $_ }
    if ($code -ne 0) { throw "Lab exited with code $code" }
    $runLine = $lines | Where-Object { $_ -like 'Run directory: *' } | Select-Object -First 1
    $statusLine = $lines | Where-Object { $_ -like 'Latest cycle status: *' } | Select-Object -First 1
    if ($runLine) {
        $runPath = $runLine.Substring('Run directory: '.Length)
        $status = if ($statusLine) { $statusLine.Substring('Latest cycle status: '.Length) } else { 'Not reported' }
        @{ learningStatus = $status; source = 'synthetic-demo'; completedUtc = [DateTime]::UtcNow.ToString('o') } |
            ConvertTo-Json | Set-Content -LiteralPath (Join-Path $runPath 'desktop-run.json') -Encoding UTF8
    }
    Write-Host ''
    Write-Host 'The demo output is synthetic. The maintained NinjaTrader strategy is ATL_EMA_Trend.'
    Write-Host 'Use scripts\Show-Telemetry.ps1 to inspect executions exported by that strategy.'
} catch { Write-Error $_; exit 1 }
