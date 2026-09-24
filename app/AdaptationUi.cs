using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace AdaptiveTradingLab.Desktop
{
    public sealed partial class DesktopWindow
    {
        private List<NativeBacktest> nativeReports = new List<NativeBacktest>();
        private List<ImprovementProposal> proposals = new List<ImprovementProposal>();
        private StrategyLibrary library;
        private bool preview = Environment.GetEnvironmentVariable("ATL_PREVIEW") == "1";
        private string templateSource;
        private int nativeRefreshVersion;
        private NativeBacktest Baseline { get { return Get<ComboBox>("NativePicker").SelectedItem as NativeBacktest; } }
        private StrategyVersion SelectedVersion { get { return Get<ComboBox>("VersionPicker").SelectedItem as StrategyVersion; } }
        private void InitializeAdaptation()
        {
            library = new StrategyLibrary(Path.Combine(DesktopDataRoot(), "adaptation"));
            using (var reader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("StrategyTemplate.cs"))) templateSource = reader.ReadToEnd();
            if (preview) Window.Title = "Adaptive Trading Lab — Development preview (installation disabled)";
            Get<ComboBox>("NativePicker").SelectionChanged += (s, e) => ShowNative();
            Get<ComboBox>("ComparisonPicker").SelectionChanged += (s, e) => ShowComparison();
            Get<ComboBox>("ProposalPicker").SelectionChanged += (s, e) => ShowProposal();
            Get<ComboBox>("VersionPicker").SelectionChanged += (s, e) => ShowVersion();
            Get<ComboBox>("StrategyFilePicker").SelectionChanged += (s, e) => ShowStrategyFile();
            Get<Button>("ImportReportButton").Click += (s, e) => ImportNative();
            Get<Button>("SuggestButton").Click += (s, e) => Suggest(false);
            Get<Button>("AutoCreateButton").Click += (s, e) => Suggest(true);
            Get<Button>("CreateProposalButton").Click += (s, e) => CreateProposal();
            Get<Button>("CustomVersionButton").Click += (s, e) => CreateCustom();
            Get<Button>("RecommendButton").Click += (s, e) => AutoRecommend();
            Get<Button>("InstallVersionButton").Click += (s, e) => InstallVersion();
            Get<Button>("RefreshFilesButton").Click += (s, e) => RefreshFiles();
            Get<Button>("SaveSourceButton").Click += (s, e) => SaveStrategy();
            Get<Button>("ImportSourceButton").Click += (s, e) => ImportStrategy();
            Get<Button>("ArchiveSourceButton").Click += (s, e) => ArchiveStrategy();
            Get<Button>("RestoreSourceButton").Click += (s, e) => RestoreStrategy();
            Get<Button>("OpenLibraryButton").Click += (s, e) => { if (Directory.Exists(library.Root)) OpenFolder(library.Root); };
            RefreshVersions();
        }
        private void FeatureError(Exception e) { Error(e); }
        private async Task RefreshNativeReports()
        {
            try
            {
                int revision = ++nativeRefreshVersion;
                var errors = new List<string>(); string home = settings.NinjaTraderHome; var old = Baseline;
                var scanned = await Task.Run(() => NativeReports.Scan(home, errors));
                if (revision != nativeRefreshVersion || home != settings.NinjaTraderHome) return;
                nativeReports = scanned;
                string imports = Path.Combine(library.Root, "imported-reports");
                if (Directory.Exists(imports)) foreach (string file in Directory.EnumerateFiles(imports, "*.xml", SearchOption.AllDirectories).Take(1000))
                {
                    try { var report = NativeReports.Load(file); if (!nativeReports.Any(r => r.ReportHash == report.ReportHash)) nativeReports.Add(report); }
                    catch (Exception e) { errors.Add(Path.GetFileName(file) + ": " + e.Message); }
                }
                nativeReports = nativeReports.OrderByDescending(r => r.Recorded).ToList();
                Get<ComboBox>("NativePicker").ItemsSource = nativeReports;
                Get<ComboBox>("NativePicker").SelectedItem = (old == null ? null : nativeReports.FirstOrDefault(r => r.ReportHash == old.ReportHash)) ?? nativeReports.FirstOrDefault(r => r.IsTwentySeconds) ?? nativeReports.FirstOrDefault();
                Get<ComboBox>("ComparisonPicker").ItemsSource = nativeReports;
                Text("NativeLoadStatus", nativeReports.Count + " saved backtests found. " + (errors.Count == 0 ? "Read directly from NinjaTrader's Strategy Analyzer logs." : errors.Count + " unreadable/unsupported files; see activity log."));
                foreach (string error in errors.Take(15)) Log("Backtest import: " + error);
                ShowNative(); RefreshVersions(); RefreshFiles();
            }
            catch (Exception e) { Text("NativeLoadStatus", "Cannot load backtests: " + e.Message); Log(e.Message); }
        }
        private void ShowNative()
        {
            var r = Baseline;
            foreach (string name in new[] { "NativeNet", "NativeTrades", "NativeWin", "NativeDrawdown" }) Text(name, "—");
            proposals.Clear(); Get<ComboBox>("ProposalPicker").ItemsSource = null; Text("ProposalExplanation", "Select a 20-second ATL backtest, then suggest changes.");
            if (r == null) { Text("AdaptBaseline", "Choose a baseline on Backtests."); ShowComparison(); Text("NativeDetails", "No saved backtest found. Run ATL_EMA_Trend in NinjaTrader Strategy Analyzer, or import its saved XML log."); return; }
            Text("NativeNet", r.Net.ToString("N2") + " " + r.Currency); Text("NativeTrades", r.Trades.ToString("N0")); Text("NativeWin", r.WinRate.ToString("N2") + "%"); Text("NativeDrawdown", r.Drawdown.ToString("N2"));
            Text("NativeDetails", r.Strategy + " | " + r.Instrument + " | " + r.Timeframe + "\n" + r.From.ToString("yyyy-MM-dd") + " to " + r.To.ToString("yyyy-MM-dd") + "\n" + r.CostLabel +
                "\nAverage trade: " + r.AverageTrade.ToString("N2") + "; trades/day: " + r.TradesPerDay.ToString("N2") + "\n" + (r.Parameters == null ? "Unsupported parameter family" : r.Parameters.Summary) +
                "\nSource: NinjaTrader saved historical backtest summary. Historical results are not live-account performance. No equity curve is inferred from fill-only CSV files.");
            Text("AdaptBaseline", "Baseline: " + r.Label);
            if (r.Parameters != null) FillParameters(r.Parameters);
            ShowComparison();
        }
        private void ShowComparison()
        {
            var candidate = Get<ComboBox>("ComparisonPicker").SelectedItem as NativeBacktest;
            Text("ComparisonText", Baseline == null || candidate == null ? "Choose a second backtest to see exact P&L, drawdown, win-rate and trade-count differences." : AdaptationEngine.Compare(Baseline, candidate));
        }
        private void ImportNative()
        {
            try
            {
                var dialog = new OpenFileDialog { Filter = "NinjaTrader backtest log (*.xml)|*.xml", InitialDirectory = Path.Combine(settings.NinjaTraderHome, "strategyanalyzerlogs") };
                if (dialog.ShowDialog(Window) != true) return;
                var report = NativeReports.Load(dialog.FileName);
                string folder = Path.Combine(library.Root, "imported-reports", report.ReportHash); StrategyLibrary.SafeAncestors(folder); Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, "report.xml"); if (!File.Exists(path)) File.Copy(dialog.FileName, path);
                // Preserve only the adjacent source snapshot whose identity was validated while reading.
                if (!string.IsNullOrEmpty(report.SourceHash))
                {
                    foreach (string snapshot in Directory.EnumerateFiles(Path.GetDirectoryName(dialog.FileName), "*.cs"))
                    {
                        if (new FileInfo(snapshot).Length > 2 * 1024 * 1024 || (File.GetAttributes(snapshot) & FileAttributes.ReparsePoint) != 0) continue;
                        if (NativeReports.SourceIdentity(File.ReadAllText(snapshot), report.Strategy) != report.SourceHash) continue;
                        string destination = Path.Combine(folder, Path.GetFileName(snapshot));
                        StrategyLibrary.SafeAncestors(destination);
                        if (!File.Exists(destination)) File.Copy(snapshot, destination);
                    }
                }
                report = NativeReports.Load(path);
                nativeReports.RemoveAll(r => r.ReportHash == report.ReportHash); nativeReports.Insert(0, report);
                Get<ComboBox>("NativePicker").ItemsSource = nativeReports.ToList(); Get<ComboBox>("NativePicker").SelectedItem = report;
                Get<ComboBox>("ComparisonPicker").ItemsSource = nativeReports.ToList();
                Log("Imported saved backtest: " + report.Label);
            }
            catch (Exception e) { FeatureError(e); }
        }
        private void RequireBaseline()
        {
            if (Baseline == null) throw new InvalidOperationException("Select a native backtest first.");
            if (!Baseline.IsTwentySeconds || Baseline.Parameters == null) throw new InvalidOperationException("Select an ATL EMA-family result on 20-second bars.");
        }
        private void Suggest(bool createAll)
        {
            try
            {
                RequireBaseline(); proposals = AdaptationEngine.Propose(Baseline);
                Get<ComboBox>("ProposalPicker").ItemsSource = proposals; Get<ComboBox>("ProposalPicker").SelectedIndex = proposals.Count == 0 ? -1 : 0;
                Get<TabControl>("Pages").SelectedItem = Get<TabItem>("AdaptationTab");
                if (createAll)
                {
                    foreach (var p in proposals) library.Create(templateSource, Baseline, p.Parameters, p.Explain(Baseline));
                    RefreshVersions(); Text("StatusText", "Created " + proposals.Count + " UNTESTED candidate versions in the library. Nothing was installed or enabled.");
                }
            }
            catch (Exception e) { FeatureError(e); }
        }
        private void ShowProposal()
        {
            var p = Get<ComboBox>("ProposalPicker").SelectedItem as ImprovementProposal;
            if (p != null && Baseline != null) { Text("ProposalExplanation", p.Explain(Baseline)); FillParameters(p.Parameters); }
        }
        private void FillParameters(StrategyParameters p)
        {
            Get<TextBox>("FastBox").Text = p.Fast.ToString(); Get<TextBox>("SlowBox").Text = p.Slow.ToString(); Get<TextBox>("FilterBox").Text = p.Filter.ToString();
            Get<TextBox>("AdxBox").Text = p.Adx.ToString(CultureInfo.InvariantCulture); Get<TextBox>("StopBox").Text = p.Stop.ToString(CultureInfo.InvariantCulture); Get<TextBox>("TargetBox").Text = p.Target.ToString(CultureInfo.InvariantCulture);
        }
        private void CreateProposal()
        {
            try
            {
                RequireBaseline(); var p = Get<ComboBox>("ProposalPicker").SelectedItem as ImprovementProposal; if (p == null) throw new InvalidOperationException("Suggest and select a proposal first.");
                var version = library.Create(templateSource, Baseline, p.Parameters, p.Explain(Baseline)); RefreshVersions(version.Id);
                Text("StatusText", "Created untested version " + version.ClassName + ". Review and install only when ready to backtest.");
            }
            catch (Exception e) { FeatureError(e); }
        }
        private void CreateCustom()
        {
            try
            {
                RequireBaseline(); AdaptationEngine.Propose(Baseline); var inv = CultureInfo.InvariantCulture;
                var p = new StrategyParameters { Fast = int.Parse(Get<TextBox>("FastBox").Text, inv), Slow = int.Parse(Get<TextBox>("SlowBox").Text, inv), Filter = int.Parse(Get<TextBox>("FilterBox").Text, inv), Adx = double.Parse(Get<TextBox>("AdxBox").Text, inv), Stop = double.Parse(Get<TextBox>("StopBox").Text, inv), Target = double.Parse(Get<TextBox>("TargetBox").Text, inv) };
                p.Validate(); var version = library.Create(templateSource, Baseline, p, "User-controlled 20-second candidate. " + AdaptationEngine.Changes(Baseline.Parameters, p) + ". Benefit unmeasured; backtest before adoption."); RefreshVersions(version.Id);
                Text("StatusText", "Custom version staged in the library. No installed file changed.");
            }
            catch (Exception e) { FeatureError(e); }
        }
        private void RefreshVersions(string selectedId = null)
        {
            try
            {
                string id = selectedId ?? (SelectedVersion == null ? null : SelectedVersion.Id); var versions = library.ListVersions();
                Get<ComboBox>("VersionPicker").ItemsSource = versions; Get<ComboBox>("VersionPicker").SelectedItem = versions.FirstOrDefault(v => v.Id == id) ?? versions.FirstOrDefault();
            }
            catch (Exception e) { Text("VersionDetails", e.Message); Log(e.Message); }
        }
        private void ShowVersion()
        {
            try
            {
                var v = SelectedVersion; Text("VersionDetails", v == null ? "No candidate versions yet." : v.Label + "\n" + v.Parameters.Summary + "\n" + v.Description);
                Get<TextBox>("VersionSourceBox").Text = v == null ? "" : library.ReadSource(v);
            }
            catch (Exception e) { FeatureError(e); }
        }
        private void AutoRecommend()
        {
            try
            {
                RequireBaseline(); var baseline = Baseline;
                StrategyVersion best = null; NativeBacktest bestResult = null, bestHoldout = null, bestOriginalHoldout = null;
                foreach (var version in library.ListVersions().Where(v => v.BaselineReportHash == baseline.ReportHash))
                foreach (var candidate in nativeReports.Where(r => r.Strategy == version.ClassName && r.SourceHash == version.SourceHash && r.Parameters != null && r.Parameters.Key == version.Parameters.Key && AdaptationEngine.Passes(baseline, r)))
                foreach (var originalHoldout in nativeReports.Where(r => r.Strategy == baseline.Strategy && r.From > baseline.To))
                {
                    var holdout = nativeReports.FirstOrDefault(r => r.Strategy == version.ClassName && r.SourceHash == version.SourceHash && AdaptationEngine.Validated(baseline, candidate, originalHoldout, r));
                    if (holdout != null && (bestResult == null || candidate.Net - candidate.Drawdown > bestResult.Net - bestResult.Drawdown)) { best = version; bestResult = candidate; bestHoldout = holdout; bestOriginalHoldout = originalHoldout; }
                }
                if (best == null)
                {
                    Text("RecommendationText", "NO VERSION QUALIFIES YET. Need: the baseline and candidate on the same 20-second date range; commission > 0 and nonzero slippage; ≥50 trades each; candidate net profit positive and higher, average trade higher, drawdown no worse. Repeat that comparison on a later non-overlapping period with unchanged source and parameters. Saved source snapshots must match library versions. No installed strategy changed."); return;
                }
                library.Recommend(best, baseline, bestResult, bestOriginalHoldout, bestHoldout); RefreshVersions(best.Id);
                Text("RecommendationText", best.ClassName + " qualifies under the recorded-test policy.\n\n" + AdaptationEngine.Compare(baseline, bestResult) + "\n\nLATER VALIDATION:\n" + AdaptationEngine.Compare(bestOriginalHoldout, bestHoldout) + "\n\nRecommendation only. Install explicitly, compile in NinjaTrader, then forward-test in simulation. Repeated selection on the same validation period can still overfit.");
            }
            catch (Exception e) { FeatureError(e); }
        }
        private StrategyFolder Folder() { return new StrategyFolder(settings.NinjaTraderHome, Path.Combine(library.Root, "file-backups")); }
        private void GuardStrategyWrite(bool existing)
        {
            if (preview) throw new InvalidOperationException("Development preview: writing to NinjaTrader is disabled. Changes remain in the candidate library.");
            if (existing)
            {
                var processes = Process.GetProcessesByName("NinjaTrader");
                try { if (processes.Length > 0) throw new InvalidOperationException("Close NinjaTrader before editing, archiving or restoring installed source. This avoids changing a strategy that may be running."); }
                finally { foreach (var process in processes) process.Dispose(); }
            }
        }
        private void InstallVersion()
        {
            try
            {
                var v = SelectedVersion; if (v == null) throw new InvalidOperationException("Select a candidate version."); GuardStrategyWrite(false);
                if (MessageBox.Show(Window, "Install " + v.ClassName + " as a separate 20-second strategy? It is " + v.State + ". This will not replace ATL_EMA_Trend or enable trading. Compile with F5 in NinjaTrader afterward.", "Install separate version", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
                Folder().Save(v.ClassName + ".cs", library.ReadSource(v), null); RefreshFiles(); Text("StatusText", "Version installed as a separate file. Compile with F5, then backtest on 20-second bars.");
            }
            catch (Exception e) { FeatureError(e); }
        }
        private void RefreshFiles()
        {
            try { var folder = Folder(); Get<ComboBox>("StrategyFilePicker").ItemsSource = folder.List(); Get<ComboBox>("BackupPicker").ItemsSource = folder.Backups(); }
            catch (Exception e) { Text("FilesStatus", e.Message); }
        }
        private void ShowStrategyFile()
        {
            try { var file = Get<ComboBox>("StrategyFilePicker").SelectedItem as StrategyFile; Get<TextBox>("StrategySourceBox").Text = file == null ? "" : Folder().Read(file); Text("FilesStatus", file == null ? "Select an installed .cs file." : file.RelativePath + " — editing requires NinjaTrader to be closed; save creates a backup."); }
            catch (Exception e) { FeatureError(e); }
        }
        private void SaveStrategy()
        {
            try
            {
                GuardStrategyWrite(true); var file = Get<ComboBox>("StrategyFilePicker").SelectedItem as StrategyFile; if (file == null) return;
                if (MessageBox.Show(Window, "Back up and save changes to " + file.RelativePath + "? Recompile in NinjaTrader afterward.", "Save source", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
                Folder().Save(file.RelativePath, Get<TextBox>("StrategySourceBox").Text, file.Hash); RefreshFiles(); Text("FilesStatus", "Source saved with backup. Compile in NinjaTrader before use.");
            }
            catch (Exception e) { FeatureError(e); }
        }
        private void ImportStrategy()
        {
            try
            {
                GuardStrategyWrite(false); var dialog = new OpenFileDialog { Filter = "NinjaScript source (*.cs)|*.cs" }; if (dialog.ShowDialog(Window) != true) return;
                string name = Path.GetFileName(dialog.FileName);
                if (MessageBox.Show(Window, "Copy " + name + " into your Strategies folder? Existing files will not be overwritten. Imported code is not verified; review before compiling.", "Import source", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
                Folder().Save(name, File.ReadAllText(dialog.FileName), null); RefreshFiles();
            }
            catch (Exception e) { FeatureError(e); }
        }
        private void ArchiveStrategy()
        {
            try
            {
                GuardStrategyWrite(true); var file = Get<ComboBox>("StrategyFilePicker").SelectedItem as StrategyFile; if (file == null) return;
                if (MessageBox.Show(Window, "Move " + file.RelativePath + " out of the compiled source folder into a restorable backup? Remove it from charts first and recompile afterward.", "Archive strategy", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
                Folder().Archive(file); RefreshFiles(); Text("FilesStatus", "Archived source. A compiled copy can remain until NinjaTrader recompiles.");
            }
            catch (Exception e) { FeatureError(e); }
        }
        private void RestoreStrategy()
        {
            try
            {
                GuardStrategyWrite(true); var backup = Get<ComboBox>("BackupPicker").SelectedItem as FileBackup; if (backup == null) return;
                if (MessageBox.Show(Window, "Restore the previous source of " + backup.RelativePath + "? Current content must still match the recorded operation.", "Restore source", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
                Folder().Restore(backup); RefreshFiles(); Text("FilesStatus", "Previous source restored. Recompile in NinjaTrader.");
            }
            catch (Exception e) { FeatureError(e); }
        }
    }
}
