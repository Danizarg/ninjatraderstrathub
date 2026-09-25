param()
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$folder = Join-Path $repo 'artifacts\mtf-tests'
New-Item -ItemType Directory -Force -Path $folder | Out-Null
$source = Get-Content (Join-Path $repo 'ninjatrader\Strategies\ATL20_MTF_v3.cs') -Raw
$helper = 'using System;' + ($source -split '// BEGIN CLOSED DIRECTION HELPER')[1]
$test = @"
class Test {
 static void Equal(int a,int b) { if(a!=b) throw new System.Exception(a+" != "+b); }
 static void Main() {
 var x=new NinjaTrader.NinjaScript.Strategies.ATLClosedDirection();
 var t=new System.DateTime(2026,1,1,10,0,0);var age=System.TimeSpan.FromMinutes(10);
 Equal(x.Before(t,age),0);
 x.Push(t,1);Equal(x.Before(t,age),0);Equal(x.Before(t.AddSeconds(20),age),1);
 x.Push(t.AddMinutes(5),-1);Equal(x.Before(t.AddMinutes(5),age),1);
 Equal(x.Before(t.AddMinutes(5).AddSeconds(20),age),-1);
 x.Push(t.AddMinutes(5),0);Equal(x.Before(t.AddMinutes(5),age),1);
 Equal(x.Before(t.AddMinutes(6),age),0);
 x.Push(t.AddMinutes(10),1);Equal(x.Before(t.AddMinutes(21),age),0);
 x.Push(t.AddMinutes(9),-1);Equal(x.Before(t.AddMinutes(11),age),1);
 x.Reset();Equal(x.Before(t.AddMinutes(11),age),0);
 System.Console.WriteLine("PASS: 10 higher-timeframe timing checks.");
 }
}
"@
$file = Join-Path $folder 'Timing.cs'
Set-Content -LiteralPath $file -Value ($helper + $test) -Encoding UTF8
$exe = Join-Path $folder 'Timing.exe'
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:exe "/out:$exe" $file
if ($LASTEXITCODE -ne 0) { throw 'Timing test compilation failed' }
& $exe
if ($LASTEXITCODE -ne 0) { throw 'Timing tests failed' }
