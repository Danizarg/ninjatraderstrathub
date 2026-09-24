# Adaptive Trading Lab — Windows / NinjaTrader 8

A local WPF desktop app for reading actual NinjaTrader backtests, proposing
20-second EMA strategy variants, comparing recorded results, and managing strategy
source files. No demo runner, generated performance data, bundled learning binary,
API key, subscription, or external AI service is used.

## Development branch

This version is staged on `feature/20-second-adaptation`. It does not update an
already-running app or install changes into NinjaTrader. Main remains separate.

## Open your backtest

1. Extract this branch's complete repository; open `AdaptiveTradingLab.exe` or
   `Launch.cmd`. Windows x64 and .NET Framework 4.8 are required.
2. Confirm the NinjaTrader **user** folder in NinjaTrader / Settings: usually
   `Documents\NinjaTrader 8`, not Program Files.
3. Open **Backtests** and click **Refresh**. The app reads saved XML reports in
   `strategyanalyzerlogs` and selects the latest 20-second result.
4. If necessary, use **Import report XML**. Keep its adjacent saved NinjaScript
   `.cs` snapshot with the XML to preserve source identity for adaptation.

P&L, completed trades, win rate, drawdown, dates, instrument, parameters, commission
and slippage come from the saved Strategy Analyzer summary. No fills are converted
into invented completed trades or equity curves. Empty workspaces show no results.
Execution CSVs remain available separately under **Executions**.

The reader targets NinjaTrader 8.1.6.3's saved individual Backtest XML format.
Unsupported or damaged reports are reported in the activity log. Optimization
summaries and other vendors' XML/CSV reports are not supported.

## Suggest and control adaptations

- Select a 20-second `ATL_EMA_Trend` backtest with at least 50 completed trades.
- **Suggest 20-second changes** explains each exact parameter change, the observed
  baseline evidence, its intended effect, and the possible downside. Suggestions
  use a free local rule engine; they are not AI predictions or measured gains.
- **Auto-create candidate versions** stages up to three alternatives: stronger ADX
  filtering, slower EMA crossover, and a closer ATR target. Each starts **Untested**.
- **Adapt & versions** lets you set all six parameters, inspect generated source,
  select a version, and explicitly install it under a unique `ATL20_...` name.
- Candidates require 20-second bars and default realtime entries to off. The
  original strategy is not replaced by installing a candidate.

The baseline source snapshot must match the bundled strategy or a known library
version. NinjaTrader's dated snapshot class aliases are normalized for comparison;
other code changes are retained. Arbitrary edited strategies can be managed in
Strategy files but cannot be automatically adapted by this rule engine.

Backtests are still run **inside NinjaTrader**: install a selected candidate,
press F5 in NinjaScript Editor, run baseline and candidate with identical dates,
instrument, 20-second bars, quantity, session and fill settings, then Refresh here.
Use commission and nonzero slippage. **Compare** displays exact observed metric
differences; a suggested parameter change has no measured benefit before testing.

**Find tested improvement** recommends only a known candidate with:

- Matching execution assumptions, instrument, timeframe and date range.
- Commission enabled and actually charged, plus positive slippage.
- At least 50 trades per test, positive and higher candidate net P&L, better average
  trade and no increase in maximum drawdown.
- The same improvement on a later, non-overlapping period, using unchanged source
  and parameters for both baseline and candidate.

The version keeps hashes of all four evidence reports. This is a conservative
recorded-test policy, not statistical proof or a guarantee of future returns.
Saved reports cannot certify identical historical feed/cache data or every global
cost configuration. Reusing a validation period for selection can still overfit.
No candidate is automatically installed, enabled, or connected to an account.

## Strategy folder control

**Strategy files** lists `.cs` files, supports source review/editing, import,
archive and restore. Edits and archives require NinjaTrader to be closed. Every
mutation records a backup outside the compiled source tree; changed files are
rejected until refreshed. Import and candidate installation refuse collisions.
Linked/junction write paths and paths escaping the Strategies directory are
rejected. Recompile with F5 after source changes. Archiving source alone does not
remove an already compiled strategy or a configured chart instance.

Settings and logs use `%LOCALAPPDATA%\AdaptiveTradingLab`. Version manifests,
generated sources, imported reports and restorable backups use its `adaptation`
subfolder. Old demo files are no longer read; this app does not delete personal
historical files left by a previous installation.

For an isolated development preview, set `ATL_DESKTOP_DATA` to a separate directory
and `ATL_PREVIEW=1`; the latter blocks all GUI writes into NinjaTrader. It still
permits candidate creation in that isolated library.

## Build and verify

```powershell
.\app\Build.ps1
.\tests\Desktop.Tests.ps1
.\tests\Smoke.Tests.ps1
.\scripts\Test-NinjaTraderCompile.ps1
```

Optional read-only integration check:

```powershell
.\tests\Desktop.Tests.ps1 -NativeReport 'C:\path\to\saved-backtest.xml'
.\scripts\Test-NinjaTraderCompile.ps1 -SourcePath .\artifacts\generated-check.cs
```

Build uses the Windows .NET Framework compiler with embedded XAML and source
resource; no NuGet, SDK or Visual Studio is required. Tests use isolated directories
under ignored `artifacts/`. Test fixtures are test-only and never loaded by the app.
The native compile check reads installed NinjaTrader assemblies and writes only
under `artifacts/`; full NinjaScript compilation and strategy backtesting remain
inside NinjaTrader. No proprietary NinjaTrader assemblies or personal reports are
included in this repository.

## Low-trade recovery

**Loosen for more trades** works even with zero completed trades. It offers a
lower ADX threshold, an ADX-disabled diagnostic candidate, and faster EMA periods.
Each change is explained, preserves stop/target multipliers, and remains untested.
Select a proposal and **Create selected version**, or enter custom parameters.
The version panel provides the install / F5 / Strategy Analyzer rerun steps.
Actual backtests still run in NinjaTrader; Refresh imports their saved reports.

Zero trades can also indicate missing historical data, an incorrect contract,
session restrictions or runtime errors. The recovery explanation flags those
possibilities. More trades are not guaranteed, and the existing 50-trade,
cost and later-validation requirements for performance recommendations remain.
