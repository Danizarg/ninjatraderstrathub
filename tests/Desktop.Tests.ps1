[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$root = Join-Path $repo ('artifacts\desktop tests ' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root -Force | Out-Null
$exe = Join-Path $root 'DesktopData.Tests.exe'
& (Join-Path $framework 'csc.exe') /nologo /target:exe "/out:$exe" "/reference:$framework\System.Web.Extensions.dll" (Join-Path $repo 'app\LabData.cs') (Join-Path $PSScriptRoot 'DesktopData.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Desktop test compilation failed.' }
& $exe (Join-Path $root 'data')
if ($LASTEXITCODE -ne 0) { throw 'Desktop data tests failed.' }
