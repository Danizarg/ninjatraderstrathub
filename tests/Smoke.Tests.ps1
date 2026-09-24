# Runs without Pester or an SDK; compatible with Windows PowerShell 5.1.
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$testRoot = Join-Path $repo ('artifacts\test with spaces ' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $testRoot | Out-Null
function Assert($Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
foreach ($file in Get-ChildItem (Join-Path $repo 'scripts') -Filter '*.ps1') {
    $parseErrors = $null
    $tokens = $null
    [System.Management.Automation.Language.Parser]::ParseFile($file.FullName, [ref]$tokens, [ref]$parseErrors) | Out-Null
    Assert ($parseErrors.Count -eq 0) "Parse errors in $($file.Name): $parseErrors"
}
$fakeHome = Join-Path $testRoot 'NinjaTrader 8'
$custom = Join-Path $fakeHome 'bin\Custom'
New-Item -ItemType Directory -Path $custom -Force | Out-Null
Set-Content (Join-Path $custom 'NinjaTrader.Custom.csproj') '<Project />'
$installer = Join-Path $repo 'scripts\Install-NinjaTrader.ps1'
$target = Join-Path $custom 'Strategies\ATL_EMA_Trend.cs'
& $installer -NinjaTraderHome $fakeHome -WhatIf
Assert (-not (Test-Path $target)) '-WhatIf wrote a strategy'
& $installer -NinjaTraderHome $fakeHome
Assert (Test-Path $target) 'Installer did not install the strategy'
& $installer -NinjaTraderHome $fakeHome
Add-Content $target '// Existing user customization'
$refused = $false
try { & $installer -NinjaTraderHome $fakeHome } catch { $refused = $true }
Assert $refused 'Installer overwrote a customized strategy'
Assert ((Get-Content $target -Raw).Contains('Existing user customization')) 'User customization was lost'
$refused = $false
try { & $installer -NinjaTraderHome (Join-Path $testRoot 'missing') } catch { $refused = $true }
Assert $refused 'Installer accepted an invalid NinjaTrader folder'
Write-Host 'PASS: script parsing, WhatIf, installation, idempotency, overwrite protection and invalid path.'
