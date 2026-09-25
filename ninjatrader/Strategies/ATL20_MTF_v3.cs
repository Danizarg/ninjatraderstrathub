// Research candidate: position controls and richer fill telemetry. Performance unvalidated.
using System;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Text;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Indicators;

namespace NinjaTrader.NinjaScript.Strategies
{
    // Editable NT8 adaptation of the EMA/ADX/ATR strategy emitted by the supplied demo.
    // No dependency on the external .NET 10 runtime is loaded into NinjaTrader.
    public class ATL20_MTF_v3 : Strategy
    {
        private EMA confirmFast, confirmSlow, trendFast, trendSlow;
        private readonly ATLClosedDirection confirmState = new ATLClosedDirection();
        private readonly ATLClosedDirection trendState = new ATLClosedDirection();
        private EMA fastEma;
        private EMA slowEma;
        private ADX adx;
        private ATR atr;
        private string executionFile;
        private bool telemetryFailed;
        private readonly object telemetryLock = new object();

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name = "ATL20_MTF_v3";
                Description = "Adaptive Trading Lab EMA crossover with ADX filter and ATR brackets.";
                Calculate = Calculate.OnBarClose;
                EntriesPerDirection = 1;
                EntryHandling = EntryHandling.AllEntries;
                IsExitOnSessionCloseStrategy = true;
                ExitOnSessionCloseSeconds = 30;
                StartBehavior = StartBehavior.WaitUntilFlat;
                RealtimeErrorHandling = RealtimeErrorHandling.StopCancelClose;
                BarsRequiredToTrade = 23;
                UseFiveMinuteConfirmation = true;
                UseFifteenMinuteTrend = true;
                ConfirmationFast = 9;
                ConfirmationSlow = 21;
                TrendFast = 20;
                TrendSlow = 50;
                FastPeriod = 9;
                SlowPeriod = 21;
                FilterPeriod = 14;
                MinimumAdx = 25;
                FlatEntriesOnly = true;
                CooldownBars = 0;
                EnableLongEntries = true;
                EnableShortEntries = true;
                RequireSlowEmaSlope = false;
                MinimumTargetTicks = 0;
                StopAtrMultiplier = 1.5;
                TargetAtrMultiplier = 2.5;
                EnableRealtimeEntries = false;
                ExportExecutions = true;
                TelemetryDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AdaptiveTradingLab", "ninjatrader", "executions");
            }
            else if (State == State.Configure)
            {
                if (BarsPeriod.BarsPeriodType != NinjaTrader.Data.BarsPeriodType.Second || BarsPeriod.Value != 20)
                    throw new ArgumentException("This candidate requires 20-second bars.");
                AddDataSeries(NinjaTrader.Data.BarsPeriodType.Minute, 5);
                AddDataSeries(NinjaTrader.Data.BarsPeriodType.Minute, 15);
                if (ConfirmationFast >= ConfirmationSlow || TrendFast >= TrendSlow)
                    throw new ArgumentException("Higher-timeframe fast EMA periods must be smaller than slow periods.");
                if (Calculate != Calculate.OnBarClose)
                    throw new ArgumentException("This strategy requires Calculate.OnBarClose.");
                if (FastPeriod >= SlowPeriod)
                    throw new ArgumentException("Fast period must be smaller than slow period.");
            }
            else if (State == State.DataLoaded)
            {
                confirmState.Reset(); trendState.Reset();
                confirmFast = EMA(Closes[1], ConfirmationFast);
                confirmSlow = EMA(Closes[1], ConfirmationSlow);
                trendFast = EMA(Closes[2], TrendFast);
                trendSlow = EMA(Closes[2], TrendSlow);
                fastEma = EMA(Closes[0], FastPeriod);
                slowEma = EMA(SlowPeriod);
                adx = ADX(FilterPeriod);
                atr = ATR(FilterPeriod);
                executionFile = null;
                telemetryFailed = false;
            }
        }

        protected override void OnBarUpdate()
        {
            // Publish only from each secondary series' own bar-close callback.
            if (BarsInProgress == 1)
            {
                if (CurrentBars[1] >= ConfirmationSlow)
                    confirmState.Push(Times[1][0], Closes[1][0] > confirmFast[0] && confirmFast[0] > confirmSlow[0] ? 1 :
                        Closes[1][0] < confirmFast[0] && confirmFast[0] < confirmSlow[0] ? -1 : 0);
                return;
            }
            if (BarsInProgress == 2)
            {
                if (CurrentBars[2] >= TrendSlow)
                    trendState.Push(Times[2][0], trendFast[0] > trendSlow[0] && trendSlow[0] > trendSlow[1] ? 1 :
                        trendFast[0] < trendSlow[0] && trendSlow[0] < trendSlow[1] ? -1 : 0);
                return;
            }
            if (BarsInProgress != 0 || CurrentBar < Math.Max(SlowPeriod, FilterPeriod) + 2) return;
            if (State == State.Realtime && !EnableRealtimeEntries) return;
            // Entry restrictions do not cancel the existing protective bracket.
            if (FlatEntriesOnly && Position.MarketPosition != MarketPosition.Flat) return;
            int barsSinceExit = BarsSinceExitExecution();
            if (CooldownBars > 0 && barsSinceExit >= 0 && barsSinceExit < CooldownBars) return;
            if (adx[0] < MinimumAdx) return;
            bool goLong = EnableLongEntries && CrossAbove(fastEma, slowEma, 1) && (!RequireSlowEmaSlope || slowEma[0] > slowEma[1]);
            bool goShort = EnableShortEntries && CrossBelow(fastEma, slowEma, 1) && (!RequireSlowEmaSlope || slowEma[0] < slowEma[1]);
            // Strictly older timestamps make equal-close ordering consistent in historical/realtime.
            int confirmation = UseFiveMinuteConfirmation ? confirmState.Before(Time[0], TimeSpan.FromMinutes(10)) : 0;
            int trend = UseFifteenMinuteTrend ? trendState.Before(Time[0], TimeSpan.FromMinutes(30)) : 0;
            goLong = goLong && (!UseFiveMinuteConfirmation || confirmation == 1) && (!UseFifteenMinuteTrend || trend == 1);
            goShort = goShort && (!UseFiveMinuteConfirmation || confirmation == -1) && (!UseFifteenMinuteTrend || trend == -1);
            if (!goLong && !goShort) return;

            // Set the bracket for the new entry; do not move an existing stop on every bar.
            int stopTicks = Math.Max(1, (int)Math.Round(atr[0] * StopAtrMultiplier / TickSize));
            int targetTicks = Math.Max(1, (int)Math.Round(atr[0] * TargetAtrMultiplier / TickSize));
            if (targetTicks < MinimumTargetTicks) return;
            string signal = goLong ? "ATL_Long" : "ATL_Short";
            SetStopLoss(signal, CalculationMode.Ticks, stopTicks, false);
            SetProfitTarget(signal, CalculationMode.Ticks, targetTicks);
            if (goLong) EnterLong(signal);
            else EnterShort(signal);
        }

        protected override void OnExecutionUpdate(Execution execution, string executionId, double price,
            int quantity, MarketPosition marketPosition, string orderId, DateTime time)
        {
            if (!ExportExecutions || telemetryFailed) return;
            // Use the passed-by-value execution data, including each partial fill.
            // Unique file per instance avoids mixing Analyzer passes or concurrent charts.
            lock (telemetryLock)
            {
                try
                {
                    if (executionFile == null)
                    {
                        Directory.CreateDirectory(TelemetryDirectory);
                        executionFile = Path.Combine(TelemetryDirectory, "ATL_" + Guid.NewGuid().ToString("N") + ".csv");
                        File.WriteAllText(executionFile,
                            "schema_version,strategy,instrument,mode,execution_id,order_id,time,price,quantity,market_position,order_action,order_name,from_entry_signal,strategy_position,position_quantity\r\n", new UTF8Encoding(false));
                        Print("ATL execution telemetry: " + executionFile);
                    }
                    string[] fields = {
                        "1", Name, Instrument.FullName, State == State.Realtime ? "Realtime" : "Historical",
                        executionId, orderId, time.ToString("o", CultureInfo.InvariantCulture),
                        price.ToString("R", CultureInfo.InvariantCulture), quantity.ToString(CultureInfo.InvariantCulture),
                        marketPosition.ToString(),
                        execution.Order == null ? "" : execution.Order.OrderAction.ToString(),
                        execution.Order == null ? "" : execution.Order.Name,
                        execution.Order == null ? "" : execution.Order.FromEntrySignal,
                        Position.MarketPosition.ToString(), Position.Quantity.ToString(CultureInfo.InvariantCulture)
                    };
                    for (int i = 0; i < fields.Length; i++) fields[i] = Csv(fields[i]);
                    File.AppendAllText(executionFile, string.Join(",", fields) + "\r\n", new UTF8Encoding(false));
                }
                catch (Exception error)
                {
                    // A disk error must not throw out of an order/execution callback.
                    telemetryFailed = true;
                    Print("ATL telemetry disabled for this instance: " + error.Message);
                }
            }
        }

        private static string Csv(string value)
        {
            return "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
        }

        [NinjaScriptProperty]
        [Display(Name = "5-minute confluence", GroupName = "Higher timeframes", Order = 1)]
        public bool UseFiveMinuteConfirmation { get; set; }
        [NinjaScriptProperty]
        [Display(Name = "15-minute trend", GroupName = "Higher timeframes", Order = 2)]
        public bool UseFifteenMinuteTrend { get; set; }
        [NinjaScriptProperty, Range(1, 10000)]
        [Display(Name = "5-minute fast EMA", GroupName = "Higher timeframes", Order = 3)]
        public int ConfirmationFast { get; set; }
        [NinjaScriptProperty, Range(2, 10000)]
        [Display(Name = "5-minute slow EMA", GroupName = "Higher timeframes", Order = 4)]
        public int ConfirmationSlow { get; set; }
        [NinjaScriptProperty, Range(1, 10000)]
        [Display(Name = "15-minute fast EMA", GroupName = "Higher timeframes", Order = 5)]
        public int TrendFast { get; set; }
        [NinjaScriptProperty, Range(2, 10000)]
        [Display(Name = "15-minute slow EMA", GroupName = "Higher timeframes", Order = 6)]
        public int TrendSlow { get; set; }
        [NinjaScriptProperty]
        [Display(Name = "Enter only when flat", GroupName = "Research controls", Order = 1)]
        public bool FlatEntriesOnly { get; set; }
        [NinjaScriptProperty, Range(0, 10000)]
        [Display(Name = "Bars to wait after exit", GroupName = "Research controls", Order = 2)]
        public int CooldownBars { get; set; }
        [NinjaScriptProperty]
        [Display(Name = "Allow long entries", GroupName = "Research controls", Order = 3)]
        public bool EnableLongEntries { get; set; }
        [NinjaScriptProperty]
        [Display(Name = "Allow short entries", GroupName = "Research controls", Order = 4)]
        public bool EnableShortEntries { get; set; }
        [NinjaScriptProperty]
        [Display(Name = "Require slow EMA slope alignment", GroupName = "Research controls", Order = 5)]
        public bool RequireSlowEmaSlope { get; set; }
        [NinjaScriptProperty, Range(0, 100000)]
        [Display(Name = "Minimum target distance (ticks)", GroupName = "Research controls", Order = 6)]
        public int MinimumTargetTicks { get; set; }
        [NinjaScriptProperty, Range(1, 10000)]
        [Display(Name = "Fast EMA period", GroupName = "Parameters", Order = 1)]
        public int FastPeriod { get; set; }
        [NinjaScriptProperty, Range(2, 10000)]
        [Display(Name = "Slow EMA period", GroupName = "Parameters", Order = 2)]
        public int SlowPeriod { get; set; }
        [NinjaScriptProperty, Range(1, 10000)]
        [Display(Name = "ADX / ATR period", GroupName = "Parameters", Order = 3)]
        public int FilterPeriod { get; set; }
        [NinjaScriptProperty, Range(0.0, 100.0)]
        [Display(Name = "Minimum ADX", GroupName = "Parameters", Order = 4)]
        public double MinimumAdx { get; set; }
        [NinjaScriptProperty, Range(0.01, 100.0)]
        [Display(Name = "Stop ATR multiplier", GroupName = "Parameters", Order = 5)]
        public double StopAtrMultiplier { get; set; }
        [NinjaScriptProperty, Range(0.01, 100.0)]
        [Display(Name = "Target ATR multiplier", GroupName = "Parameters", Order = 6)]
        public double TargetAtrMultiplier { get; set; }
        [NinjaScriptProperty]
        [Display(Name = "Enable realtime entries", GroupName = "Execution", Order = 1)]
        public bool EnableRealtimeEntries { get; set; }
        [NinjaScriptProperty]
        [Display(Name = "Export executions", GroupName = "Telemetry", Order = 1)]
        public bool ExportExecutions { get; set; }
        [NinjaScriptProperty]
        [Display(Name = "Telemetry directory", GroupName = "Telemetry", Order = 2)]
        public string TelemetryDirectory { get; set; }
    }
}

// BEGIN CLOSED DIRECTION HELPER
namespace NinjaTrader.NinjaScript.Strategies
{
    // Two snapshots allow strict-before selection even if a secondary callback runs first.
    public sealed class ATLClosedDirection
    {
        private DateTime latest = DateTime.MinValue, previous = DateTime.MinValue;
        private int direction, previousDirection;
        public void Reset() { latest = previous = DateTime.MinValue; direction = previousDirection = 0; }
        public void Push(DateTime time, int value)
        {
            if (time < latest) return;
            if (time > latest) { previous = latest; previousDirection = direction; }
            latest = time; direction = value;
        }
        public int Before(DateTime entryTime, TimeSpan maxAge)
        {
            DateTime selected = latest < entryTime ? latest : previous;
            int value = latest < entryTime ? direction : previousDirection;
            return selected == DateTime.MinValue || selected >= entryTime || entryTime - selected > maxAge ? 0 : value;
        }
    }
}
