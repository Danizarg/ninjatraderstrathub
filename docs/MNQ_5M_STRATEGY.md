# ATL_MNQ_5M_v4

Manual research strategy, exclusively MNQ. Rebuy is absent. Entries explicitly
request one contract and require a flat strategy position and no pending entry.
Planned stop: 160 ticks / 40 points / $80. Target: 200 ticks / 50 points / $100.
These are gross distances from entry; commissions, slippage and gaps can change
realized amounts. Session-close exits can close before either bracket is reached.

Select Tick / 1000 as the PRIMARY Strategy Analyzer series. The script internally
adds 5, 15, 60 and 240 minute bars. Orders and managed brackets use the primary
series. 1000 tick means a bar composed of 1000 ticks, not a price distance.
Use Standard fill resolution for this multi-series script.

Implementation choices for the requested confluence:
- 5-minute setup: EMA9 above/below EMA21, EMA21 slope aligned, price beyond EMA9,
  and ADX14 at least 20.
- 15-minute primary trend: EMA20/50 alignment and EMA50 slope.
- 1-hour and 4-hour: same trend test, both must confirm the 15-minute direction.
- Entry: first eligible completed 1000-tick candle in that direction, closing
  beyond its EMA9. At most one entry attempt per eligible five-minute setup bar.

Only strictly earlier completed higher-timeframe snapshots are eligible. At
identical timestamps the previous snapshot is used. Missing/warming/stale series
block entry; maximum snapshot age is twice its nominal timeframe. Load more than
50 four-hour bars before expecting entries (substantial historical warm-up).
This can trade infrequently, especially with all trend filters aligned.

Realtime entries default off. Existing scripts and installed app are unchanged.
Compiled against the installed NinjaTrader libraries. No native backtest was run;
profitability and runtime order behavior still require Strategy Analyzer and
simulation validation. The rule choices above are unoptimized starting assumptions.
