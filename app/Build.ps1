[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'Install .NET Framework 4.8 on Windows to build the desktop app.' }
$references = @('System.dll','System.Core.dll','System.Xml.dll','System.Xml.Linq.dll','System.Xaml.dll','System.Web.Extensions.dll','System.Windows.Forms.dll','System.Drawing.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll')
$arguments = @('/nologo','/target:winexe','/platform:x64','/optimize+',('/out:' + (Join-Path $repo 'AdaptiveTradingLab.exe')),('/resource:' + (Join-Path $PSScriptRoot 'MainWindow.xaml') + ',MainWindow.xaml'))
foreach ($reference in $references) { $arguments += '/reference:' + (Join-Path $framework $reference) }
$arguments += Join-Path $PSScriptRoot 'LabData.cs'
$arguments += Join-Path $PSScriptRoot 'Program.cs'
$arguments += Join-Path $PSScriptRoot 'Adaptation.cs'
$arguments += Join-Path $PSScriptRoot 'StrategyLibrary.cs'
$arguments += Join-Path $PSScriptRoot 'AdaptationUi.cs'
$arguments += '/resource:' + (Join-Path $repo 'ninjatrader\Strategies\ATL_EMA_Trend.cs') + ',StrategyTemplate.cs'
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'Desktop build failed.' }
Write-Host 'Built AdaptiveTradingLab.exe (Windows x64, .NET Framework 4.8).'
