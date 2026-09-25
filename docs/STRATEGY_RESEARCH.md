# Direct strategy research: ATL20_Research_v2

Separate candidate based on the latest tested EMA 9/21, ADX 14/25, ATR 14,
stop 1.5 ATR and target 2.5 ATR configuration. The desktop app is unchanged.
This file is manually installed; the original strategy and its results remain intact.

## Correction and research controls

The original code can call entry and Set methods while already holding a position.
Depending on direction/managed-order handling, this can request a reversal or
refresh an existing signal's bracket even when an entry is ignored. The new
FlatEntriesOnly default returns before either operation while a position is open.
This creates a bracket-driven variant, not a claim that reversal trading is invalid.

Optional, default-off experiments: post-exit cooldown, slow-EMA slope alignment,
and minimum target distance. Long and short entries remain enabled independently;
do not disable shorts just because they lost money in one already-inspected sample.
Minimum target distance skips an entry rather than stretching its target. It does
not estimate expected profit or include transaction costs.

Execution CSV retains the original columns and adds order action/name, originating
entry signal and callback-time strategy position/quantity. Order names distinguish
profit targets, stops, session exits and reversals on new runs. These extra fields
cannot be recovered retrospectively from the older CSV. Callback-time Order/Position
objects are supplementary diagnostics, not guaranteed snapshots across providers;
use native paired trade reports for financial calculations.

## Controlled validation

Install ATL20_Research_v2.cs as a separate NinjaScript and compile with F5.
Use 20-second bars and the same actual MNQ contract and historical data as baseline.

1. Control: FlatEntriesOnly=false; cooldown 0, slope off, minimum target 0,
   both directions enabled. Compare with the prior strategy using identical settings.
2. Change only FlatEntriesOnly=true. Compare trade count, average winner/loser,
   average trade, drawdown and stop/target/reversal counts.
3. Only then test cooldown OR slope alignment individually. Choose a fixed later
   validation interval before inspecting its results; do not select a winner on
   the same interval used to tune it.

Apply the same actual commission template and nonzero slippage to both versions.
For fill sensitivity, repeat with High fill resolution and a supported finer series
when historical data is available. Higher fill resolution is not proof of live fills:
https://ninjatrader.com/support/helpguides/nt8/understanding_historical_fill_.htm

The reference compiler passed against installed NinjaTrader assemblies. No new
native backtest has run, no performance improvement has been measured, and no
installed strategy or live account was changed by this repository update.
