[CmdletBinding()]
param(
    [string]$NinjaTraderHome,
    [string]$NinjaTraderBin = (Join-Path $env:ProgramFiles 'NinjaTrader 8\bin')
)
. "$PSScriptRoot\Common.ps1"
$ntHome = Get-NinjaTraderHome $NinjaTraderHome
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
$references = @(
    (Join-Path $NinjaTraderBin 'NinjaTrader.Core.dll'),
    (Join-Path $NinjaTraderBin 'NinjaTrader.Gui.dll'),
    (Join-Path $ntHome 'bin\Custom\NinjaTrader.Custom.dll'),
    (Join-Path $framework 'System.ComponentModel.DataAnnotations.dll'),
    (Join-Path $framework 'WPF\WindowsBase.dll'),
    (Join-Path $framework 'WPF\PresentationCore.dll'),
    (Join-Path $framework 'WPF\PresentationFramework.dll')
)
foreach ($file in @($compiler) + $references) {
    if (-not (Test-Path -LiteralPath $file)) { throw "Missing compile dependency: $file" }
}
$output = Join-Path $script:RepoRoot 'artifacts\compile-check'
New-Item -ItemType Directory -Force -Path $output | Out-Null
$arguments = @('/nologo', '/target:library', '/platform:x64', ('/out:' + (Join-Path $output 'ATL.CompileCheck.dll')))
foreach ($reference in $references) { $arguments += '/reference:' + $reference }
$arguments += Join-Path $script:RepoRoot 'ninjatrader\Strategies\ATL_EMA_Trend.cs'
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'NinjaTrader reference compilation failed.' }
Write-Host 'PASS: strategy compiles against the installed NinjaTrader libraries (.NET Framework).'
Write-Host 'This checks API compatibility; use F5 in NinjaScript Editor for the full NinjaTrader compilation.'
