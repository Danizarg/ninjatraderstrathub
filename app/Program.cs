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
    public sealed partial class DesktopWindow
    {
        public Window Window { get; private set; }
        private readonly string settingsFile = System.IO.Path.Combine(DesktopDataRoot(), "desktop-settings.json");
        private static string DesktopDataRoot() { return Environment.GetEnvironmentVariable("ATL_DESKTOP_DATA") ?? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AdaptiveTradingLab"); }
        private WorkspaceSettings settings;
        private bool busy;
        private int executionVersion;
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
                    saved.ExportsRoot = ValidPath(saved.ExportsRoot); saved.NinjaTraderHome = LabData.ResolveNinjaTraderHome(ValidPath(saved.NinjaTraderHome));
                    settings = saved;
                }
            }
            catch (Exception e) { startupWarning = "Saved folders could not be read; using defaults. " + e.Message; }
            Get<TextBox>("NtPathBox").Text = settings.NinjaTraderHome;
            Get<TextBox>("ExportPathBox").Text = settings.ExportsRoot;
            Get<Button>("RefreshButton").Click += async (s, e) => await RefreshAll();
            Get<Button>("InstallButton").Click += async (s, e) => await Install();
            Get<Button>("SaveSettingsButton").Click += async (s, e) => { try { SaveFolders(); await RefreshAll(); Log("Workspace folders saved."); } catch (Exception ex) { Error(ex); } };
            Get<Button>("BrowseNtButton").Click += (s, e) => Browse("NtPathBox");
            Get<Button>("BrowseExportsButton").Click += (s, e) => Browse("ExportPathBox");
            Get<Button>("OpenNtButton").Click += (s, e) => { try { OpenFolder(System.IO.Path.Combine(ValidPath(Get<TextBox>("NtPathBox").Text), "bin", "Custom", "Strategies")); } catch (Exception ex) { Error(ex); } };
            Get<Button>("OpenExportsButton").Click += (s, e) => OpenFolder(settings.ExportsRoot);
            Get<ComboBox>("ExecutionPicker").SelectionChanged += async (s, e) => await LoadSelectedExecutions();
            InitializeAdaptation();
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
            var candidate = new WorkspaceSettings { ExportsRoot = ValidPath(Get<TextBox>("ExportPathBox").Text), NinjaTraderHome = LabData.ResolveNinjaTraderHome(ValidPath(Get<TextBox>("NtPathBox").Text)) };
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
            foreach (string name in new[] { "RefreshButton", "InstallButton", "SaveSettingsButton", "BrowseNtButton", "BrowseExportsButton" }) Get<Button>(name).IsEnabled = !value;
            Get<ProgressBar>("BusyBar").Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            Text("StatusText", status);
        }
        private async Task Install()
        {
            if (busy) return;
            SetBusy(true, "Installing NinjaTrader strategy…");
            try
            {
                GuardStrategyWrite(true);
                string ntFolder = LabData.ResolveNinjaTraderHome(ValidPath(Get<TextBox>("NtPathBox").Text));
                if (!File.Exists(System.IO.Path.Combine(ntFolder, "bin", "Custom", "NinjaTrader.Custom.csproj")))
                    throw new DirectoryNotFoundException("Select your NinjaTrader user folder, normally Documents\\NinjaTrader 8. A chart template or the Program Files installation folder cannot be used here.");
                Get<TextBox>("NtPathBox").Text = ntFolder;
                SaveFolders();
                var folder = Folder();
                var existing = folder.List().FirstOrDefault(f => f.RelativePath.Equals("ATL_EMA_Trend.cs", StringComparison.OrdinalIgnoreCase));
                if (existing != null && folder.Read(existing) == templateSource) { Text("StatusText", "Strategy is already installed."); return; }
                if (existing != null && MessageBox.Show(Window, "Back up and replace ATL_EMA_Trend.cs with the bundled source?", "Update strategy", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
                folder.Save("ATL_EMA_Trend.cs", templateSource, existing == null ? null : existing.Hash);
                CheckNinjaTrader();
                await RefreshNativeReports();
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
        private async Task RefreshAll()
        {
            try
            {
                CheckNinjaTrader();
                await RefreshNativeReports();
                var executions = Get<ComboBox>("ExecutionPicker");
                var old = executions.SelectedItem as FileChoice;
                string exports = settings.ExportsRoot;
                var files = await Task.Run(() => LabData.ExecutionFiles(exports));
                executions.ItemsSource = files;
                executions.SelectedItem = (old == null ? null : files.FirstOrDefault(f => f.Path == old.Path)) ?? files.FirstOrDefault();
                await LoadSelectedExecutions();
                if (!busy) Text("StatusText", "Updated " + DateTime.Now.ToString("HH:mm:ss") + " · Saved NinjaTrader backtests and execution files.");
            }
            catch (Exception e) { Error(e); }
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
    }
}
