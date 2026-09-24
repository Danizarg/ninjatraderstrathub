[CmdletBinding()]
param([string]$NativeReport)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
& (Join-Path $repo 'app\Build.ps1')
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$root = Join-Path $repo ('artifacts\t ' + [Guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Path $root -Force | Out-Null
$exe = Join-Path $root 'DesktopData.Tests.exe'
Copy-Item (Join-Path $repo 'AdaptiveTradingLab.exe') $root
Copy-Item (Join-Path $repo 'AdaptiveTradingLab.exe.config') ($exe + '.config')
$references = @('System.dll','System.Core.dll','System.Xml.dll','System.Xml.Linq.dll','System.Xaml.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll')
$arguments = @('/nologo','/target:exe','/platform:x64',"/out:$exe",('/reference:' + (Join-Path $root 'AdaptiveTradingLab.exe')))
foreach ($reference in $references) { $arguments += '/reference:' + (Join-Path $framework $reference) }
$arguments += Join-Path $PSScriptRoot 'DesktopData.Tests.cs'
& (Join-Path $framework 'csc.exe') @arguments
if ($LASTEXITCODE -ne 0) { throw 'Desktop test compilation failed.' }
& $exe (Join-Path $root 'data') $repo $NativeReport
if ($LASTEXITCODE -ne 0) { throw 'Desktop tests failed.' }
