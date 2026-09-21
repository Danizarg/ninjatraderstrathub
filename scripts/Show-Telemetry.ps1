[CmdletBinding()]
param([string]$Path = (Join-Path $env:LOCALAPPDATA 'AdaptiveTradingLab\ninjatrader\executions'))
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $Path)) {
    Write-Host 'No NinjaTrader executions exported yet. Run ATL_EMA_Trend in Strategy Analyzer first.'
    return
}
$files = @(Get-ChildItem -LiteralPath $Path -Filter 'ATL_*.csv' -File | Sort-Object LastWriteTime -Descending)
if ($files.Count -eq 0) { Write-Host 'No execution CSV files found.'; return }
$file = $files[0]
Write-Host "Latest execution file: $($file.FullName)"
$rows = @(Import-Csv -LiteralPath $file.FullName)
Write-Host "Execution events: $($rows.Count). These are fills, not completed trades or a P&L report."
$rows | Select-Object -Last 20 time,instrument,mode,price,quantity,market_position | Format-Table -AutoSize
