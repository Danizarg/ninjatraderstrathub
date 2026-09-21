# Validation on 2026-09-21

- Original archive SHA-256 matched the value documented in `vendor/README.md`.
- Original self-contained executable ran successfully on Windows x64.
- Demo produced 100 synthetic records, one learning cycle reporting
  `INSUFFICIENT_DATA`, and the original generated NinjaScript file.
- Windows PowerShell 5.1 smoke tests passed: parsing, paths with spaces,
  `-WhatIf`, installation, repeated installation, protection of customized
  files, invalid user directory rejection, and execution of the packaged demo.
- Native strategy compiled against installed NinjaTrader 8.1.6.3 assemblies
  using the Windows .NET Framework compiler.
- Installed `ATL_EMA_Trend.cs` into NinjaTrader's existing user strategy folder.
- NinjaScript Editor discovered and opened `ATL_EMA_Trend`.
- Pressed F5 in NinjaScript Editor. No compiler error rows were reported;
  NinjaTrader regenerated `NinjaTrader.Custom.dll` successfully.

This confirms installation and compilation, not strategy performance. No
historical backtest or forward trading session was executed as part of these
checks. No ATL strategy was enabled and no broker/account settings were changed.
The execution exporter still needs a backtest or simulation run to validate its
records against NinjaTrader's execution history. It does not feed the binary
demo's learning engine.
