using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using Forms = System.Windows.Forms;

namespace AdaptiveTradingLab.Desktop
{
    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            try
            {
                var app = new Application();
                app.DispatcherUnhandledException += (s, e) => {
                    MessageBox.Show(e.Exception.Message, "Adaptive Trading Lab", MessageBoxButton.OK, MessageBoxImage.Error);
                    e.Handled = true;
                };
                var desktop = new DesktopWindow();
                app.Run(desktop.Window);
            }
            catch (Exception e) { MessageBox.Show(e.ToString(), "Unable to open Adaptive Trading Lab", MessageBoxButton.OK, MessageBoxImage.Error); Environment.ExitCode = 1; }
        }
    }
    public sealed class DesktopWindow
    {
        public Window Window { get; private set; }
        private readonly string repo = AppDomain.CurrentDomain.BaseDirectory;
        private readonly string settingsFile = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AdaptiveTradingLab", "desktop-settings.json");
        private readonly CultureInfo usd = CultureInfo.GetCultureInfo("en-US");
        private WorkspaceSettings settings;
        private RunResult current = new RunResult();
        private bool busy;
        private int runVersion, executionVersion;
        private string startupWarning;

        private T Get<T>(string name) where T : FrameworkElement { return (T)Window.FindName(name); }
        private void Text(string name, string value) { Get<TextBlock>(name).Text = value; }
        private void Log(string value)
        {
            var log = Get<TextBox>("LogBox");
            log.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + value + Environment.NewLine);
            log.ScrollToEnd();
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(settingsFile));
                File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(settingsFile), "desktop.log"), DateTime.Now.ToString("o") + " " + value + Environment.NewLine);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        private void Error(Exception e)
        {
            Text("StatusText", "Action failed — see activity log."); Log("ERROR: " + e.ToString());
            MessageBox.Show(Window, e.Message, "Adaptive Trading Lab", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        public DesktopWindow()
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("MainWindow.xaml")) Window = (Window)XamlReader.Load(stream);
            settings = WorkspaceSettings.Defaults();
            try
            {
                if (File.Exists(settingsFile))
                {
                    var saved = new JavaScriptSerializer().Deserialize<WorkspaceSettings>(File.ReadAllText(settingsFile));
                    if (saved == null) throw new InvalidDataException("Empty settings file.");
                    saved.OutputRoot = ValidPath(saved.OutputRoot); saved.ExportsRoot = ValidPath(saved.ExportsRoot); saved.NinjaTraderHome = LabData.ResolveNinjaTraderHome(ValidPath(saved.NinjaTraderHome));
                    settings = saved;
                }
            }
            catch (Exception e) { startupWarning = "Saved folders could not be read; using defaults. " + e.Message; }
            Get<TextBox>("OutputPathBox").Text = settings.OutputRoot;
            Get<TextBox>("NtPathBox").Text = settings.NinjaTraderHome;
            Get<TextBox>("ExportPathBox").Text = settings.ExportsRoot;
            Get<Button>("RunButton").Click += async (s, e) => await RunDemo();
            Get<Button>("RefreshButton").Click += async (s, e) => await RefreshAll();
            Get<Button>("InstallButton").Click += async (s, e) => await Install();
            Get<Button>("SaveSettingsButton").Click += async (s, e) => { try { SaveFolders(); await RefreshAll(); Log("Workspace folders saved."); } catch (Exception ex) { Error(ex); } };
            Get<Button>("BrowseOutputButton").Click += (s, e) => Browse("OutputPathBox");
            Get<Button>("BrowseNtButton").Click += (s, e) => Browse("NtPathBox");
            Get<Button>("BrowseExportsButton").Click += (s, e) => Browse("ExportPathBox");
            Get<Button>("OpenRunButton").Click += (s, e) => { var selected = Get<ComboBox>("RunPicker").SelectedItem as FileChoice; if (selected != null) OpenFolder(selected.Path); };
            Get<Button>("OpenNtButton").Click += (s, e) => { try { OpenFolder(System.IO.Path.Combine(ValidPath(Get<TextBox>("NtPathBox").Text), "bin", "Custom", "Strategies")); } catch (Exception ex) { Error(ex); } };
            Get<Button>("OpenExportsButton").Click += (s, e) => OpenFolder(settings.ExportsRoot);
            Get<ComboBox>("RunPicker").SelectionChanged += async (s, e) => await LoadSelectedRun();
            Get<ComboBox>("ExecutionPicker").SelectionChanged += async (s, e) => await LoadSelectedExecutions();
            Get<Canvas>("EquityCanvas").SizeChanged += (s, e) => DrawEquity();
            Window.Loaded += async (s, e) => { Log("Desktop ready. No orders or account commands are sent by this app."); if (startupWarning != null) Log(startupWarning); await RefreshAll(); };
            Window.Closing += (s, e) => { if (busy) { e.Cancel = true; Text("StatusText", "Please wait for the current operation to finish before closing."); } };
        }
        private static string ValidPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !System.IO.Path.IsPathRooted(path.Trim())) throw new ArgumentException("Choose an absolute folder path, for example C:\\Trading Lab Data.");
            string full = System.IO.Path.GetFullPath(path.Trim());
            if (File.Exists(full)) throw new ArgumentException("Expected a folder but found a file: " + full);
            return full;
        }
        private void SaveFolders()
        {
            var candidate = new WorkspaceSettings { OutputRoot = ValidPath(Get<TextBox>("OutputPathBox").Text), ExportsRoot = ValidPath(Get<TextBox>("ExportPathBox").Text), NinjaTraderHome = LabData.ResolveNinjaTraderHome(ValidPath(Get<TextBox>("NtPathBox").Text)) };
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(settingsFile));
            string temp = settingsFile + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temp, new JavaScriptSerializer().Serialize(candidate));
            try { if (File.Exists(settingsFile)) File.Replace(temp, settingsFile, null); else File.Move(temp, settingsFile); }
            finally { if (File.Exists(temp)) File.Delete(temp); }
            settings = candidate;
            Get<TextBox>("NtPathBox").Text = candidate.NinjaTraderHome;
        }
        private void Browse(string control)
        {
            using (var dialog = new Forms.FolderBrowserDialog())
            {
                dialog.Description = "Select a workspace folder";
                dialog.SelectedPath = Get<TextBox>(control).Text;
                if (dialog.ShowDialog(new WindowOwner(Window)) == Forms.DialogResult.OK)
                    Get<TextBox>(control).Text = control == "NtPathBox" ? LabData.ResolveNinjaTraderHome(dialog.SelectedPath) : dialog.SelectedPath;
            }
        }
        private sealed class WindowOwner : Forms.IWin32Window
        {
            public IntPtr Handle { get; private set; }
            public WindowOwner(Window w) { Handle = new System.Windows.Interop.WindowInteropHelper(w).Handle; }
        }
        private void OpenFolder(string path)
        {
            try
            {
                if (!Directory.Exists(path)) throw new DirectoryNotFoundException("This folder does not exist yet: " + path);
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true, Verb = "open" });
            }
            catch (Exception e) { Error(e); }
        }
        private void SetBusy(bool value, string status)
        {
            busy = value;
            foreach (string name in new[] { "RunButton", "RefreshButton", "InstallButton", "SaveSettingsButton", "BrowseOutputButton", "BrowseNtButton", "BrowseExportsButton" }) Get<Button>(name).IsEnabled = !value;
            Get<ProgressBar>("BusyBar").Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            Text("StatusText", status);
        }
        private async Task<string> RunScript(string script, params string[] arguments)
        {
            string path = System.IO.Path.Combine(repo, "scripts", script);
            if (!File.Exists(path)) throw new FileNotFoundException("The app must stay with its scripts and vendor folders. Extract the complete download.", path);
            var args = new List<string> { "-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", path };
            args.AddRange(arguments);
            string powershell = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
            return await Task.Run(async () => {
                using (var process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo { FileName = powershell, Arguments = string.Join(" ", args.Select(LabData.QuoteArgument)),
                        WorkingDirectory = repo, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                    // A launch from PowerShell 7 can carry its incompatible module paths into
                    // Windows PowerShell. These scripts use the built-in Windows modules only.
                    process.StartInfo.EnvironmentVariables["PSModulePath"] = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(powershell), "Modules")
                        + ";" + System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WindowsPowerShell", "Modules");
                    process.Start();
                    Task<string> output = process.StandardOutput.ReadToEndAsync();
                    Task<string> error = process.StandardError.ReadToEndAsync();
                    await Task.WhenAll(output, error);
                    process.WaitForExit();
                    string text = output.Result + (string.IsNullOrWhiteSpace(error.Result) ? "" : Environment.NewLine + error.Result);
                    if (process.ExitCode != 0) throw new InvalidOperationException("Operation failed (exit " + process.ExitCode + ").\n" + text);
                    return text;
                }
            });
        }
        private async Task RunDemo()
        {
            if (busy) return;
            SetBusy(true, "Running synthetic demo… First launch also prepares the bundled runtime.");
            try
            {
                Log("Starting synthetic demo.");
                Log(await RunScript("Start-Lab.ps1", "-OutputRoot", settings.OutputRoot));
                await RefreshAll(true);
                Get<TabControl>("Pages").SelectedIndex = 0;
                Text("StatusText", "Demo complete. Synthetic results are ready.");
            }
            catch (Exception e) { Error(e); }
            finally { SetBusy(false, Get<TextBlock>("StatusText").Text); }
        }
        private async Task Install()
        {
            if (busy) return;
            SetBusy(true, "Installing NinjaTrader strategy…");
            try
            {
                string ntFolder = LabData.ResolveNinjaTraderHome(ValidPath(Get<TextBox>("NtPathBox").Text));
                if (!File.Exists(System.IO.Path.Combine(ntFolder, "bin", "Custom", "NinjaTrader.Custom.csproj")))
                    throw new DirectoryNotFoundException("Select your NinjaTrader user folder, normally Documents\\NinjaTrader 8. A chart template or the Program Files installation folder cannot be used here.");
                Get<TextBox>("NtPathBox").Text = ntFolder;
                SaveFolders();
                string source = System.IO.Path.Combine(repo, "ninjatrader", "Strategies", "ATL_EMA_Trend.cs");
                string target = System.IO.Path.Combine(settings.NinjaTraderHome, "bin", "Custom", "Strategies", "ATL_EMA_Trend.cs");
                bool replace = File.Exists(target) && File.ReadAllText(source) != File.ReadAllText(target);
                if (replace && MessageBox.Show(Window, "An existing ATL_EMA_Trend.cs differs. Back it up and replace it with this version? Close any editor tab for this file first.", "Update strategy", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) { Text("StatusText", "Strategy update cancelled."); return; }
                var args = new List<string> { "-NinjaTraderHome", settings.NinjaTraderHome };
                if (replace) args.Add("-ReplaceExisting");
                Log(await RunScript("Install-NinjaTrader.ps1", args.ToArray()));
                CheckNinjaTrader();
                Text("StatusText", "Strategy installed. In NinjaTrader, open NinjaScript Editor and press F5.");
            }
            catch (Exception e) { Error(e); }
            finally { SetBusy(false, Get<TextBlock>("StatusText").Text); }
        }
        private void CheckNinjaTrader()
        {
            bool found = File.Exists(System.IO.Path.Combine(settings.NinjaTraderHome, "bin", "Custom", "NinjaTrader.Custom.csproj"));
            bool installed = File.Exists(System.IO.Path.Combine(settings.NinjaTraderHome, "bin", "Custom", "Strategies", "ATL_EMA_Trend.cs"));
            bool running;
            var processes = Process.GetProcessesByName("NinjaTrader");
            try { running = processes.Length > 0; } finally { foreach (var process in processes) process.Dispose(); }
            Text("NtBadge", found ? (running ? "●  NinjaTrader detected · running" : "●  NinjaTrader detected · closed") : "○  NinjaTrader folder not found");
            Text("InstallStatus", !found ? "Choose your NinjaTrader Documents folder below." : installed ? "Strategy source installed. Compilation status is checked inside NinjaTrader." : "NinjaTrader found. The ATL strategy has not been installed here yet.");
        }
        private async Task RefreshAll(bool newest = false)
        {
            try
            {
                CheckNinjaTrader();
                var runs = Get<ComboBox>("RunPicker"); var executions = Get<ComboBox>("ExecutionPicker");
                var oldRun = runs.SelectedItem as FileChoice; var oldExecution = executions.SelectedItem as FileChoice;
                string outputRoot = settings.OutputRoot, exportsRoot = settings.ExportsRoot;
                var runList = await Task.Run(() => LabData.Runs(outputRoot));
                var exportList = await Task.Run(() => LabData.ExecutionFiles(exportsRoot));
                runs.ItemsSource = runList; executions.ItemsSource = exportList;
                runs.SelectedItem = (!newest && oldRun != null ? runList.FirstOrDefault(r => r.Path == oldRun.Path) : null) ?? runList.FirstOrDefault();
                executions.SelectedItem = (oldExecution != null ? exportList.FirstOrDefault(r => r.Path == oldExecution.Path) : null) ?? exportList.FirstOrDefault();
                // Explicit loads also refresh a currently selected file that has grown on disk.
                await LoadSelectedRun(); await LoadSelectedExecutions();
                if (!busy) Text("StatusText", "Updated " + DateTime.Now.ToString("HH:mm:ss") + " · Local files only. Click Refresh after new executions.");
            }
            catch (Exception e) { Error(e); }
        }
        private async Task LoadSelectedRun()
        {
            int version = ++runVersion;
            var selected = Get<ComboBox>("RunPicker").SelectedItem as FileChoice;
            Get<Button>("OpenRunButton").IsEnabled = selected != null;
            try
            {
                var result = selected == null ? new RunResult() : await Task.Run(() => LabData.LoadRun(selected.Path));
                string learning = "No run selected";
                if (selected != null)
                {
                    learning = "Status not recorded for this older run";
                    string metadata = System.IO.Path.Combine(selected.Path, "desktop-run.json");
                    if (File.Exists(metadata))
                    {
                        try { var info = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(metadata)); learning = "Demo: " + Convert.ToString(info["learningStatus"]); }
                        catch { learning = "Run status could not be read"; }
                    }
                }
                if (version != runVersion) return;
                current = result;
                Text("PnlText", selected == null ? "—" : result.Net.ToString("C2", usd));
                Text("CountText", selected == null ? "—" : result.Trades.Count.ToString("N0"));
                Text("WinText", selected == null ? "—" : result.WinRate.ToString("0.0") + "%");
                Text("DrawdownText", selected == null ? "—" : result.Drawdown.ToString("C2", usd));
                Text("LearningText", learning);
                Get<DataGrid>("TradesGrid").ItemsSource = result.Trades;
                Get<TextBlock>("ChartEmpty").Visibility = result.Trades.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                if (result.Skipped > 0) { Log("Skipped " + result.Skipped + " unreadable trade records in " + selected.Path); Text("LearningText", learning + " · " + result.Skipped + " records skipped; totals are partial"); }
                DrawEquity();
            }
            catch (Exception e)
            {
                if (version == runVersion)
                {
                    current = new RunResult();
                    Get<DataGrid>("TradesGrid").ItemsSource = null;
                    foreach (string name in new[] { "PnlText", "CountText", "WinText", "DrawdownText" }) Text(name, "—");
                    Text("LearningText", "Unable to read this run");
                    DrawEquity(); Error(e);
                }
            }
        }
        private async Task LoadSelectedExecutions()
        {
            int version = ++executionVersion;
            var selected = Get<ComboBox>("ExecutionPicker").SelectedItem as FileChoice;
            try
            {
                var rows = selected == null ? new List<ExecutionRow>() : await Task.Run(() => LabData.LoadExecutions(selected.Path));
                if (version != executionVersion) return;
                Get<DataGrid>("ExecutionsGrid").ItemsSource = rows;
                Text("ExecutionCount", rows.Count.ToString("N0") + " fills");
                Get<TextBlock>("ExportsEmpty").Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception e) { if (version == executionVersion) { Get<DataGrid>("ExecutionsGrid").ItemsSource = null; Text("ExecutionCount", "Unable to read file"); Error(e); } }
        }
        private void DrawEquity()
        {
            var canvas = Get<Canvas>("EquityCanvas"); canvas.Children.Clear();
            if (current.Trades.Count == 0 || canvas.ActualWidth < 100 || canvas.ActualHeight < 50) return;
            double left = 78, right = canvas.ActualWidth - 12, top = 10, bottom = canvas.ActualHeight - 28;
            double min = current.Equity.Min(), max = current.Equity.Max();
            if (max - min < 0.01) { min -= 1; max += 1; }
            double padding = (max - min) * 0.08; min -= padding; max += padding;
            var muted = (Brush)new BrushConverter().ConvertFromString("#8294AF");
            for (int i = 0; i < 4; i++)
            {
                double y = top + (bottom - top) * i / 3;
                canvas.Children.Add(new Line { X1 = left, X2 = right, Y1 = y, Y2 = y, Stroke = (Brush)new BrushConverter().ConvertFromString("#2A384C"), StrokeThickness = 1 });
                var label = new TextBlock { Text = (max - (max - min) * i / 3).ToString("C0", usd), Foreground = muted, FontSize = 11 };
                Canvas.SetTop(label, y - 8); canvas.Children.Add(label);
            }
            var line = new Polyline { Stroke = (Brush)new BrushConverter().ConvertFromString("#62E2BD"), StrokeThickness = 2.5 };
            var area = new Polygon { Fill = (Brush)new BrushConverter().ConvertFromString("#183DD9AD") };
            area.Points.Add(new Point(left, bottom));
            for (int i = 0; i < current.Equity.Count; i++)
            {
                var point = new Point(left + (right - left) * i / (current.Equity.Count - 1), bottom - (current.Equity[i] - min) / (max - min) * (bottom - top));
                line.Points.Add(point); area.Points.Add(point);
            }
            area.Points.Add(new Point(right, bottom)); canvas.Children.Add(area); canvas.Children.Add(line);
            var start = new TextBlock { Text = "START", FontSize = 11, Foreground = muted }; Canvas.SetLeft(start, left); Canvas.SetTop(start, bottom + 10); canvas.Children.Add(start);
            var end = new TextBlock { Text = current.Trades.Count + " TRADES", FontSize = 11, Foreground = muted }; Canvas.SetRight(end, 12); Canvas.SetTop(end, bottom + 10); canvas.Children.Add(end);
        }
    }
}
