# Validation — 2026-09-24

Changes are isolated on `feature/20-second-adaptation`. The running desktop app,
installed NinjaTrader strategy, main branch and original personal files were not
updated during development.

- Built the Windows x64 WPF executable with the .NET Framework compiler.
- Passed 38 checks with an optional real saved report: native XML metrics and
  source-alias identity; DTD rejection; cost, sample, drawdown, period, parameter
  and source-provenance gates; version persistence and evidence; generated
  20-second/realtime defaults; import collision and traversal rejection;
  backup/edit/archive/restore; external-change protection; CSV quoting and
  partial writes; empty-workspace behavior; actual UI discovery, KPI binding,
  candidate creation and preview write protection.
- Loaded the user's actual saved 20-second Strategy Analyzer report read-only.
  KPI values were checked against the report's own currency summary. Saved
  NinjaTrader class aliases matched the underlying bundled strategy source.
- Rendered and inspected the Backtests and Adaptation WPF layouts using an
  isolated test instance. Demo controls are absent.
- Installer smoke checks passed using an isolated mock NinjaTrader folder:
  script parsing, WhatIf, installation, repeat installation, overwrite protection
  and invalid-folder rejection.
- A generated candidate compiled against installed NinjaTrader 8.1.6.3 libraries.
  The compile output was written only under ignored `artifacts/`.

No generated candidate was installed into the user's NinjaTrader folder, compiled
with F5 there, backtested or enabled. No new strategy performance is claimed.
Recommendations are gated by recorded test evidence; native Strategy Analyzer
runs still require user action in NinjaTrader. The new application must be
explicitly launched/deployed to replace the old app's displayed data.

## Low-trade recovery update

Passed 43 checks including zero-trade candidate creation, lower/disabled ADX,
faster EMA windows, unchanged stop/target multipliers, minimum-bound no-op
suppression, missing-data messaging and preservation of performance sample gates.
A generated strategy also passed the installed NinjaTrader reference compilation.
No native backtest was automatically started or deployed app replaced.
