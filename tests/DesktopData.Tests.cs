using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using System.Globalization;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using AdaptiveTradingLab.Desktop;
public static class Tests {
 static int count;
 static void Check(bool ok,string text) { if(!ok) throw new Exception(text); count++; }
 static void Reject(Action action,string text) { bool rejected=false;try {action();}catch {rejected=true;}Check(rejected,text); }
 static StrategyParameters P(){return new StrategyParameters{Fast=9,Slow=21,Filter=14,Adx=20,Stop=1.5,Target=2.5};}
 static NativeBacktest R(bool better,bool later){return new NativeBacktest{Strategy=better?"ATL20_test":"ATL_EMA_Trend",Instrument="TEST",Currency="USD",Action="Backtest",BarsType=3,BarsValue=20,From=new DateTime(2025,later?4:1,1),To=new DateTime(2025,later?6:3,28),CostsIncluded=true,Commission=40,Slippage=1,Trades=100,Net=better?200:100,Drawdown=better?40:80,AverageTrade=better?2:1,WinRate=50,TradesPerDay=2,SettingsKey="matching",Parameters=P(),SourceHash=better?"changed":"original",ReportHash=Guid.NewGuid().ToString()};}
 static string Fixture(string dir) {
 var root=new XElement("StrategyAnalyzerGridEntry",
 new XElement("StrategyName","ATL_EMA_Trend"),new XElement("Instrument","TEST"),new XElement("Date","2025-07-01"),new XElement("From","2025-01-01"),new XElement("To","2025-03-28"),new XElement("IncludeCommission",true),new XElement("Slippage",1),new XElement("Action","Backtest"),
 new XElement("DataSeries",new XElement("BarsPeriodTypeSerialize",3),new XElement("Value",20)),
 new XElement("SummaryPerformances",new XElement("PerformanceUnit","Currency"),new XElement("Denomination","UsDollar"),new XElement("SummaryPerformancesSerialize","Commission;40|TotalNetProfit;100|MaxDrawdown;-80|TotalNumTrades;100|PercentProfitable;0.5|AverageTrade;1|AverageNumTradesPerDay;2|ProfitFactor;1.2")),
 new XElement("StrategyTemplate","<Strategy><DefaultQuantity>1</DefaultQuantity><Calculate>OnBarClose</Calculate></Strategy>"));
 foreach(string key in new[]{"EntryHandling","EntriesPerDirection","ExitOnSessionClose","ExitOnSessionCloseSeconds","FillType","FillTypeType","FillTypeValue","FillLimitOrdersOnTouch","IsBreakAtEod","IsTickReplay","MinBarsRequired","MaximumBarsLookBack","TradingHoursTemplate","SetOrderQuantity","StopTargetHandling"}) root.Add(new XElement(key,"1"));
 string[] names={"FastPeriod","SlowPeriod","FilterPeriod","MinimumAdx","StopAtrMultiplier","TargetAtrMultiplier"},values={"9","21","14","20","1.5","2.5"};
 var parameters=new XElement("Parameters");for(int i=0;i<names.Length;i++)parameters.Add(new XElement("ParameterWrapper",new XElement("Name",names[i]),new XElement("Value",values[i])));root.Add(parameters);
 string file=Path.Combine(dir,"fixture.xml");new XDocument(new XElement("StrategyAnalyzerLog",root)).Save(file);return file;
 }
 [STAThread] public static int Main(string[] args) {int exit=0;var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};app.Startup+=async(s,e)=>{try{await Run(args);Console.WriteLine("PASS: "+count+" checks.");}catch(Exception ex){Console.Error.WriteLine(ex);exit=1;}finally{app.Shutdown();}};app.Run();return exit;}
 static async Task Run(string[] args){
 string dir=Path.GetFullPath(args[0]),repo=Path.GetFullPath(args[1]);Directory.CreateDirectory(dir);
 Environment.SetEnvironmentVariable("ATL_DESKTOP_DATA",Path.Combine(dir,"preview"));Environment.SetEnvironmentVariable("ATL_PREVIEW","1");
 var fixture=NativeReports.Load(Fixture(dir));Check(fixture.IsTwentySeconds&&fixture.Trades==100&&fixture.Net==100&&fixture.Drawdown==80&&fixture.WinRate==50,"Fixture parser");
 var a=R(false,false);var b=R(true,false);var ah=R(false,true);var bh=R(true,true);
 Check(AdaptationEngine.Validated(a,b,ah,bh),"Matching validation");
 b.Slippage=0;Check(!AdaptationEngine.Passes(a,b),"Slippage mismatch");b.Slippage=1;
 b.Commission=0;Check(!AdaptationEngine.Passes(a,b),"Zero costs");b.Commission=40;
 b.Trades=49;Check(!AdaptationEngine.Passes(a,b),"Insufficient sample");b.Trades=100;
 b.Drawdown=81;Check(!AdaptationEngine.Passes(a,b),"Worse drawdown");b.Drawdown=40;
 bh.From=a.To;Check(!AdaptationEngine.Validated(a,b,ah,bh),"Overlapping validation");bh.From=ah.From;
 bh.SourceHash="different";Check(!AdaptationEngine.Validated(a,b,ah,bh),"Changed source");bh.SourceHash=b.SourceHash;
 ah.Parameters.Adx=30;Check(!AdaptationEngine.Validated(a,b,ah,bh),"Changed parameters");ah.Parameters=P();
 b.SettingsKey="different";Check(AdaptationEngine.Compare(a,b).StartsWith("NOT COMPARABLE"),"Mismatch comparison");b.SettingsKey=a.SettingsKey;
 a.Trades=0; var recovery=AdaptationEngine.Loosen(a);
 Check(recovery.Count==3 && recovery.All(x=>x.Parameters.Stop==a.Parameters.Stop && x.Parameters.Target==a.Parameters.Target),"Recovery preserves brackets");
 Check(recovery[0].Parameters.Adx==15 && recovery[1].Parameters.Adx==0 && recovery[2].Parameters.Fast<a.Parameters.Fast,"Recovery loosens entries");
 Check(recovery.All(x=>x.Explain(a).Contains("ZERO TRADES")),"Missing-data diagnosis absent");
 Reject(()=>AdaptationEngine.Propose(a),"Small sample performance proposal accepted");
 a.Parameters.Adx=0;a.Parameters.Fast=1;a.Parameters.Slow=2;Check(AdaptationEngine.Loosen(a).Count==0,"Minimum settings generated duplicates");a.Parameters=P();a.Trades=100;
 var proposals=AdaptationEngine.Propose(a);Check(proposals.Count==3&&proposals.All(p=>p.Explain(a).Contains("NOT YET KNOWN")),"Proposal honesty");
 a.BarsValue=60;Reject(()=>AdaptationEngine.Propose(a),"Other timeframe accepted");a.BarsValue=20;
 var invalid=P();invalid.Target=double.NaN;Reject(()=>invalid.Validate(),"NaN accepted");
 var library=new StrategyLibrary(Path.Combine(dir,"library"));string template=File.ReadAllText(Path.Combine(repo,"ninjatrader","Strategies","ATL_EMA_Trend.cs"));
 a.SourceHash=ah.SourceHash=NativeReports.Hash(template);
 a.Trades=0;
 var version=library.Create(template,a,proposals[0].Parameters,"Test candidate");string source=library.ReadSource(version); a.Trades=100;
 Check(source.Contains("MinimumAdx = 25;")&&source.Contains("BarsPeriod.Value != 20")&&source.Contains("EnableRealtimeEntries = false;"),"Generation invariants");
 Check(NativeReports.SourceIdentity(source.Replace(version.ClassName,version.ClassName+"_2026_09_24_0"),version.ClassName)==version.SourceHash,"Dated candidate alias");
 Check(NativeReports.SourceIdentity(source.Replace("MinimumAdx = 25;","MinimumAdx = 26;"),version.ClassName)!=version.SourceHash,"Semantic source changes hidden");
 File.WriteAllText(Path.Combine(repo,"artifacts","generated-check.cs"),source);
 Check(library.ListVersions().Count==1&&version.State=="Untested","Version persistence");Reject(()=>library.Recommend(version,a,b,ah,bh),"Wrong report recommended");
 b.Strategy=bh.Strategy=version.ClassName;b.SourceHash=bh.SourceHash=version.SourceHash;b.Parameters=bh.Parameters=version.Parameters.Copy();
 library.Recommend(version,a,b,ah,bh);Check(library.ListVersions()[0].EvidenceReportHashes.Length==4,"Evidence missing");
 a.SourceHash=ah.SourceHash="replaced";Reject(()=>library.Recommend(version,a,b,ah,bh),"Changed baseline provenance");
 string nt=Path.Combine(dir,"NinjaTrader 8"),custom=Path.Combine(nt,"bin","Custom");Directory.CreateDirectory(custom);File.WriteAllText(Path.Combine(custom,"NinjaTrader.Custom.csproj"),"<Project/>");
 var folder=new StrategyFolder(nt,Path.Combine(dir,"backups"));folder.Save("Mine.cs","original",null);var file=folder.List().Single();
 Reject(()=>folder.Save("Mine.cs","overwrite",null),"Import overwrote source");Reject(()=>folder.Save("../outside.cs","escape",null),"Path traversal");
 folder.Save(file.RelativePath,"edited",file.Hash);folder.Restore(folder.Backups().First(x=>x.Reason=="Edit"));Check(folder.Read(folder.List().Single())=="original","Edit restore");
 file=folder.List().Single();folder.Archive(file);Check(folder.List().Count==0,"Archive");folder.Restore(folder.Backups().First(x=>x.Reason=="Archive"));Check(folder.Read(folder.List().Single())=="original","Archive restore");
 File.WriteAllText(Path.Combine(custom,"Strategies","Mine.cs"),"external edit");Reject(()=>folder.Save(file.RelativePath,"lost",file.Hash),"Concurrent edit overwritten");
 string csv=Path.Combine(dir,"fills.csv");File.WriteAllText(csv,"schema_version,time,instrument,mode,price,quantity,market_position\r\n1,now,\"A,B\",Historical,100,1,Long\r\n1,unfinished");Check(LabData.LoadExecutions(csv).Single().Instrument=="A,B","CSV partial/quotes");
 Check(LabData.ResolveNinjaTraderHome(Path.Combine(custom,"Strategies"))==nt,"Nested home");
 string hostile=Path.Combine(dir,"hostile.xml");File.WriteAllText(hostile,"<!DOCTYPE foo [<!ENTITY x SYSTEM 'file:///C:/Windows/win.ini'>]><StrategyAnalyzerLog>&x;</StrategyAnalyzerLog>");Reject(()=>NativeReports.Load(hostile),"DTD accepted");
 var desktop=new DesktopWindow();var settings=new WorkspaceSettings{NinjaTraderHome=nt,ExportsRoot=Path.Combine(dir,"empty")};typeof(DesktopWindow).GetField("settings",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(desktop,settings);
 await (Task)typeof(DesktopWindow).GetMethod("RefreshAll",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(desktop,null);
 Check(((ComboBox)desktop.Window.FindName("NativePicker")).Items.Count==0,"Empty workspace fabricated results");
 Directory.CreateDirectory(Path.Combine(nt,"strategyanalyzerlogs"));File.Copy(Path.Combine(dir,"fixture.xml"),Path.Combine(nt,"strategyanalyzerlogs","fixture.xml"));
 await (Task)typeof(DesktopWindow).GetMethod("RefreshAll",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(desktop,null);
 Check(((TextBlock)desktop.Window.FindName("NativeTrades")).Text==100.ToString("N0"),"Fixture UI metrics");
 File.Delete(Path.Combine(nt,"strategyanalyzerlogs","fixture.xml"));
 Check(desktop.Window.FindName("RunButton")==null&&desktop.Window.FindName("DemoOverviewTab")==null,"Demo UI survived");
 if(args.Length>2&&File.Exists(args[2])){
 var real=NativeReports.Load(args[2]);
 var raw=XDocument.Load(args[2]).Descendants("SummaryPerformances").First(x=>(string)x.Element("PerformanceUnit")=="Currency");
 var metrics=raw.Element("SummaryPerformancesSerialize").Value.Split('|').Select(x=>x.Split(';')).Where(x=>x.Length>=2).ToDictionary(x=>x[0],x=>x[1]);
 Check(real.Trades==int.Parse(metrics["TotalNumTrades"],CultureInfo.InvariantCulture)&&real.Net==double.Parse(metrics["TotalNetProfit"],CultureInfo.InvariantCulture)&&real.Drawdown==Math.Abs(double.Parse(metrics["MaxDrawdown"],CultureInfo.InvariantCulture)),"Actual report metrics");Check(real.SourceHash==NativeReports.Hash(template),"Actual aliased source provenance");
 Console.WriteLine("Actual report: "+real.Trades+" trades, "+real.Net+" "+real.Currency+", "+real.Timeframe);
 Directory.CreateDirectory(Path.Combine(nt,"strategyanalyzerlogs"));File.Copy(args[2],Path.Combine(nt,"strategyanalyzerlogs","report.xml"));
 foreach(string snapshot in Directory.GetFiles(Path.GetDirectoryName(args[2]),"*.cs")) File.Copy(snapshot,Path.Combine(nt,"strategyanalyzerlogs",Path.GetFileName(snapshot)));
 await (Task)typeof(DesktopWindow).GetMethod("RefreshAll",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(desktop,null);
 Check(((ComboBox)desktop.Window.FindName("NativePicker")).Items.Count==1,"UI discovery");Check(((TextBlock)desktop.Window.FindName("NativeTrades")).Text==real.Trades.ToString("N0"),"Native KPI");
 typeof(DesktopWindow).GetMethod("Suggest",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(desktop,new object[]{true,false});Check(((ComboBox)desktop.Window.FindName("VersionPicker")).Items.Count==3,"Auto-create UI");
 var pages=(TabControl)desktop.Window.FindName("Pages");
 foreach(int tab in new[]{0,1,2}) {
 pages.SelectedIndex=tab;
 var visual=(FrameworkElement)desktop.Window.Content;visual.Measure(new Size(1184,806));visual.Arrange(new Rect(0,0,1184,806));visual.UpdateLayout();
 var bitmap=new RenderTargetBitmap(1184,806,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
 using(var stream=File.Create(Path.Combine(repo,"artifacts","preview-"+tab+".png"))) encoder.Save(stream);
 }

 }
 Reject(()=>typeof(DesktopWindow).GetMethod("GuardStrategyWrite",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(desktop,new object[]{false}),"Preview allowed writes");desktop.Window.Close();
 }
}
