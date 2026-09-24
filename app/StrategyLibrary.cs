using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace AdaptiveTradingLab.Desktop
{
    public sealed class StrategyVersion
    {
        public string Id { get; set; }
        public string ClassName { get; set; }
        public DateTime CreatedUtc { get; set; }
        public string BaselineReportHash { get; set; }
        public string BaselineSourceHash { get; set; }
        public string Description { get; set; }
        public string SourceHash { get; set; }
        public StrategyParameters Parameters { get; set; }
        public string State { get; set; }
        public string[] EvidenceReportHashes { get; set; }
        public string Label { get { return ClassName + " · " + State; } }
    }
    public sealed class StrategyFile
    {
        public string RelativePath { get; set; }
        public string Hash { get; set; }
        public string Label { get { return RelativePath; } }
    }
    public sealed class FileBackup
    {
        public string Id { get; set; }
        public string RelativePath { get; set; }
        public string BeforeHash { get; set; }
        public string AfterHash { get; set; }
        public string Reason { get; set; }
        public DateTime CreatedUtc { get; set; }
        public string Label { get { return CreatedUtc.ToLocalTime().ToString("dd MMM HH:mm") + " · " + RelativePath + " · " + Reason; } }
    }
    public sealed class StrategyLibrary
    {
        public readonly string Root;
        private readonly JavaScriptSerializer serializer = new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 };
        public StrategyLibrary(string root) { Root = Path.GetFullPath(root); SafeAncestors(Root); }
        internal static void SafeAncestors(string path)
        {
            for (string current = Path.GetFullPath(path); current != null; current = Path.GetDirectoryName(current))
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Linked/junction paths are not supported for strategy writes: " + current);
        }
        private string VersionFolder(string id)
        {
            if (!Regex.IsMatch(id ?? "", "^[a-f0-9]{32}$")) throw new InvalidDataException("Invalid version ID.");
            string folder = Path.Combine(Root, "versions", id); SafeAncestors(folder); return folder;
        }
        public static string Generate(string template, string name, StrategyParameters p)
        {
            if (!Regex.IsMatch(name ?? "", "^ATL20_[A-Za-z0-9_]{1,80}$")) throw new InvalidDataException("Invalid generated strategy name.");
            p.Validate();
            if (!template.Contains("public class ATL_EMA_Trend : Strategy")) throw new InvalidDataException("Unexpected base strategy template.");
            string source = template.Replace("ATL_EMA_Trend", name);
            var values = new Dictionary<string, double> { {"FastPeriod",p.Fast}, {"SlowPeriod",p.Slow}, {"FilterPeriod",p.Filter}, {"MinimumAdx",p.Adx}, {"StopAtrMultiplier",p.Stop}, {"TargetAtrMultiplier",p.Target} };
            foreach (var pair in values)
            {
                string pattern = @"\b" + pair.Key + @" = [0-9.]+;";
                if (Regex.Matches(source, pattern).Count != 1) throw new InvalidDataException("Cannot locate a unique default for " + pair.Key);
                source = Regex.Replace(source, pattern, pair.Key + " = " + pair.Value.ToString("R", CultureInfo.InvariantCulture) + ";");
            }
            string configure = "else if (State == State.Configure)\n            {";
            source = source.Replace("\r\n", "\n");
            if (!source.Contains(configure)) throw new InvalidDataException("Missing Configure block.");
            source = source.Replace(configure, configure + "\n                if (BarsPeriod.BarsPeriodType != NinjaTrader.Data.BarsPeriodType.Second || BarsPeriod.Value != 20)\n                    throw new ArgumentException(\"This candidate requires 20-second bars.\");");
            if (!source.Contains("EnableRealtimeEntries = false;")) throw new InvalidDataException("Template must default realtime entries off.");
            return "// Generated ATL 20-second candidate. Untested until compared in NinjaTrader.\n" + source;
        }
        public StrategyVersion Create(string template, NativeBacktest baseline, StrategyParameters p, string description)
        {
            if (!baseline.IsTwentySeconds || baseline.Parameters == null) throw new InvalidDataException("A 20-second ATL baseline with parameters is required.");
            AdaptationEngine.Propose(baseline);
            bool known = baseline.Strategy == "ATL_EMA_Trend" && baseline.SourceHash == NativeReports.Hash(template);
            if (!known) known = ListVersions().Any(v => v.ClassName == baseline.Strategy && v.SourceHash == baseline.SourceHash);
            if (!known) throw new InvalidDataException("The baseline source snapshot is missing or differs from the supported strategy. Use the original saved XML and adjacent NinjaScript .cs snapshot in Strategy Analyzer logs; arbitrary edited strategies require manual review.");
            string id = Guid.NewGuid().ToString("N");
            var version = new StrategyVersion { Id = id, CreatedUtc = DateTime.UtcNow, ClassName = "ATL20_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + "_" + id.Substring(0, 8),
                Parameters = p.Copy(), BaselineReportHash = baseline.ReportHash, BaselineSourceHash = baseline.SourceHash, Description = description, State = "Untested" };
            string code = Generate(template, version.ClassName, p); version.SourceHash = NativeReports.Hash(code);
            string folder = VersionFolder(id); Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, version.ClassName + ".cs"), code, new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(folder, "version.json"), serializer.Serialize(version), new UTF8Encoding(false));
            return version;
        }
        public List<StrategyVersion> ListVersions()
        {
            string folder = Path.Combine(Root, "versions"); SafeAncestors(folder);
            if (!Directory.Exists(folder)) return new List<StrategyVersion>();
            var result = new List<StrategyVersion>();
            foreach (string dir in Directory.EnumerateDirectories(folder))
            {
                string file = Path.Combine(VersionFolder(Path.GetFileName(dir)), "version.json");
                if (!File.Exists(file)) continue;
                var version = serializer.Deserialize<StrategyVersion>(File.ReadAllText(file));
                if (version == null || version.Id != Path.GetFileName(dir)) throw new InvalidDataException("Invalid version manifest.");
                ReadSource(version); result.Add(version);
            }
            return result.OrderByDescending(v => v.CreatedUtc).ToList();
        }
        public string ReadSource(StrategyVersion version)
        {
            if (!Regex.IsMatch(version.ClassName ?? "", "^ATL20_[A-Za-z0-9_]{1,80}$")) throw new InvalidDataException("Invalid class name.");
            string path = Path.Combine(VersionFolder(version.Id), version.ClassName + ".cs"); SafeAncestors(path);
            string source = File.ReadAllText(path);
            if (NativeReports.Hash(source) != version.SourceHash) throw new InvalidDataException("Version source has changed outside the library; create a new version instead.");
            return source;
        }
        public void Recommend(StrategyVersion version, NativeBacktest a, NativeBacktest b, NativeBacktest ah, NativeBacktest bh)
        {
            ReadSource(version);
            if (version.BaselineReportHash != a.ReportHash || version.BaselineSourceHash != a.SourceHash || b.Strategy != version.ClassName || bh.Strategy != version.ClassName || b.SourceHash != version.SourceHash || bh.SourceHash != version.SourceHash ||
                b.Parameters == null || bh.Parameters == null || b.Parameters.Key != version.Parameters.Key || bh.Parameters.Key != version.Parameters.Key ||
                !AdaptationEngine.Validated(a, b, ah, bh)) throw new InvalidDataException("This version does not pass cost, sample-size, same-period, source-identity and later-validation checks.");
            version.State = "Recommended by recorded tests";
            version.EvidenceReportHashes = new[] { a.ReportHash, b.ReportHash, ah.ReportHash, bh.ReportHash };
            string file = Path.Combine(VersionFolder(version.Id), "version.json");
            AtomicWrite(file, serializer.Serialize(version));
        }
        internal static void AtomicWrite(string path, string text)
        {
            SafeAncestors(path); string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temp, text, new UTF8Encoding(false));
            try { if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path); }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
    // All mutations are explicit UI actions. Backups live outside NinjaTrader's compiled source tree.
    public sealed class StrategyFolder
    {
        private readonly string root, backups;
        private readonly JavaScriptSerializer json = new JavaScriptSerializer();
        public StrategyFolder(string ninjaHome, string backupRoot)
        {
            string home = LabData.ResolveNinjaTraderHome(ninjaHome);
            if (!File.Exists(Path.Combine(home, "bin", "Custom", "NinjaTrader.Custom.csproj"))) throw new DirectoryNotFoundException("Select a valid NinjaTrader Documents folder.");
            root = Path.GetFullPath(Path.Combine(home, "bin", "Custom", "Strategies"));
            backups = Path.GetFullPath(backupRoot); StrategyLibrary.SafeAncestors(root); StrategyLibrary.SafeAncestors(backups);
            if (backups.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || backups.Equals(root, StringComparison.OrdinalIgnoreCase)) throw new IOException("Backups must be outside the strategy folder.");
        }
        private string Target(string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Split('\\','/').Any(p => p == ".." || p == ".") || !relative.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Select a .cs file inside the strategy folder.");
            string target = Path.GetFullPath(Path.Combine(root, relative));
            if (!target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new IOException("Strategy path escapes the selected folder.");
            StrategyLibrary.SafeAncestors(target); return target;
        }
        public List<StrategyFile> List()
        {
            var items = new List<StrategyFile>(); if (!Directory.Exists(root)) return items;
            Collect(root, items); return items.OrderBy(f => f.RelativePath).ToList();
        }
        private void Collect(string folder, List<StrategyFile> items)
        {
            StrategyLibrary.SafeAncestors(folder);
            foreach (string file in Directory.EnumerateFiles(folder, "*.cs"))
            {
                StrategyLibrary.SafeAncestors(file); items.Add(new StrategyFile { RelativePath = file.Substring(root.Length + 1), Hash = NativeReports.Hash(File.ReadAllText(file)) });
            }
            foreach (string child in Directory.EnumerateDirectories(folder)) if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0) Collect(child, items);
        }
        public string Read(StrategyFile file) { return File.ReadAllText(Target(file.RelativePath)); }
        private void Check(string path, string expected)
        {
            string actual = File.Exists(path) ? NativeReports.Hash(File.ReadAllText(path)) : null;
            if (actual != expected) throw new IOException("The file changed since selection. Refresh and review before continuing.");
        }
        private FileBackup Backup(string relative, string target, string after, string reason)
        {
            string id = Guid.NewGuid().ToString("N"); string folder = Path.Combine(backups, id); StrategyLibrary.SafeAncestors(folder); Directory.CreateDirectory(folder);
            string before = File.Exists(target) ? File.ReadAllText(target) : null;
            var record = new FileBackup { Id = id, RelativePath = relative, BeforeHash = before == null ? null : NativeReports.Hash(before), AfterHash = after == null ? null : NativeReports.Hash(after), Reason = reason, CreatedUtc = DateTime.UtcNow };
            if (before != null) File.WriteAllText(Path.Combine(folder, "before.txt"), before, new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(folder, "record.json"), json.Serialize(record)); return record;
        }
        public void Save(string relative, string text, string expected)
        {
            if (string.IsNullOrWhiteSpace(text)) throw new InvalidDataException("Strategy source cannot be empty.");
            string target = Target(relative); Check(target, expected);
            Backup(relative, target, text, expected == null ? "Install" : "Edit");
            Directory.CreateDirectory(Path.GetDirectoryName(target)); StrategyLibrary.AtomicWrite(target, text);
        }
        public void Archive(StrategyFile file)
        {
            string target = Target(file.RelativePath); Check(target, file.Hash); Backup(file.RelativePath, target, null, "Archive"); File.Delete(target);
        }
        public List<FileBackup> Backups()
        {
            StrategyLibrary.SafeAncestors(backups); if (!Directory.Exists(backups)) return new List<FileBackup>();
            return Directory.EnumerateDirectories(backups).Select(dir => {
                StrategyLibrary.SafeAncestors(dir); string file = Path.Combine(dir, "record.json");
                var entry = json.Deserialize<FileBackup>(File.ReadAllText(file));
                if (entry == null || entry.Id != Path.GetFileName(dir)) throw new InvalidDataException("Invalid backup record."); return entry;
            }).OrderByDescending(x => x.CreatedUtc).ToList();
        }
        public void Restore(FileBackup record)
        {
            if (!Regex.IsMatch(record.Id ?? "", "^[a-f0-9]{32}$") || record.BeforeHash == null) throw new InvalidDataException("Select an Edit or Archive backup with prior source to restore.");
            string target = Target(record.RelativePath); Check(target, record.AfterHash);
            string file = Path.Combine(backups, record.Id, "before.txt"); StrategyLibrary.SafeAncestors(file); string content = File.ReadAllText(file);
            if (NativeReports.Hash(content) != record.BeforeHash) throw new InvalidDataException("Backup content no longer matches its checksum.");
            Backup(record.RelativePath, target, content, "Restore"); Directory.CreateDirectory(Path.GetDirectoryName(target)); StrategyLibrary.AtomicWrite(target, content);
        }
    }
}
