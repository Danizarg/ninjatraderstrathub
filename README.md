# Adaptive Trading Lab for Windows and NinjaTrader 8

Windows launchers, an editable NinjaTrader strategy, and the original
`AdaptiveTradingLab-windows-x64.zip` supplied for this repository.

## Quick start

1. Download this repository as a ZIP and **extract all files**, or clone it.
2. Double-click **Launch.cmd** to run the original 100-trade synthetic demo.
   No separate .NET installation or SDK is needed. The bundled runtime is
   unpacked to `.runtime` on first launch after its SHA-256 is checked.
3. Double-click **Setup-NinjaTrader.cmd** to install **ATL_EMA_Trend** into
   your NinjaTrader Documents folder.
4. In NinjaTrader, open **New > NinjaScript Editor** and press **F5**.
5. Open **New > Strategy Analyzer**, select **ATL_EMA_Trend**, choose an
   instrument, historical date range and bars, then run a backtest.

Requires x64 Windows supported by the bundled .NET 10 runtime and NinjaTrader 8
for strategy use. The original package was tested on Windows with NinjaTrader
8.1.6.3. NinjaTrader uses .NET Framework; the demo's .NET 10 DLLs are kept outside
NinjaTrader's `bin/Custom` directory.

## What is included

| Component | Behavior |
| --- | --- |
| `Launch.cmd` | Runs the supplied synthetic console demo; saves to `%LOCALAPPDATA%\AdaptiveTradingLab\runs` |
| `Setup-NinjaTrader.cmd` | Copies one editable `.cs` strategy into your NinjaTrader installation |
| `ninjatrader/Strategies/ATL_EMA_Trend.cs` | EMA crossover, ADX filter, ATR stop and target, configurable in NinjaTrader |
| `scripts/Show-Telemetry.ps1` | Shows the latest execution CSV produced by the strategy |
| `vendor/` | Unmodified binary package and its checksum |

The supplied ZIP has **no application source code**, WPF interface, or functional
learning/telemetry connection. Its synthetic run reports `INSUFFICIENT_DATA`.
This repository preserves that demo and adds source for the Windows integration
and native NinjaTrader strategy. Execution CSV files are **not automatically fed
into the binary demo**; there is no claim of an adaptive learning loop.

## Strategy behavior

Defaults match the generated demo signal: EMA 9/21 crossover, ADX 14 at least
20, ATR 14 with stop 1.5x and target 2.5x, evaluated at bar close. Brackets are
set per entry instead of being reset on every bar as in the original generated
sample. Opposite signals can reverse a position. Session-close exits are enabled.
Use NinjaTrader's order quantity and fill settings in Strategy Analyzer.

**Enable realtime entries defaults to false.** Historical backtests still work.
For forward testing, select **Sim101**, enable realtime entries in the strategy
parameters, and then enable the strategy yourself. The installer does not select
an account, connect to a broker, enable a strategy, or place orders. Enabling
realtime entries permits orders on whichever account you select, including a
live account; it is not a simulation-account enforcement switch.

## Execution records

With `Export executions` enabled, each strategy instance writes its own CSV under:

```text
%LOCALAPPDATA%\AdaptiveTradingLab\ninjatrader\executions
```

Records include strategy, instrument, Historical/Realtime mode, execution/order
IDs, NinjaTrader's timestamp (with its original DateTime timezone semantics),
price, fill quantity, and execution market position. A partial fill is a separate
record. These are **execution events**, not paired trades or a P&L report.
No account IDs are exported. A write failure prints an error in NinjaScript
Output and disables logging for that instance without interrupting order handling.
Logging is synchronous; disable it for very large optimization runs if necessary.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Show-Telemetry.ps1
```

## Custom folders and updates

The installer resolves Windows' actual Documents folder, including redirected
Documents locations. For a different NinjaTrader user directory:

```powershell
.\Setup-NinjaTrader.cmd -NinjaTraderHome "D:\Trading\NinjaTrader 8"
.\Launch.cmd -OutputRoot "D:\Trading Lab Data"
```

Identical installs are left alone. A different existing `ATL_EMA_Trend.cs` is
protected; use `-ReplaceExisting` to make a backup under
`%LOCALAPPDATA%\AdaptiveTradingLab\backups` and replace it. Recompile afterward.
Close any editor tab for this file before replacing it. No other scripts or
NinjaTrader configuration files are changed. To uninstall, disable/remove any
instances, then remove ATL_EMA_Trend using NinjaScript Editor and compile.

## Verification and development

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\Smoke.Tests.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-NinjaTraderCompile.ps1
```

The smoke test exercises the real packaged demo and an isolated mock installation,
including paths with spaces and overwrite protection. The compile check uses the
installed NinjaTrader assemblies and the Windows .NET Framework compiler, writing
only to ignored `artifacts/`. Final NinjaScript compilation is done in NinjaTrader.
No proprietary NinjaTrader binaries, account data, or generated telemetry belong
in this repository. The original demo cannot be rebuilt without its missing source.

Official references: [NinjaScript overview](https://docs.ninjatrader.com/ninjascript),
[execution callbacks](https://ninjatrader.com/support/helpGuides/nt8/onexecutionupdate.htm),
[Strategy Analyzer](https://ninjatrader.com/support/helpGuides/nt8/strategy_analyzer.htm).
