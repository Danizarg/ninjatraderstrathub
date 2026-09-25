using System;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Indicators;

namespace NinjaTrader.NinjaScript.Strategies
{
    // One MNQ contract. Fixed gross risk/reward; no averaging or automatic reversals.
    public class ATL_MNQ_5M_v4 : Strategy
    {
        private EMA entryEma;
        private EMA[] fast = new EMA[5], slow = new EMA[5];
        private ADX setupAdx;
        private DateTime[] last = new DateTime[5], previous = new DateTime[5];
        private int[] direction = new int[5], priorDirection = new int[5];
        private DateTime consumedSetup = DateTime.MinValue;
        private bool entryPending;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name = "ATL_MNQ_5M_v4";
                Description = "MNQ: 5-minute setups, 1000-tick entries, 15/60/240-minute trend filters. One contract, no rebuy.";
                Calculate = Calculate.OnBarClose;
                EntriesPerDirection = 1;
                EntryHandling = EntryHandling.AllEntries;
                DefaultQuantity = 1;
                BarsRequiredToTrade = 10;
                StartBehavior = StartBehavior.WaitUntilFlat;
                RealtimeErrorHandling = RealtimeErrorHandling.StopCancelClose;
                IsExitOnSessionCloseStrategy = true;
                ExitOnSessionCloseSeconds = 30;
                EnableRealtimeEntries = false;
            }
            else if (State == State.Configure)
            {
                if (BarsPeriod.BarsPeriodType != NinjaTrader.Data.BarsPeriodType.Tick || BarsPeriod.Value != 1000)
                    throw new ArgumentException("Use Tick / 1000 as the primary series. Five-minute trading logic is added automatically.");
                if (Calculate != Calculate.OnBarClose) throw new ArgumentException("Use OnBarClose calculation.");
                AddDataSeries(NinjaTrader.Data.BarsPeriodType.Minute, 5);
                AddDataSeries(NinjaTrader.Data.BarsPeriodType.Minute, 15);
                AddDataSeries(NinjaTrader.Data.BarsPeriodType.Minute, 60);
                AddDataSeries(NinjaTrader.Data.BarsPeriodType.Minute, 240);
                // MNQ: $0.50 per tick. Each order explicitly requests one contract.
                SetStopLoss("Entry", CalculationMode.Ticks, 160, false);
                SetProfitTarget("Entry", CalculationMode.Ticks, 200);
            }
            else if (State == State.DataLoaded)
            {
                if (Instrument.MasterInstrument.Name != "MNQ" || Math.Abs(TickSize * Instrument.MasterInstrument.PointValue - .5) > .000001)
                    throw new ArgumentException("This strategy is exclusively for MNQ ($0.50 per tick).");
                entryEma = EMA(Closes[0], 9);
                for (int i = 1; i < 5; i++)
                {
                    fast[i] = EMA(Closes[i], i == 1 ? 9 : 20);
                    slow[i] = EMA(Closes[i], i == 1 ? 21 : 50);
                }
                setupAdx = ADX(Closes[1], 14);
                Array.Clear(last, 0, 5); Array.Clear(previous, 0, 5);
                Array.Clear(direction, 0, 5); Array.Clear(priorDirection, 0, 5);
                consumedSetup = DateTime.MinValue; entryPending = false;
            }
        }
        protected override void OnBarUpdate()
        {
            int series = BarsInProgress;
            if (series > 0)
            {
                if (CurrentBars[series] < (series == 1 ? 21 : 50)) return;
                int value = fast[series][0] > slow[series][0] && slow[series][0] > slow[series][1] ? 1 :
                    fast[series][0] < slow[series][0] && slow[series][0] < slow[series][1] ? -1 : 0;
                if (series == 1 && (setupAdx[0] < 20 || (value == 1 && Closes[1][0] <= fast[1][0]) || (value == -1 && Closes[1][0] >= fast[1][0]))) value = 0;
                if (Times[series][0] > last[series]) { previous[series] = last[series]; priorDirection[series] = direction[series]; }
                last[series] = Times[series][0]; direction[series] = value;
                return;
            }
            if (CurrentBars[0] < 10 || entryPending || Position.MarketPosition != MarketPosition.Flat) return;
            if (State == State.Realtime && !EnableRealtimeEntries) return;
            DateTime setupTime;
            int setup = Eligible(1, 10, out setupTime);
            if (setup == 0 || setupTime <= consumedSetup) return;
            DateTime ignored;
            // 15-minute trend is mandatory; 1h and 4h must confirm the same direction.
            if (Eligible(2, 30, out ignored) != setup || Eligible(3, 120, out ignored) != setup || Eligible(4, 480, out ignored) != setup) return;
            bool trigger = setup == 1 ? Close[0] > Open[0] && Close[0] > entryEma[0] : Close[0] < Open[0] && Close[0] < entryEma[0];
            if (!trigger) return;
            consumedSetup = setupTime; entryPending = true;
            if (setup == 1) EnterLong(0, 1, "Entry"); else EnterShort(0, 1, "Entry");
        }
        private int Eligible(int series, int maxAgeMinutes, out DateTime time)
        {
            bool useLatest = last[series] < Time[0];
            time = useLatest ? last[series] : previous[series];
            if (time == DateTime.MinValue || time >= Time[0] || Time[0] - time > TimeSpan.FromMinutes(maxAgeMinutes)) return 0;
            return useLatest ? direction[series] : priorDirection[series];
        }
        protected override void OnOrderUpdate(Order order, double limitPrice, double stopPrice, int quantity,
            int filled, double averageFillPrice, OrderState orderState, DateTime time, ErrorCode error, string comment)
        {
            if (order.Name == "Entry" && (orderState == OrderState.Cancelled || orderState == OrderState.Rejected)) entryPending = false;
        }
        protected override void OnExecutionUpdate(Execution execution, string executionId, double price, int quantity,
            MarketPosition marketPosition, string orderId, DateTime time)
        {
            if (execution.Order != null && execution.Order.Name == "Entry") entryPending = false;
        }
        [NinjaScriptProperty]
        [System.ComponentModel.DataAnnotations.Display(Name = "Enable realtime entries", GroupName = "Execution", Order = 1)]
        public bool EnableRealtimeEntries { get; set; }
    }
}
