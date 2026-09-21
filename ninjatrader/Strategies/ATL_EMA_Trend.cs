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
    public class ATL_EMA_Trend : Strategy
    {
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
                Name = "ATL_EMA_Trend";
                Description = "Adaptive Trading Lab EMA crossover with ADX filter and ATR brackets.";
                Calculate = Calculate.OnBarClose;
                EntriesPerDirection = 1;
                EntryHandling = EntryHandling.AllEntries;
                IsExitOnSessionCloseStrategy = true;
                ExitOnSessionCloseSeconds = 30;
                StartBehavior = StartBehavior.WaitUntilFlat;
                RealtimeErrorHandling = RealtimeErrorHandling.StopCancelClose;
                BarsRequiredToTrade = 23;
                FastPeriod = 9;
                SlowPeriod = 21;
                FilterPeriod = 14;
                MinimumAdx = 20;
                StopAtrMultiplier = 1.5;
                TargetAtrMultiplier = 2.5;
                EnableRealtimeEntries = false;
                ExportExecutions = true;
                TelemetryDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AdaptiveTradingLab", "ninjatrader", "executions");
            }
            else if (State == State.Configure)
            {
                if (FastPeriod >= SlowPeriod)
                    throw new ArgumentException("Fast period must be smaller than slow period.");
            }
            else if (State == State.DataLoaded)
            {
                fastEma = EMA(FastPeriod);
                slowEma = EMA(SlowPeriod);
                adx = ADX(FilterPeriod);
                atr = ATR(FilterPeriod);
                executionFile = null;
                telemetryFailed = false;
            }
        }

        protected override void OnBarUpdate()
        {
            if (BarsInProgress != 0 || CurrentBar < Math.Max(SlowPeriod, FilterPeriod) + 2) return;
            if (State == State.Realtime && !EnableRealtimeEntries) return;
            if (adx[0] < MinimumAdx) return;
            bool goLong = CrossAbove(fastEma, slowEma, 1);
            bool goShort = CrossBelow(fastEma, slowEma, 1);
            if (!goLong && !goShort) return;

            // Set the bracket for the new entry; do not move an existing stop on every bar.
            int stopTicks = Math.Max(1, (int)Math.Round(atr[0] * StopAtrMultiplier / TickSize));
            int targetTicks = Math.Max(1, (int)Math.Round(atr[0] * TargetAtrMultiplier / TickSize));
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
                            "schema_version,strategy,instrument,mode,execution_id,order_id,time,price,quantity,market_position\r\n", new UTF8Encoding(false));
                        Print("ATL execution telemetry: " + executionFile);
                    }
                    string[] fields = {
                        "1", Name, Instrument.FullName, State == State.Realtime ? "Realtime" : "Historical",
                        executionId, orderId, time.ToString("o", CultureInfo.InvariantCulture),
                        price.ToString("R", CultureInfo.InvariantCulture), quantity.ToString(CultureInfo.InvariantCulture),
                        marketPosition.ToString()
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
