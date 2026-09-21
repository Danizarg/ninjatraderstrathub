[CmdletBinding(SupportsShouldProcess)]
param([string]$NinjaTraderHome, [switch]$ReplaceExisting)
. "$PSScriptRoot\Common.ps1"
Assert-WindowsX64
$ntHome = Get-NinjaTraderHome $NinjaTraderHome
$source = Join-Path $script:RepoRoot 'ninjatrader\Strategies\ATL_EMA_Trend.cs'
$target = Join-Path $ntHome 'bin\Custom\Strategies\ATL_EMA_Trend.cs'
if (Test-Path -LiteralPath $target) {
    if ((Get-FileHash -LiteralPath $source).Hash -eq (Get-FileHash -LiteralPath $target).Hash) {
        Write-Host "Already installed: $target"
        return
    }
    if (-not $ReplaceExisting) {
        throw 'An existing ATL_EMA_Trend.cs differs. Use -ReplaceExisting to back it up and replace it.'
    }
}
if ($PSCmdlet.ShouldProcess($target, 'Install editable NinjaScript strategy')) {
    if (Test-Path -LiteralPath $target) {
        $backup = Join-Path $env:LOCALAPPDATA ('AdaptiveTradingLab\backups\' + [Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $backup -Force | Out-Null
        Copy-Item -LiteralPath $target -Destination $backup
        Write-Host "Previous strategy backed up to $backup"
    }
    New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
    Copy-Item -LiteralPath $source -Destination $target
    Write-Host "Installed: $target"
    Write-Host 'In NinjaTrader: New > NinjaScript Editor, then F5 to compile.'
    Write-Host 'For a historical test: New > Strategy Analyzer > ATL_EMA_Trend.'
    Write-Host 'Realtime entries default to OFF. Installation does not enable a strategy.'
}
