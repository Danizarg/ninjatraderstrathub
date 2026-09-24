using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace AdaptiveTradingLab.Desktop
{
    public sealed class StrategyParameters
    {
        public int Fast { get; set; }
        public int Slow { get; set; }
        public int Filter { get; set; }
        public double Adx { get; set; }
        public double Stop { get; set; }
        public double Target { get; set; }
        public StrategyParameters Copy() { return (StrategyParameters)MemberwiseClone(); }
        public void Validate()
        {
            if (Fast < 1 || Slow <= Fast || Slow > 10000 || Filter < 1 || Filter > 10000 ||
                !Finite(Adx) || Adx < 0 || Adx > 100 || !Finite(Stop) || Stop < .01 || Stop > 100 || !Finite(Target) || Target < .01 || Target > 100)
                throw new InvalidDataException("Invalid strategy parameters: require fast < slow, positive periods and ATR multipliers, and ADX 0–100.");
        }
        public static bool Finite(double n) { return !double.IsNaN(n) && !double.IsInfinity(n); }
        public string Key { get { return string.Join("/", new[] { Fast.ToString(), Slow.ToString(), Filter.ToString(), Adx.ToString("R", CultureInfo.InvariantCulture), Stop.ToString("R", CultureInfo.InvariantCulture), Target.ToString("R", CultureInfo.InvariantCulture) }); } }
        public string Summary { get { return "EMA " + Fast + "/" + Slow + ", ADX/ATR " + Filter + ", ADX ≥ " + Adx + ", stop " + Stop + " ATR, target " + Target + " ATR"; } }
    }
    public sealed class NativeBacktest
    {
        public string FilePath { get; set; }
        public string ReportHash { get; set; }
        public string Strategy { get; set; }
        public string Instrument { get; set; }
        public string Currency { get; set; }
        public DateTime Recorded { get; set; }
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public int BarsType { get; set; }
        public int BarsValue { get; set; }
        public bool CostsIncluded { get; set; }
        public double Slippage { get; set; }
        public double Commission { get; set; }
        public int Trades { get; set; }
        public double Net { get; set; }
        public double Drawdown { get; set; }
        public double WinRate { get; set; }
        public double ProfitFactor { get; set; }
        public double AverageTrade { get; set; }
        public double TradesPerDay { get; set; }
        public StrategyParameters Parameters { get; set; }
        public string SettingsKey { get; set; }
        public string SourceHash { get; set; }
        public string Action { get; set; }
        public string Label { get { return Recorded.ToString("dd MMM HH:mm") + " · " + Strategy + " · " + Instrument + " · " + Timeframe; } }
        public string Timeframe { get { return BarsValue + (BarsType == 3 ? " seconds" : " (bar type " + BarsType + ")"); } }
        public bool IsTwentySeconds { get { return BarsType == 3 && BarsValue == 20; } }
        public string CostLabel { get { return CostsIncluded ? "Commission included: " + Commission.ToString("N2") + "; slippage " + Slippage + " ticks" : "Commission OFF; slippage " + Slippage + " ticks"; } }
    }
    public static class NativeReports
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        public static string Hash(string text)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text.Replace("\r\n", "\n")))).Replace("-", "");
        }
        private static string Read(XElement e, string name)
        {
            var value = e.Element(name); if (value == null) throw new InvalidDataException("Backtest report is missing " + name + "."); return value.Value;
        }
        public static string SourceIdentity(string source, string strategy)
        {
            // Strategy Analyzer saves a dated class/Name alias in its source snapshot.
            // Normalize only that alias, retaining every other byte of source semantics.
            var match = Regex.Match(source, @"public\s+class\s+(\w+)\s*:\s*Strategy\b");
            if (!match.Success) return null;
            string alias = match.Groups[1].Value;
            if (alias != strategy && !Regex.IsMatch(alias, "^" + Regex.Escape(strategy) + @"_\d{4}_\d{2}_\d{2}_\d+$")) return null;
            return Hash(Regex.Replace(source, @"\b" + Regex.Escape(alias) + @"\b", strategy));
        }
        private static double Number(string text)
        {
            double n; if (!double.TryParse(text, NumberStyles.Float, Inv, out n) || !StrategyParameters.Finite(n)) throw new InvalidDataException("Invalid numeric value in report: " + text); return n;
        }
        public static NativeBacktest Load(string path)
        {
            if (new FileInfo(path).Length > 16 * 1024 * 1024) throw new InvalidDataException("Report exceeds 16 MB.");
            string text = File.ReadAllText(path);
            var config = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 16 * 1024 * 1024 };
            XDocument document; using (var reader = XmlReader.Create(new StringReader(text), config)) document = XDocument.Load(reader);
            var root = document.Root == null ? null : document.Root.Element("StrategyAnalyzerGridEntry");
            if (root == null) throw new InvalidDataException("Expected a NinjaTrader StrategyAnalyzerLog XML report, not an execution CSV.");
            var summary = root.Descendants("SummaryPerformances").FirstOrDefault(e => (string)e.Element("PerformanceUnit") == "Currency" && e.Element("SummaryPerformancesSerialize") != null);
            if (summary == null) throw new InvalidDataException("This report has no currency performance summary.");
            var metrics = new Dictionary<string, string>();
            foreach (string part in Read(summary, "SummaryPerformancesSerialize").Split('|'))
            {
                string[] cells = part.Split(';'); if (cells.Length >= 2) metrics[cells[0]] = cells[1];
            }
            Func<string, double> metric = key => { if (!metrics.ContainsKey(key)) throw new InvalidDataException("Missing metric: " + key); return Number(metrics[key]); };
            var series = root.Element("DataSeries"); if (series == null) throw new InvalidDataException("Missing timeframe.");
            var result = new NativeBacktest {
                FilePath = Path.GetFullPath(path), ReportHash = Hash(text), Strategy = Read(root, "StrategyName"), Instrument = Read(root, "Instrument"), Currency = Read(summary, "Denomination"),
                Recorded = DateTime.Parse(Read(root, "Date"), Inv, DateTimeStyles.RoundtripKind), From = DateTime.Parse(Read(root, "From"), Inv, DateTimeStyles.RoundtripKind), To = DateTime.Parse(Read(root, "To"), Inv, DateTimeStyles.RoundtripKind),
                BarsType = int.Parse(Read(series, "BarsPeriodTypeSerialize"), Inv), BarsValue = int.Parse(Read(series, "Value"), Inv),
                CostsIncluded = bool.Parse(Read(root, "IncludeCommission")), Slippage = Number(Read(root, "Slippage")),
                Commission = metric("Commission"), Net = metric("TotalNetProfit"), Drawdown = Math.Abs(metric("MaxDrawdown")),
                Trades = checked((int)metric("TotalNumTrades")), WinRate = metric("PercentProfitable") * 100, AverageTrade = metric("AverageTrade"),
                TradesPerDay = metric("AverageNumTradesPerDay"), Action = Read(root, "Action"),
                ProfitFactor = metrics.ContainsKey("ProfitFactor") && metrics["ProfitFactor"] != "Infinity" ? Number(metrics["ProfitFactor"]) : 0
            };
            if (result.Currency == "UsDollar") result.Currency = "USD";
            if (result.To < result.From || result.Trades < 0 || result.Commission < 0 || result.Slippage < 0 || result.WinRate < 0 || result.WinRate > 100) throw new InvalidDataException("Invalid backtest range or metrics.");
            var parameters = root.Descendants("ParameterWrapper").Where(e => e.Element("Name") != null && e.Element("Value") != null).ToDictionary(e => e.Element("Name").Value, e => e.Element("Value").Value);
            if (new[] { "FastPeriod", "SlowPeriod", "FilterPeriod", "MinimumAdx", "StopAtrMultiplier", "TargetAtrMultiplier" }.All(parameters.ContainsKey))
            {
                result.Parameters = new StrategyParameters { Fast = int.Parse(parameters["FastPeriod"], Inv), Slow = int.Parse(parameters["SlowPeriod"], Inv), Filter = int.Parse(parameters["FilterPeriod"], Inv), Adx = Number(parameters["MinimumAdx"]), Stop = Number(parameters["StopAtrMultiplier"]), Target = Number(parameters["TargetAtrMultiplier"]) };
                result.Parameters.Validate();
            }
            // Exclude strategy parameters/name; compare the remaining explicit execution assumptions.
            var keys = new[] { "EntryHandling", "EntriesPerDirection", "ExitOnSessionClose", "ExitOnSessionCloseSeconds", "FillType", "FillTypeType", "FillTypeValue", "FillLimitOrdersOnTouch", "IsBreakAtEod", "IsTickReplay", "MinBarsRequired", "MaximumBarsLookBack", "TradingHoursTemplate", "SetOrderQuantity", "StopTargetHandling" };
            string settings = string.Join("|", keys.Select(k => k + "=" + Read(root, k))) + "|" + series.ToString(SaveOptions.DisableFormatting);
            // Quantity and commission template may only be present in the embedded template.
            string template = (string)root.Element("StrategyTemplate");
            if (string.IsNullOrWhiteSpace(template)) throw new InvalidDataException("Missing strategy template; cannot establish test assumptions.");
            XDocument embedded; using (var reader = XmlReader.Create(new StringReader(template), config)) embedded = XDocument.Load(reader);
            foreach (string key in new[] { "DefaultQuantity", "Calculate", "CommissionTemplate", "TradingHoursSerializable", "IsFillLimitOnTouch" })
                settings += "|" + key + "=" + string.Join(";", embedded.Descendants(key).Select(e => e.Value));
            result.SettingsKey = Hash(settings);
            // The report's source path is data, not permission to read arbitrary local files.
            string source = (string)root.Element("NinjaScriptFile");
            if (!string.IsNullOrEmpty(source))
            {
                string localSource = Path.Combine(Path.GetDirectoryName(result.FilePath), Path.GetFileName(source));
                if (File.Exists(localSource) && new FileInfo(localSource).Length < 2 * 1024 * 1024 && (File.GetAttributes(localSource) & FileAttributes.ReparsePoint) == 0)
                    result.SourceHash = SourceIdentity(File.ReadAllText(localSource), result.Strategy);
            }
            return result;
        }
        public static List<NativeBacktest> Scan(string home, IList<string> errors)
        {
            string folder = Path.Combine(home, "strategyanalyzerlogs");
            if (!Directory.Exists(folder)) return new List<NativeBacktest>();
            var result = new List<NativeBacktest>();
            foreach (string path in Directory.EnumerateFiles(folder, "*.xml").OrderByDescending(File.GetLastWriteTimeUtc).Take(1000))
            {
                try { var report = Load(path); if (report.Action == "Backtest") result.Add(report); }
                catch (Exception e) { errors.Add(Path.GetFileName(path) + ": " + e.Message); }
            }
            return result.OrderByDescending(r => r.Recorded).ToList();
        }
    }
    public sealed class ImprovementProposal
    {
        public string Title { get; set; }
        public string Reason { get; set; }
        public string Expected { get; set; }
        public string Tradeoff { get; set; }
        public StrategyParameters Parameters { get; set; }
        public string Label { get { return Title + " — UNTESTED"; } }
        public string Explain(NativeBacktest baseline)
        {
            return Title + "\n\nEvidence: " + Reason + "\n\nExact changes: " + AdaptationEngine.Changes(baseline.Parameters, Parameters) +
                "\n\nHypothesis: " + Expected + "\n\nTrade-off: " + Tradeoff +
                "\n\nMeasured improvement: NOT YET KNOWN. Run this version and the baseline with identical 20-second data, commission, slippage, quantity and fill settings. Validate again on a later, non-overlapping period before using it. No future gain is guaranteed.";
        }
    }
    public static class AdaptationEngine
    {
        public static List<ImprovementProposal> Propose(NativeBacktest baseline)
        {
            if (!baseline.IsTwentySeconds || baseline.Parameters == null || baseline.Action != "Backtest" || !(baseline.Strategy == "ATL_EMA_Trend" || baseline.Strategy.StartsWith("ATL20_", StringComparison.Ordinal)))
                throw new InvalidDataException("Select an ATL EMA-family backtest on 20-second bars. Other strategy code cannot be adapted by this engine.");
            if (baseline.Trades < 50) throw new InvalidDataException("At least 50 completed trades are required to propose changes; collect more baseline data.");
            var p = baseline.Parameters; var result = new List<ImprovementProposal>();
            if (p.Adx < 50)
            {
                var next = p.Copy(); next.Adx = Math.Min(50, p.Adx + 5);
                result.Add(new ImprovementProposal { Title = "Stricter trend filter", Parameters = next,
                    Reason = string.Format(CultureInfo.InvariantCulture, "{0:N0} trades, {1:N1}/day; average trade {2:N2} {3}, win rate {4:N2}%.", baseline.Trades, baseline.TradesPerDay, baseline.AverageTrade, baseline.Currency, baseline.WinRate),
                    Expected = "Require stronger ADX before a crossover can enter. This may skip weak-trend entries and reduce turnover; the recorded fills alone cannot prove which skipped trades would win or lose.",
                    Tradeoff = "Fewer opportunities; may miss early trends. Profit and drawdown can worsen." });
            }
            if (p.Slow < 5000)
            {
                var next = p.Copy(); next.Fast = Math.Max(p.Fast + 1, (int)Math.Round(p.Fast * 1.3)); next.Slow = Math.Max(next.Fast + 1, (int)Math.Round(p.Slow * 1.3));
                result.Add(new ImprovementProposal { Title = "Slower crossover response", Parameters = next,
                    Reason = "On 20-second bars, the baseline EMA windows are approximately " + (p.Fast * 20) + " and " + (p.Slow * 20) + " seconds. Observed drawdown is " + baseline.Drawdown.ToString("N2") + " " + baseline.Currency + ".",
                    Expected = "Smooth more of the 20-second price variation before a crossover. Test whether fewer reversals improve net results after costs.",
                    Tradeoff = "Later entries and exits; fast moves can be missed. This is a hypothesis, not a diagnosed cause of the losses." });
            }
            if (p.Target > .02)
            {
                var next = p.Copy(); next.Target = Math.Max(.01, Math.Round(p.Target * .8, 2));
                result.Add(new ImprovementProposal { Title = "Closer profit target", Parameters = next,
                    Reason = "The baseline wins " + baseline.WinRate.ToString("N2") + "% of completed trades with a target of " + p.Target + " ATR.",
                    Expected = "Take profit at a smaller favorable move. Test whether a higher hit rate offsets smaller winners.",
                    Tradeoff = "Lower payoff per winner; tighter targets can reduce total profit even if win rate rises." });
            }
            return result;
        }
        public static string Changes(StrategyParameters a, StrategyParameters b)
        {
            var changes = new List<string>();
            Action<string, double, double> add = (name, before, after) => { if (before != after) changes.Add(name + ": " + before.ToString(CultureInfo.InvariantCulture) + " → " + after.ToString(CultureInfo.InvariantCulture)); };
            add("Fast EMA", a.Fast, b.Fast); add("Slow EMA", a.Slow, b.Slow); add("ADX/ATR period", a.Filter, b.Filter); add("Minimum ADX", a.Adx, b.Adx); add("Stop ATR", a.Stop, b.Stop); add("Target ATR", a.Target, b.Target);
            return changes.Count == 0 ? "No parameter changes" : string.Join("; ", changes);
        }
        public static string Incompatible(NativeBacktest a, NativeBacktest b, bool samePeriod)
        {
            if (!a.IsTwentySeconds || !b.IsTwentySeconds) return "Both tests must use 20-second bars.";
            if (a.Instrument != b.Instrument || a.Currency != b.Currency) return "Instrument/contract or currency differs.";
            if (a.Action != "Backtest" || b.Action != "Backtest") return "Use individual historical backtests, not optimization summaries.";
            if (a.SettingsKey != b.SettingsKey || a.CostsIncluded != b.CostsIncluded || a.Slippage != b.Slippage) return "Execution, quantity, session or cost assumptions differ.";
            if (samePeriod && (a.From != b.From || a.To != b.To)) return "Date ranges differ. Compare the same test period.";
            return null;
        }
        public static string Compare(NativeBacktest baseline, NativeBacktest candidate)
        {
            string mismatch = Incompatible(baseline, candidate, true); if (mismatch != null) return "NOT COMPARABLE: " + mismatch;
            return string.Format(CultureInfo.InvariantCulture,
                "OBSERVED BACKTEST CHANGE — not a prediction\nP&L: {0:N2} → {1:N2} {2} (delta {3:+0.00;-0.00;0.00})\nMaximum drawdown: {4:N2} → {5:N2} (delta {6:+0.00;-0.00;0.00}; lower is better)\nWin rate: {7:N2}% → {8:N2}%\nTrades: {9:N0} → {10:N0}\n\n{11}\n\nSame-period comparisons can be overfit. Require a later non-overlapping validation period. Reports do not certify that the historical data feed/cache was identical.",
                baseline.Net, candidate.Net, baseline.Currency, candidate.Net - baseline.Net, baseline.Drawdown, candidate.Drawdown, candidate.Drawdown - baseline.Drawdown,
                baseline.WinRate, candidate.WinRate, baseline.Trades, candidate.Trades,
                baseline.CostsIncluded && baseline.Slippage > 0 ? "Commission and nonzero slippage enabled; review the actual cost assumptions in NinjaTrader." : "COSTS INCOMPLETE: commission and nonzero slippage are required before an automatic recommendation.");
        }
        public static bool Passes(NativeBacktest a, NativeBacktest b)
        {
            return Incompatible(a, b, true) == null && a.CostsIncluded && b.CostsIncluded && a.Commission > 0 && b.Commission > 0 && a.Slippage > 0 &&
                a.Trades >= 50 && b.Trades >= 50 && b.Net > Math.Max(0, a.Net) && b.Drawdown <= a.Drawdown && b.AverageTrade > a.AverageTrade;
        }
        public static bool Validated(NativeBacktest original, NativeBacktest changed, NativeBacktest holdoutOriginal, NativeBacktest holdoutChanged)
        {
            return Passes(original, changed) && Passes(holdoutOriginal, holdoutChanged) &&
                Incompatible(original, holdoutOriginal, false) == null && Incompatible(changed, holdoutChanged, false) == null &&
                holdoutOriginal.From > original.To && holdoutChanged.From > changed.To &&
                original.Parameters != null && changed.Parameters != null && holdoutOriginal.Parameters != null && holdoutChanged.Parameters != null &&
                original.Parameters.Key == holdoutOriginal.Parameters.Key && changed.Parameters.Key == holdoutChanged.Parameters.Key &&
                !string.IsNullOrEmpty(original.SourceHash) && !string.IsNullOrEmpty(changed.SourceHash) &&
                original.SourceHash == holdoutOriginal.SourceHash && changed.SourceHash == holdoutChanged.SourceHash;
        }
    }
}
