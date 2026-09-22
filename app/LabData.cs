using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace AdaptiveTradingLab.Desktop
{
    public class WorkspaceSettings
    {
        public string OutputRoot { get; set; }
        public string NinjaTraderHome { get; set; }
        public string ExportsRoot { get; set; }
        public static WorkspaceSettings Defaults()
        {
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AdaptiveTradingLab");
            return new WorkspaceSettings { OutputRoot = root, NinjaTraderHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "NinjaTrader 8"), ExportsRoot = Path.Combine(root, "ninjatrader", "executions") };
        }
    }
    public class FileChoice
    {
        public string Path { get; set; }
        public string Label { get; set; }
    }
    public class TradeRow
    {
        public string Id { get; set; }
        public string Instrument { get; set; }
        public DateTimeOffset ExitTime { get; set; }
        public string ExitLabel { get { return ExitTime.ToString("yyyy-MM-dd HH:mm zzz"); } }
        public int Quantity { get; set; }
        public double Entry { get; set; }
        public double Exit { get; set; }
        public double Net { get; set; }
    }
    public class ExecutionRow
    {
        public string Time { get; set; }
        public string Instrument { get; set; }
        public string Mode { get; set; }
        public string Price { get; set; }
        public string Quantity { get; set; }
        public string Position { get; set; }
    }
    public class RunResult
    {
        public List<TradeRow> Trades = new List<TradeRow>();
        public List<double> Equity = new List<double> { 0 };
        public double Net;
        public double WinRate;
        public double Drawdown;
        public int Skipped;
    }
    public static class LabData
    {
        public static string ResolveNinjaTraderHome(string path)
        {
            var folder = new DirectoryInfo(Path.GetFullPath(path));
            while (folder != null)
            {
                if (File.Exists(Path.Combine(folder.FullName, "bin", "Custom", "NinjaTrader.Custom.csproj"))) return folder.FullName;
                folder = folder.Parent;
            }
            return Path.GetFullPath(path);
        }
        public static List<FileChoice> Runs(string outputRoot)
        {
            string dir = Path.Combine(outputRoot, "runs");
            if (!Directory.Exists(dir)) return new List<FileChoice>();
            return new DirectoryInfo(dir).GetDirectories().OrderByDescending(d => d.LastWriteTimeUtc)
                .Select(d => new FileChoice { Path = d.FullName, Label = d.LastWriteTime.ToString("dd MMM yyyy  HH:mm:ss") + "  ·  " + d.Name.Substring(0, Math.Min(8, d.Name.Length)) }).ToList();
        }
        public static List<FileChoice> ExecutionFiles(string dir)
        {
            if (!Directory.Exists(dir)) return new List<FileChoice>();
            return new DirectoryInfo(dir).GetFiles("ATL_*.csv").OrderByDescending(f => f.LastWriteTimeUtc)
                .Select(f => new FileChoice { Path = f.FullName, Label = f.LastWriteTime.ToString("dd MMM yyyy  HH:mm:ss") + "  ·  " + f.Name.Substring(0, Math.Min(12, f.Name.Length)) }).ToList();
        }
        public static RunResult LoadRun(string path)
        {
            var result = new RunResult();
            string dir = Path.Combine(path, "demo-export");
            if (!Directory.Exists(dir)) return result;
            var serializer = new JavaScriptSerializer();
            foreach (string file in Directory.EnumerateFiles(dir, "*.json"))
            {
                try
                {
                    var value = serializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(file));
                    if (value == null) throw new FormatException("Empty trade record");
                    var trade = new TradeRow { Id = Convert.ToString(value["tradeId"]), Instrument = Convert.ToString(value["contract"]),
                        ExitTime = DateTimeOffset.Parse(Convert.ToString(value["exitTimestamp"]), CultureInfo.InvariantCulture),
                        Entry = Convert.ToDouble(value["entryPrice"], CultureInfo.InvariantCulture), Exit = Convert.ToDouble(value["exitPrice"], CultureInfo.InvariantCulture),
                        Quantity = Convert.ToInt32(value["quantity"], CultureInfo.InvariantCulture), Net = Convert.ToDouble(value["netPnL"], CultureInfo.InvariantCulture) };
                    if (double.IsNaN(trade.Net) || double.IsInfinity(trade.Net)) throw new FormatException("Nonfinite net P&L");
                    result.Trades.Add(trade);
                }
                catch (Exception e)
                {
                    if (!(e is IOException || e is ArgumentException || e is InvalidOperationException || e is KeyNotFoundException || e is FormatException || e is OverflowException)) throw;
                    result.Skipped++;
                }
            }
            result.Trades = result.Trades.OrderBy(t => t.ExitTime).ThenBy(t => t.Id, StringComparer.Ordinal).ToList();
            double peak = 0;
            foreach (var trade in result.Trades)
            {
                result.Net += trade.Net;
                peak = Math.Max(peak, result.Net);
                result.Drawdown = Math.Max(result.Drawdown, peak - result.Net);
                result.Equity.Add(result.Net);
            }
            result.WinRate = result.Trades.Count == 0 ? 0 : 100.0 * result.Trades.Count(t => t.Net > 0) / result.Trades.Count;
            return result;
        }
        // Parse RFC4180 quoting, including escaped quotes and newlines inside a quoted field.
        // Discard an unfinished trailing record while NinjaTrader is appending to the file.
        public static List<string[]> ReadCsv(TextReader reader)
        {
            var rows = new List<string[]>(); var fields = new List<string>(); var field = new StringBuilder();
            bool quoted = false;
            int c;
            while ((c = reader.Read()) != -1)
            {
                char ch = (char)c;
                if (ch == '"')
                {
                    if (quoted && reader.Peek() == '"') { reader.Read(); field.Append('"'); }
                    else quoted = !quoted;
                }
                else if (!quoted && ch == ',') { fields.Add(field.ToString()); field.Clear(); }
                else if (!quoted && (ch == '\n' || ch == '\r'))
                {
                    if (ch == '\r' && reader.Peek() == '\n') reader.Read();
                    fields.Add(field.ToString()); rows.Add(fields.ToArray()); fields.Clear(); field.Clear();
                }
                else field.Append(ch);
            }
            return rows;
        }
        public static List<ExecutionRow> LoadExecutions(string path)
        {
            List<string[]> rows;
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(file)) rows = ReadCsv(reader);
            var result = new List<ExecutionRow>();
            if (rows.Count == 0) return result;
            string[] names = { "schema_version", "time", "instrument", "mode", "price", "quantity", "market_position" };
            var indexes = names.Select(n => Array.IndexOf(rows[0], n)).ToArray();
            if (indexes.Any(i => i < 0)) throw new InvalidDataException("Execution CSV is missing required columns.");
            foreach (var row in rows.Skip(1))
            {
                if (row.Length != rows[0].Length) continue;
                if (row[indexes[0]] != "1") throw new InvalidDataException("Unsupported execution schema version.");
                result.Add(new ExecutionRow { Time = row[indexes[1]], Instrument = row[indexes[2]], Mode = row[indexes[3]], Price = row[indexes[4]], Quantity = row[indexes[5]], Position = row[indexes[6]] });
            }
            return result;
        }
        public static string QuoteArgument(string value)
        {
            // CommandLineToArgvW quoting, including a quoted path ending in a backslash.
            var b = new StringBuilder("\""); int slashes = 0;
            foreach (char c in value)
            {
                if (c == '\\') { slashes++; continue; }
                if (c == '"') { b.Append('\\', slashes * 2 + 1); b.Append(c); }
                else { b.Append('\\', slashes); b.Append(c); }
                slashes = 0;
            }
            b.Append('\\', slashes * 2); b.Append('"'); return b.ToString();
        }
    }
}
