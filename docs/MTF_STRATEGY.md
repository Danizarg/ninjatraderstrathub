# ATL20_MTF_v3: 20-second / 5-minute / 15-minute

A separate strategy based on Research v2. Both higher-timeframe filters default on.
Configure the primary Strategy Analyzer series as Second / 20. The strategy adds
5-minute and 15-minute series for the same instrument automatically.

Long entry requires all of:
- The existing 20-second EMA 9/21 upward crossover and ADX >= 25.
- Last eligible 5-minute close above EMA 9, and EMA 9 above EMA 21.
- Last eligible 15-minute EMA 20 above EMA 50, with EMA 50 rising.

Short entry reverses those comparisons. Equality or conflicting directions means
no entry. ATR brackets remain calculated from the 20-second series. Existing exits
remain active even when higher-timeframe alignment disappears; this filter governs
entries, not an additional forced exit. Flat-only entry protection remains on.

Periods and the two filter toggles are exposed in Higher timeframes. Primary
crossover signals are not queued: alignment arriving later requires a new crossover.
Thus this version can trade less frequently and is not a proven profitability fix.

Secondary snapshots are published only from their own OnBarClose callbacks after
warm-up. Entry decisions use snapshots strictly older than the primary timestamp.
At a shared 5/15-minute close the previous snapshot is used; the new one becomes
eligible at the following 20-second decision. This avoids depending on callback
order. Five-minute snapshots expire after 10 minutes; fifteen-minute snapshots after
30 minutes, preventing reuse across long session/data gaps. Insufficient warm-up
or missing higher-timeframe data blocks entries while that filter is enabled.
Allow at least 51 completed 15-minute bars for the default trend warm-up.

Install this file separately and compile with F5. Backtest on 20-second bars using
matching instrument, date range, session template, commissions and slippage. Use
Standard fill resolution for this multi-series strategy: NinjaTrader's High fill
resolution is not supported for multi-timeframe strategies. A future finer-fill
implementation would require an explicit execution series, not toggling High.

Validation: compiled against installed NinjaTrader libraries; ten executable timing
tests cover missing history, equal timestamps, post-close eligibility, duplicate and
out-of-order callbacks, stale snapshots and reset. No new native backtest or live
execution was performed. The installed strategy and desktop app are unchanged.

Reference: https://ninjatrader.com/support/helpGuides/nt8/multi-time_frame__instruments.htm
