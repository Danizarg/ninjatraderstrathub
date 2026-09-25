# Long-only comparison: ATL_MNQ_5M_Long_v5

Separate controlled variant of ATL_MNQ_5M_v4. Only entry direction changes:
neutral/bearish five-minute setups are rejected and no short entry is submitted.
All trend tests, primary entry conditions for longs, fixed brackets, order quantity,
rebuy prohibition, warm-up and session-close behavior remain the same.

Install the new .cs file separately, compile with F5, then select
ATL_MNQ_5M_Long_v5. Use MNQ 12-26, Tick / 1000, Standard fill resolution,
and the same dates/session/data as the baseline. Enable the actual commission
template and test identical 1-tick and 2-tick slippage assumptions for both versions.
Realtime entries default off; historical backtests do not require enabling them.

Gross stop/target distances remain $80/$100 for one MNQ contract. Costs and gaps
can alter realized outcomes. Native backtesting is still required; compiling does
not establish performance. Removing short positions can make additional long
entries possible, so v4's long subtotal is not a substitute for a v5 backtest.
After the controlled comparison, validate on an untouched period or forward simulation.

Validation: reviewed the diff against v4 to confirm changes are limited to the
strategy identity/description and long-only entry path; compiled successfully
against installed NinjaTrader assemblies. No installed source or running app changed.
