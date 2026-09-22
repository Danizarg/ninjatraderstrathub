using System;
using System.Globalization;
using System.IO;
using AdaptiveTradingLab.Desktop;

public static class DesktopDataTests
{
    private static int checks;
    private static void Assert(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    public static int Main(string[] args)
    {
        try
        {
            string root = args[0]; Directory.CreateDirectory(root);
            string run = Path.Combine(root, "demo-export"); Directory.CreateDirectory(run);
            // File order differs from exit-time order. Breakeven is not a winning trade.
            File.WriteAllText(Path.Combine(run, "a.json"), Trade("third", "2026-01-03T10:00:00Z", 0));
            File.WriteAllText(Path.Combine(run, "b.json"), Trade("second", "2026-01-02T10:00:00Z", 20));
            File.WriteAllText(Path.Combine(run, "c.json"), Trade("first", "2026-01-01T10:00:00Z", -10));
            File.WriteAllText(Path.Combine(run, "broken.json"), "{unfinished");
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("es-ES");
            var result = LabData.LoadRun(root);
            Assert(result.Trades.Count == 3 && result.Skipped == 1, "Malformed JSON must be counted and skipped");
            Assert(result.Trades[0].Id == "first", "Trades must sort by exit time");
            Assert(result.Net == 10 && result.Drawdown == 10, "Net P&L and drawdown calculation");
            Assert(Math.Abs(result.WinRate - 100.0 / 3) < 0.0001, "Win rate includes breakeven denominator");
            Assert(result.Equity.Count == 4 && result.Equity[0] == 0 && result.Equity[1] == -10, "Equity starts at zero");
            Assert(result.Trades[0].Entry == 5000.25, "JSON number parsing is culture independent");
            File.WriteAllText(Path.Combine(run, "null.json"), "null");
            Assert(LabData.LoadRun(root).Skipped == 2, "Null JSON record must not abort the entire run");
            Assert(LabData.LoadRun(Path.Combine(root, "missing")).Trades.Count == 0, "Empty workspace");
            var csv = LabData.ReadCsv(new StringReader("a,b\r\n\"one, two\",\"say \"\"hi\"\"\"\r\n\"multi\nline\",ok\r\n\"partial"));
            Assert(csv.Count == 3 && csv[1][0] == "one, two" && csv[1][1] == "say \"hi\"", "CSV quote and comma handling");
            Assert(csv[2][0] == "multi\nline", "Quoted newlines and trailing partial record");
            string executionFile = Path.Combine(root, "ATL_fixture.csv");
            File.WriteAllText(executionFile, "schema_version,time,instrument,mode,price,quantity,market_position\r\n1,2026-01-01T12:00:00,ES 12-26,Historical,5000.25,1,Long\r\n1,unfinished");
            var executions = LabData.LoadExecutions(executionFile);
            Assert(executions.Count == 1 && executions[0].Price == "5000.25", "Read complete execution while file is being appended");
            File.WriteAllText(executionFile, "wrong,header\r\n");
            bool refused = false; try { LabData.LoadExecutions(executionFile); } catch (InvalidDataException) { refused = true; }
            Assert(refused, "Reject incompatible execution schema");
            Assert(LabData.QuoteArgument("D:\\folder with spaces\\") == "\"D:\\folder with spaces\\\\\"", "Quote trailing backslash");
            Assert(LabData.QuoteArgument("a\"b") == "\"a\\\"b\"", "Quote embedded quote");
            string ntRoot = Path.Combine(root, "NinjaTrader 8");
            Directory.CreateDirectory(Path.Combine(ntRoot, "bin", "Custom"));
            File.WriteAllText(Path.Combine(ntRoot, "bin", "Custom", "NinjaTrader.Custom.csproj"), "<Project />");
            Assert(LabData.ResolveNinjaTraderHome(Path.Combine(ntRoot, "workspaces", "recovery", "template")) == ntRoot, "Recover NinjaTrader root from a selected template subfolder");
            Console.WriteLine("PASS: " + checks + " desktop data checks."); return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
    private static string Trade(string id, string time, double net)
    {
        return "{\"tradeId\":\"" + id + "\",\"contract\":\"ES 12-26\",\"exitTimestamp\":\"" + time + "\",\"entryPrice\":5000.25,\"exitPrice\":5001.25,\"quantity\":1,\"netPnL\":" + net.ToString(CultureInfo.InvariantCulture) + "}";
    }
}
