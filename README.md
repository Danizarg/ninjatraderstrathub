# Adaptive Trading Lab for Windows and NinjaTrader 8

A native Windows desktop app with a results dashboard, equity chart, trade
tables, NinjaTrader setup, and an execution viewer. Includes an editable
NinjaTrader strategy and the original supplied binary demo.

## Quick start

1. Download this repository as a ZIP and **extract all files**, or clone it.
2. Double-click **AdaptiveTradingLab.exe** (or **Launch.cmd**) to open the visual
   desktop app. Keep it with the `scripts` and `vendor` folders.
3. Click **Run demo** to generate a synthetic run and show its results. Open the
   **NinjaTrader** tab and click **Install strategy** to install **ATL_EMA_Trend**.
4. In NinjaTrader, open **New > NinjaScript Editor** and press **F5**.
5. Open **New > Strategy Analyzer**, select **ATL_EMA_Trend**, choose an
   instrument, historical date range and bars, then run a backtest.

The desktop app requires Windows x64 with .NET Framework 4.8 (also used by
NinjaTrader 8). No SDK is needed to run the included EXE. Running the demo also
requires Windows supported by its bundled .NET 10 runtime. That runtime is
unpacked to `.runtime` on first use after its SHA-256 is checked.
The original package was tested on Windows with NinjaTrader
8.1.6.3. NinjaTrader uses .NET Framework; the demo's .NET 10 DLLs are kept outside
NinjaTrader's `bin/Custom` directory.

## What is included

| Component | Behavior |
| --- | --- |
| `AdaptiveTradingLab.exe` / `Launch.cmd` | Opens the graphical Windows app |
| `app/` | Complete editable C# / WPF desktop source and build script |
| `Run-Demo.cmd` | Optional terminal-only launcher for the original synthetic demo |
| `Setup-NinjaTrader.cmd` | Copies one editable `.cs` strategy into your NinjaTrader installation |
| `ninjatrader/Strategies/ATL_EMA_Trend.cs` | EMA crossover, ADX filter, ATR stop and target, configurable in NinjaTrader |
| `scripts/Show-Telemetry.ps1` | Shows the latest execution CSV produced by the strategy |
| `vendor/` | Unmodified binary package and its checksum |

The original supplied ZIP has **no engine source code** or functional
learning/telemetry connection. Its synthetic run reports `INSUFFICIENT_DATA`.
This repository adds a new WPF desktop interface with its full source, Windows
integration, and a native NinjaTrader strategy. Execution CSV files are **not automatically fed
into the binary demo**; there is no claim of an adaptive learning loop.

## Visual app

- **Overview:** select an existing run; view synthetic net P&L, trade count,
  win rate, maximum drawdown, and a cumulative net P&L chart. Metrics are computed
  from JSON records, ordered by exit time. Unreadable records are reported as
  skipped, with partial totals clearly marked.
- **Demo trades:** inspect and sort the selected run's records.
- **NinjaTrader:** detect the user folder and running process, install the strategy,
  and follow the compile/backtest steps. Detection is local; it does not imply an
  account connection or successful NinjaScript compilation.
- **Executions:** select an exported CSV and inspect complete fills. Click
  **Refresh** after NinjaTrader writes more data. There is no background file watcher.
- **Settings & activity:** choose output and export folders, save settings, and
  read operation output. Settings are saved to
  `%LOCALAPPDATA%\AdaptiveTradingLab\desktop-settings.json`; the activity log is
  `desktop.log` beside it. Existing runs are preserved.

The app remains responsive while the demo runs, captures terminal output in the
activity tab, and shows errors in the window. New runs include a small
`desktop-run.json` status file. Older runs still load but may lack learning status.
The NinjaTrader folder picker recognizes a selected subfolder (such as a workspace
template) and resolves it to the parent NinjaTrader user folder.

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
.\Run-Demo.cmd -OutputRoot "D:\Trading Lab Data"
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
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\Desktop.Tests.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-NinjaTraderCompile.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\app\Build.ps1
```

The smoke test exercises the real packaged demo and an isolated mock installation,
including paths with spaces and overwrite protection. The compile check uses the
installed NinjaTrader assemblies and the Windows .NET Framework compiler, writing
only to ignored `artifacts/`. Final NinjaScript compilation is done in NinjaTrader.
Close the desktop app before rebuilding its EXE. `Build.ps1` uses Windows'
included .NET Framework C# compiler and embeds the XAML; no NuGet restore or
Visual Studio installation is needed. The desktop executable is included in Git
so a download is ready to launch.

No proprietary NinjaTrader binaries, account data, or generated telemetry belong
in this repository. The original demo cannot be rebuilt without its missing source.

Official references: [NinjaScript overview](https://docs.ninjatrader.com/ninjascript),
[execution callbacks](https://ninjatrader.com/support/helpGuides/nt8/onexecutionupdate.htm),
[Strategy Analyzer](https://ninjatrader.com/support/helpGuides/nt8/strategy_analyzer.htm).
