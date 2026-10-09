using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

[assembly: System.Reflection.AssemblyVersion("1.4.3.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.4.3.0")]

namespace ClearSpace {
    public class Category {
        public string Name, Description, ProcessName;
        public bool RequiresAdmin, UpdateDownloads, BrowserOpen, Exists;
        public List<string> Roots = new List<string>();
        public List<Entry> Files = new List<Entry>();
        public int Skipped, Recent, Denied;
        public Dictionary<string,int> Reasons=new Dictionary<string,int>();
        public long Bytes { get { return Files.Sum(x=>x.Size); } }
        public void Skip(string reason){Skipped++;if(reason=="Access denied")Denied++;if(!Reasons.ContainsKey(reason))Reasons[reason]=0;Reasons[reason]++;}
        public string State {get{
            if(BrowserOpen)return "Browser open · locked files will be skipped";
            if(RequiresAdmin&&!Engine.IsAdministrator)return "Run as administrator · "+Files.Count+" files found";
            if(!Exists)return "Cache folder not found";
            if(Denied>0)return Denied+" access denied";
            if(Recent>0)return Recent+" recent files excluded";
            if(Skipped>0)return Skipped+" skipped (see details)";
            return Files.Count==0?"No matching files":"Ready";
        }}
    }
    public class Entry { public string Path,Root;public long Size;public DateTime Modified,Created; }
    public class CleanResult {
        public long Bytes;public int Deleted,Skipped;
        public List<string> Notes=new List<string>();public Dictionary<string,int> Reasons=new Dictionary<string,int>();
        public void Skip(string reason,int count=1){Skipped+=count;if(!Reasons.ContainsKey(reason))Reasons[reason]=0;Reasons[reason]+=count;}
        public string Details {get{return String.Join(Environment.NewLine,Reasons.Select(x=>x.Key+": "+x.Value).Concat(Notes));}}
    }
    public interface IUpdateServices {
        ServiceControllerStatus Status(string name);
        void Stop(string name,Func<bool> cancel);
        void Start(string name);
    }
    public class WindowsUpdateServices : IUpdateServices {
        public ServiceControllerStatus Status(string name){using(var service=new ServiceController(name))return service.Status;}
        public void Stop(string name,Func<bool> cancel){using(var service=new ServiceController(name)){
            foreach(var dependent in service.DependentServices)using(dependent){if(dependent.Status!=ServiceControllerStatus.Stopped)throw new InvalidOperationException(name+" has a running dependent service.");}
            service.Stop();var timeout=DateTime.UtcNow.AddSeconds(15);while(service.Status!=ServiceControllerStatus.Stopped){if(cancel())throw new OperationCanceledException();if(DateTime.UtcNow>timeout)throw new System.TimeoutException("Could not stop "+name+".");Thread.Sleep(100);service.Refresh();}
        }}
        public void Start(string name){using(var service=new ServiceController(name)){
            service.Refresh();if(service.Status==ServiceControllerStatus.StopPending)service.WaitForStatus(ServiceControllerStatus.Stopped,TimeSpan.FromSeconds(20));
            service.Refresh();if(service.Status==ServiceControllerStatus.Stopped)service.Start();service.WaitForStatus(ServiceControllerStatus.Running,TimeSpan.FromSeconds(20));
        }}
    }
    public sealed class UpdatePause : IDisposable {
        readonly IUpdateServices services;readonly List<string> restore=new List<string>();readonly List<string> notes;
        public UpdatePause(IUpdateServices value,List<string> messages){services=value;notes=messages;}
        public void Begin(Func<bool> cancel){string[] names={"bits","wuauserv"};foreach(string name in names){var state=services.Status(name);if(state!=ServiceControllerStatus.Running&&state!=ServiceControllerStatus.Stopped)throw new InvalidOperationException("Update services are changing state; try later.");}
            foreach(string name in names){if(cancel())throw new OperationCanceledException();if(services.Status(name)==ServiceControllerStatus.Running){restore.Add(name);services.Stop(name,cancel);}}
            if(!Stopped)throw new InvalidOperationException("Update services could not be paused.");
        }
        public bool Stopped {get{return services.Status("bits")==ServiceControllerStatus.Stopped&&services.Status("wuauserv")==ServiceControllerStatus.Stopped;}}
        public void Dispose(){for(int i=restore.Count-1;i>=0;i--){try{services.Start(restore[i]);}catch(Exception ex){notes.Add("Service restart needs attention: "+restore[i]+". "+ex.Message);}}restore.Clear();}
    }
    public static class Engine {
        [StructLayout(LayoutKind.Sequential)] struct FileTime {public uint Low,High;public DateTime Utc{get{return DateTime.FromFileTimeUtc(((long)High<<32)|Low);}}}
        [StructLayout(LayoutKind.Sequential)] struct HandleInfo {public uint Attributes;public FileTime Creation,Access,Write;public uint Volume,SizeHigh,SizeLow,Links,IndexHigh,IndexLow;}
        [StructLayout(LayoutKind.Sequential)] struct Disposition {[MarshalAs(UnmanagedType.U1)]public bool Delete;}
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern SafeFileHandle CreateFile(string name,uint access,uint share,IntPtr security,uint mode,uint flags,IntPtr template);
        [DllImport("kernel32.dll",SetLastError=true)]static extern bool GetFileInformationByHandle(SafeFileHandle handle,out HandleInfo info);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern uint GetFinalPathNameByHandle(SafeFileHandle handle,StringBuilder name,uint size,uint flags);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern uint GetLongPathName(string shortName,StringBuilder longName,uint size);
        [DllImport("kernel32.dll",SetLastError=true)]static extern bool SetFileInformationByHandle(SafeFileHandle handle,int kind,ref Disposition info,uint size);
        public static bool IsAdministrator {get{using(var identity=WindowsIdentity.GetCurrent())return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);}}
        public static string Canonical(string path){string full=Path.GetFullPath(path).TrimEnd('\\');if(full.IndexOf('~')<0)return full;var longPath=new StringBuilder(Math.Max(512,full.Length+1));uint size=GetLongPathName(full,longPath,(uint)longPath.Capacity);if(size>=longPath.Capacity){longPath.Capacity=(int)size+1;size=GetLongPathName(full,longPath,(uint)longPath.Capacity);}return size>0&&size<longPath.Capacity?longPath.ToString().TrimEnd('\\'):full;}
        public static bool Inside(string path,string root){return Canonical(path).StartsWith(Canonical(root)+"\\",StringComparison.OrdinalIgnoreCase);}
        static bool NoLinks(string path){string current=Path.GetFullPath(path);while(!String.IsNullOrEmpty(current)){if((File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)return false;current=Path.GetDirectoryName(current);}return true;}
        public static bool SafePath(string path,string root){try{return Inside(path,root)&&NoLinks(path);}catch{return false;}}
        public static bool Running(string name){if(String.IsNullOrEmpty(name))return false;try{var processes=Process.GetProcessesByName(name);bool found=processes.Length>0;foreach(var p in processes)p.Dispose();return found;}catch{return true;}}
        static string ErrorReason(int code){return code==5?"Access denied":code==32||code==33?"File in use":code==2||code==3?"File disappeared":"Windows error "+code;}
        static bool DeleteExclusive(Entry entry,DateTime cutoff,out string reason){reason="File changed or unsafe";
            using(var handle=CreateFile(entry.Path,0x00010080,0,IntPtr.Zero,3,0x00200000,IntPtr.Zero)){
                if(handle.IsInvalid){reason=ErrorReason(Marshal.GetLastWin32Error());return false;}
                HandleInfo info;if(!GetFileInformationByHandle(handle,out info)){reason=ErrorReason(Marshal.GetLastWin32Error());return false;}
                long size=((long)info.SizeHigh<<32)|info.SizeLow;
                if((info.Attributes&0x410)!=0||info.Links>1||size!=entry.Size||info.Write.Utc!=entry.Modified||info.Creation.Utc!=entry.Created||info.Write.Utc>=cutoff||info.Creation.Utc>=cutoff)return false;
                var resolved=new StringBuilder(32768);uint length=GetFinalPathNameByHandle(handle,resolved,(uint)resolved.Capacity,0);if(length==0||length>=resolved.Capacity)return false;
                string actual=resolved.ToString();if(actual.StartsWith("\\\\?\\",StringComparison.Ordinal))actual=actual.Substring(4);
                if(!String.Equals(Canonical(actual),Canonical(entry.Path),StringComparison.OrdinalIgnoreCase)||!SafePath(entry.Path,entry.Root))return false;
                var disposition=new Disposition{Delete=true};bool deleted=SetFileInformationByHandle(handle,4,ref disposition,(uint)Marshal.SizeOf(typeof(Disposition)));if(!deleted)reason=ErrorReason(Marshal.GetLastWin32Error());return deleted;
            }
        }
        public static List<string> TempRoots(string local,string configured){var roots=new List<string>{Canonical(Path.Combine(local,"Temp"))};
            // Include the actual TEMP setting only inside this user's LocalAppData.
            // Do not let an environment variable turn a personal folder into a cleaning root.
            if(!String.IsNullOrWhiteSpace(configured)){string resolved=Canonical(configured);string leaf=Path.GetFileName(resolved);if(Inside(resolved,local)&&(leaf.Equals("Temp",StringComparison.OrdinalIgnoreCase)||leaf.Equals("Tmp",StringComparison.OrdinalIgnoreCase))&&!roots.Contains(resolved,StringComparer.OrdinalIgnoreCase))roots.Add(resolved);}return roots;
        }
        static void AddRoot(Category category,string path){if(String.IsNullOrWhiteSpace(path))return;try{string root=Canonical(path);if(!category.Roots.Contains(root,StringComparer.OrdinalIgnoreCase))category.Roots.Add(root);}catch{category.Skip("Invalid cache path");}}
        public static List<string> AppCacheRoots(string local){var roots=new List<string>();Action<string> add=path=>{try{string root=Canonical(path);if(!roots.Contains(root,StringComparer.OrdinalIgnoreCase))roots.Add(root);}catch{}};
            add(Path.Combine(local,"Microsoft","Windows","INetCache"));
            add(Path.Combine(local,"D3DSCache"));
            add(Path.Combine(local,"Microsoft","Windows","Explorer"));
            string packages=Path.Combine(local,"Packages");try{if(Directory.Exists(packages))foreach(string package in Directory.GetDirectories(packages)){add(Path.Combine(package,"TempState"));add(Path.Combine(package,"AC","INetCache"));}}catch{}
            return roots;
        }
        public static List<Category> Categories(){string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);string windows=Environment.GetFolderPath(Environment.SpecialFolder.Windows);var result=new List<Category>();
            result.Add(new Category{Name="Temporary files",Description="Your LocalAppData Temp and configured user TEMP folder",Roots=TempRoots(local,Path.GetTempPath())});
            result.Add(new Category{Name="Windows temporary files",Description="Windows Temp; administrator access is required",RequiresAdmin=true,Roots=new List<string>{Path.Combine(windows,"Temp")}});
            string roaming=Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            result.Add(new Category{Name="Windows and app caches",Description="Windows web, thumbnail, shader and packaged-app cache folders",Roots=AppCacheRoots(local)});
            AddBrowser(result,"Chrome cache","chrome",new[]{Path.Combine(local,"Google","Chrome","User Data"),Path.Combine(local,"Google","Chrome SxS","User Data"),Path.Combine(roaming,"Google","Chrome","User Data")});
            AddBrowser(result,"Edge cache","msedge",Path.Combine(local,"Microsoft","Edge","User Data"));
            AddBrowser(result,"Brave cache","brave",Path.Combine(local,"BraveSoftware","Brave-Browser","User Data"));
            AddBrowser(result,"Vivaldi cache","vivaldi",Path.Combine(local,"Vivaldi","User Data"));
            AddBrowser(result,"Chromium cache","chromium",Path.Combine(local,"Chromium","User Data"));
            AddBrowser(result,"Yandex cache","browser",Path.Combine(local,"Yandex","YandexBrowser","User Data"));
            AddOpera(result,"Opera cache","opera",new[]{Path.Combine(roaming,"Opera Software","Opera Stable"),Path.Combine(local,"Opera Software","Opera Stable")});
            AddOpera(result,"Opera GX cache","opera",new[]{Path.Combine(roaming,"Opera Software","Opera GX Stable"),Path.Combine(local,"Opera Software","Opera GX Stable")});
            AddFirefox(result,"Firefox cache","firefox",new[]{Path.Combine(local,"Mozilla","Firefox","Profiles"),Path.Combine(roaming,"Mozilla","Firefox","Profiles")});
            result.Add(new Category{Name="Windows Update downloads",Description="Downloaded update packages only; requires administrator mode and pauses update services during cleaning",RequiresAdmin=true,UpdateDownloads=true,Roots=new List<string>{Path.Combine(windows,"SoftwareDistribution","Download")}});return result;
        }
        public static void AddBrowser(List<Category> all,string name,string process,string location){AddBrowser(all,name,process,new[]{location});}
        static void AddChromiumProfile(Category category,string profile){foreach(string leaf in new[]{"Cache","Code Cache","GPUCache","DawnCache","DawnGraphiteCache","DawnWebGPUCache","Media Cache",Path.Combine("Network","Cache"),Path.Combine("Service Worker","CacheStorage"),Path.Combine("Service Worker","ScriptCache")})AddRoot(category,Path.Combine(profile,leaf));}
        public static void AddBrowser(List<Category> all,string name,string process,IEnumerable<string> locations){var category=new Category{Name=name,Description="Cached content only; open-browser cache files are removed only when they are not in use",ProcessName=process};
            foreach(string location in locations){try{if(!Directory.Exists(location))continue;string data=Canonical(location);foreach(string profile in Directory.GetDirectories(data)){string n=Path.GetFileName(profile);if(n=="Default"||n=="Guest Profile"||n.StartsWith("Profile ",StringComparison.Ordinal)||File.Exists(Path.Combine(profile,"Preferences")))AddChromiumProfile(category,profile);}foreach(string shared in new[]{"ShaderCache","GrShaderCache","GraphiteDawnCache","component_crx_cache","extensions_crx_cache"})AddRoot(category,Path.Combine(data,shared));}catch{category.Skip("Cannot inspect browser profiles");}}all.Add(category);
        }
        public static void AddOpera(List<Category> all,string name,string process,IEnumerable<string> profiles){var category=new Category{Name=name,Description="Cached content only; open-browser cache files are removed only when they are not in use",ProcessName=process};foreach(string profile in profiles)try{if(Directory.Exists(profile))AddChromiumProfile(category,Canonical(profile));}catch{category.Skip("Cannot inspect browser profiles");}all.Add(category);}
        public static void AddFirefox(List<Category> all,string name,string process,IEnumerable<string> profileParents){var category=new Category{Name=name,Description="Firefox cache2, startup, shader and thumbnail caches only",ProcessName=process};foreach(string parent in profileParents)try{if(!Directory.Exists(parent))continue;foreach(string profile in Directory.GetDirectories(parent))foreach(string leaf in new[]{"cache2","startupCache","shader-cache","thumbnails"})AddRoot(category,Path.Combine(profile,leaf));}catch{category.Skip("Cannot inspect browser profiles");}all.Add(category);
        }
        public static void Scan(Category category,DateTime cutoff,Func<bool> cancel){category.Files.Clear();category.Skipped=0;category.Recent=0;category.Denied=0;category.Exists=false;category.Reasons.Clear();category.BrowserOpen=Running(category.ProcessName);var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(string configured in category.Roots){if(cancel())return;string root=Canonical(configured);try{if(!Directory.Exists(root))continue;category.Exists=true;if(!NoLinks(root)){category.Skip("Linked folder excluded");continue;}}catch(UnauthorizedAccessException){category.Skip("Access denied");continue;}catch{category.Skip("Folder unavailable");continue;}
                var pending=new Stack<string>();pending.Push(root);while(pending.Count>0&&!cancel()){string directory=pending.Pop();try{
                    foreach(string file in Directory.GetFiles(directory)){if(cancel())return;try{if(!SafePath(file,root)){category.Skip("Linked or inaccessible path");continue;}string canonical=Canonical(file);if(!seen.Add(canonical))continue;var info=new FileInfo(canonical);if(info.LastWriteTimeUtc>=cutoff||info.CreationTimeUtc>=cutoff){category.Recent++;continue;}category.Files.Add(new Entry{Path=canonical,Root=root,Size=info.Length,Modified=info.LastWriteTimeUtc,Created=info.CreationTimeUtc});}catch(UnauthorizedAccessException){category.Skip("Access denied");}catch{category.Skip("File unavailable");}}
                    foreach(string sub in Directory.GetDirectories(directory)){if(SafePath(sub,root))pending.Push(sub);else category.Skip("Linked or inaccessible folder");}
                }catch(UnauthorizedAccessException){category.Skip("Access denied");}catch{category.Skip("Folder unavailable");}}
            }
        }
        public static string UpdateBlockReason(){if(!IsAdministrator)return "Administrator access is required for Windows Update downloads.";
            if(Running("TiWorker")||Running("TrustedInstaller"))return "Windows servicing is active. Wait for updates to finish, then scan again.";
            try{using(var hive=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64))foreach(string key in new[]{@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending",@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired"})using(var pending=hive.OpenSubKey(key)){if(pending!=null)return "Windows Update needs a restart. Restart Windows before cleaning update downloads.";}}catch{return "Could not verify the Windows Update state.";}return null;
        }
        public static CleanResult Clean(List<Category> selected,DateTime cutoff,Func<bool> cancel){var result=new CleanResult();foreach(var category in selected){if(cancel())break;
                if(category.RequiresAdmin&&!IsAdministrator){result.Skip("Run as administrator: "+category.Name,category.Files.Count);continue;}
                if(category.UpdateDownloads){string reason=UpdateBlockReason();if(reason!=null){result.Skip(reason,category.Files.Count);continue;}
                    int processedBefore=result.Deleted+result.Skipped;using(var paused=new UpdatePause(new WindowsUpdateServices(),result.Notes)){try{paused.Begin(cancel);CleanFiles(category,cutoff,cancel,result,()=>paused.Stopped);}catch(OperationCanceledException){break;}catch(Exception ex){result.Skip("Windows Update cleanup stopped",Math.Max(0,category.Files.Count-(result.Deleted+result.Skipped-processedBefore)));result.Notes.Add("Windows Update downloads: "+ex.Message);}}}
                else CleanFiles(category,cutoff,cancel,result,()=>true);
            }return result;
        }
        static void CleanFiles(Category category,DateTime cutoff,Func<bool> cancel,CleanResult result,Func<bool> ready){for(int index=0;index<category.Files.Count;index++){if(cancel())return;if(!ready()){result.Skip("Update services resumed; clean again later",category.Files.Count-index);return;}var entry=category.Files[index];try{
                if(!category.Roots.Any(root=>String.Equals(Canonical(root),Canonical(entry.Root),StringComparison.OrdinalIgnoreCase))||!SafePath(entry.Path,entry.Root)){result.Skip("Path outside the approved cache or a linked path");continue;}
                if(category.UpdateDownloads&&!String.Equals(Canonical(entry.Root),Canonical(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"SoftwareDistribution","Download")),StringComparison.OrdinalIgnoreCase)){result.Skip("Outside Windows Update download cache");continue;}
                string reason;if(!DeleteExclusive(entry,cutoff,out reason)){result.Skip(reason);continue;}result.Deleted++;result.Bytes+=entry.Size;
            }catch(UnauthorizedAccessException){result.Skip("Access denied");}catch(IOException){result.Skip("File in use or unavailable");}catch{result.Skip("File could not be removed");}}}
        public static string ScanDetails(IEnumerable<Category> categories){return String.Join(Environment.NewLine+Environment.NewLine,categories.Select(c=>c.Name+" — "+c.State+Environment.NewLine+"Eligible: "+c.Files.Count+" files, "+Size(c.Bytes)+"; recent excluded: "+c.Recent+Environment.NewLine+String.Join(Environment.NewLine,c.Roots)+Environment.NewLine+String.Join("; ",c.Reasons.Select(x=>x.Key+": "+x.Value))));}
        public static string Size(long n){string[] units={"B","KB","MB","GB","TB"};double value=n;int i=0;while(value>=1024&&i<4){value/=1024;i++;}return value.ToString(i==0?"0":"0.0")+" "+units[i];}
    }

    public static class Program {
        [STAThread] public static int Main(string[] args){
            if(args.Length>1&&args[0]=="--expected-user"&&WindowsIdentity.GetCurrent().User.Value!=args[1]){MessageBox.Show("Run ClearSpace with administrator privileges for the same Windows account to keep cleaning the correct user's cache.","Different Windows account");return 1;}
            if(args.Length>0&&args[0]=="--self-test")return Tests.Run(args.Length>1?args[1]:".");
            if(args.Length>0&&args[0]=="--scan-report") {var cats=Engine.Categories();File.WriteAllText(args[1],"Administrator: "+Engine.IsAdministrator+Environment.NewLine);foreach(var c in cats){File.AppendAllText(args[1],"Scanning "+c.Name+Environment.NewLine);Engine.Scan(c,args.Contains("--all-ages")?DateTime.MaxValue:DateTime.UtcNow.AddDays(-1),()=>false);File.AppendAllText(args[1],Engine.ScanDetails(new[]{c})+Environment.NewLine+Environment.NewLine);}return 0;}
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            if(args.Length>0&&args[0]=="--ui-smoke"){using(var f=new MainWindow()){Task task=null;var timer=new System.Windows.Forms.Timer {Interval=100};f.Shown+=(s,e)=>{task=f.DoScan();timer.Start();};timer.Tick+=(s,e)=>{if(task!=null&&task.IsCompleted){timer.Stop();try{f.VerifyUiAndCapture(args[1]);}catch(Exception ex){File.WriteAllText(Path.ChangeExtension(args[1],"error.txt"),ex.ToString());Environment.ExitCode=1;}f.Close();}};Application.Run(f);timer.Dispose();}return Environment.ExitCode;}
            Application.Run(new MainWindow());return 0;
        }
    }
    public static class ExtendedTests {
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern uint GetShortPathName(string full,StringBuilder result,uint length);
        static void Check(bool value,string message){if(!value)throw new Exception(message);}
        class FakeServices : IUpdateServices {
            public Dictionary<string,ServiceControllerStatus> States=new Dictionary<string,ServiceControllerStatus>{{"bits",ServiceControllerStatus.Running},{"wuauserv",ServiceControllerStatus.Running}};
            public List<string> Started=new List<string>();public string FailStop,FailStart;
            public ServiceControllerStatus Status(string name){return States[name];}
            public void Stop(string name,Func<bool> cancel){States[name]=ServiceControllerStatus.Stopped;if(name==FailStop)throw new IOException("Simulated stop failure");}
            public void Start(string name){Started.Add(name);if(name==FailStart)throw new IOException("Simulated restart failure");States[name]=ServiceControllerStatus.Running;}
        }
        public static void Run(string root){
            var catalog=Engine.Categories();Check(catalog.Count>=12&&catalog.Any(c=>c.ProcessName=="brave")&&catalog.Any(c=>c.ProcessName=="firefox")&&catalog.Any(c=>c.Name=="Windows and app caches")&&catalog.Count(c=>c.UpdateDownloads)==1,"Missing cache categories");
            string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);Check(Engine.TempRoots(local,Path.GetTempPath()).Count==1,"User TEMP alias must be deduplicated");
            Check(Engine.TempRoots(root,Path.Combine(root,"Documents")).Count==1,"Unsafe configured TEMP accepted");
            string any=Path.Combine(root,"any-age");Directory.CreateDirectory(any);string file=Path.Combine(any,"fresh.tmp");File.WriteAllText(file,"recent test data");var category=new Category{Roots=new List<string>{any}};
            Engine.Scan(category,DateTime.UtcNow.AddDays(-1),()=>false);Check(category.Files.Count==0&&category.Recent==1,"Recent file accounting failed");Engine.Scan(category,DateTime.MaxValue,()=>false);Check(category.Files.Count==1,"Any age scan excluded a recent file");
            var alias=new StringBuilder(32768);if(GetShortPathName(file,alias,(uint)alias.Capacity)>0){Check(Engine.Canonical(alias.ToString())==Engine.Canonical(file),"Short path was not normalized");category.Files[0].Path=alias.ToString();Check(Engine.Clean(new List<Category>{category},DateTime.MaxValue,()=>false).Deleted==1&&!File.Exists(file),"Short-path/Any-age deletion failed");}else Check(Engine.Clean(new List<Category>{category},DateTime.MaxValue,()=>false).Deleted==1&&!File.Exists(file),"Any-age deletion failed");
            string browser=Path.Combine(root,"browser");foreach(string profile in new[]{"Default","Profile 3","Custom"}){string dir=Path.Combine(browser,profile);Directory.CreateDirectory(Path.Combine(dir,"Cache","Cache_Data"));Directory.CreateDirectory(Path.Combine(dir,"Media Cache"));Directory.CreateDirectory(Path.Combine(dir,"Network","Cache"));Directory.CreateDirectory(Path.Combine(dir,"Service Worker","CacheStorage"));File.WriteAllText(Path.Combine(dir,"Preferences"),"{}");File.WriteAllText(Path.Combine(dir,"Cookies"),"keep cookies");File.WriteAllText(Path.Combine(dir,"History"),"keep history");File.WriteAllText(Path.Combine(dir,"Cache","legacy"),"cache");File.WriteAllText(Path.Combine(dir,"Cache","Cache_Data","data"),"cache");File.WriteAllText(Path.Combine(dir,"Media Cache","media"),"cache");File.WriteAllText(Path.Combine(dir,"Network","Cache","network"),"cache");File.WriteAllText(Path.Combine(dir,"Service Worker","CacheStorage","worker"),"cache");}
            var testBrowsers=new List<Category>();Engine.AddBrowser(testBrowsers,"Test browser",null,browser);Engine.Scan(testBrowsers[0],DateTime.MaxValue,()=>false);Check(testBrowsers[0].Files.Count==15,"Browser cache layouts/profiles not discovered");Check(Engine.Clean(testBrowsers,DateTime.MaxValue,()=>false).Deleted==15,"Browser fixture clean failed");Check(File.Exists(Path.Combine(browser,"Default","Cookies"))&&File.Exists(Path.Combine(browser,"Custom","History")),"Personal browser data removed");
            string firefox=Path.Combine(root,"firefox-profiles"),firefoxProfile=Path.Combine(firefox,"test.default-release");Directory.CreateDirectory(Path.Combine(firefoxProfile,"cache2"));Directory.CreateDirectory(Path.Combine(firefoxProfile,"startupCache"));File.WriteAllText(Path.Combine(firefoxProfile,"cache2","cache"),"cache");File.WriteAllText(Path.Combine(firefoxProfile,"startupCache","startup"),"cache");File.WriteAllText(Path.Combine(firefoxProfile,"cookies.sqlite"),"keep cookies");var firefoxCategories=new List<Category>();Engine.AddFirefox(firefoxCategories,"Firefox",null,new[]{firefox});Engine.Scan(firefoxCategories[0],DateTime.MaxValue,()=>false);Check(firefoxCategories[0].Files.Count==2,"Firefox cache folders not discovered");Check(Engine.Clean(firefoxCategories,DateTime.MaxValue,()=>false).Deleted==2&&File.Exists(Path.Combine(firefoxProfile,"cookies.sqlite")),"Firefox cache safety failed");
            var notes=new List<string>();var fake=new FakeServices();using(var paused=new UpdatePause(fake,notes)){paused.Begin(()=>false);Check(paused.Stopped,"Services not stopped");}Check(fake.States.Values.All(s=>s==ServiceControllerStatus.Running),"Service restoration failed");
            fake=new FakeServices{FailStop="wuauserv"};try{using(var paused=new UpdatePause(fake,notes))paused.Begin(()=>false);}catch(IOException){}Check(fake.States.Values.All(s=>s==ServiceControllerStatus.Running),"Service restoration after partial stop failed");
            fake=new FakeServices();int checks=0;try{using(var paused=new UpdatePause(fake,notes))paused.Begin(()=>++checks>1);}catch(OperationCanceledException){}Check(fake.States.Values.All(s=>s==ServiceControllerStatus.Running),"Service restoration after cancellation failed");
            fake=new FakeServices();fake.States["bits"]=ServiceControllerStatus.Stopped;using(var paused=new UpdatePause(fake,notes))paused.Begin(()=>false);Check(fake.States["bits"]==ServiceControllerStatus.Stopped&&!fake.Started.Contains("bits"),"Originally stopped service was started");
            fake=new FakeServices{FailStart="wuauserv"};using(var paused=new UpdatePause(fake,notes))paused.Begin(()=>false);Check(notes.Any(n=>n.Contains("restart needs attention"))&&fake.States["bits"]==ServiceControllerStatus.Running,"Restart failure was hidden or blocked other restoration");
        }
    }
    public static class Tests {
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool CreateHardLink(string link,string target,IntPtr reserved);
        static void Assert(bool value,string label){if(!value)throw new Exception(label);}
        static void Old(string path){File.WriteAllText(path,"temporary fixture");File.SetCreationTimeUtc(path,DateTime.UtcNow.AddDays(-10));File.SetLastWriteTimeUtc(path,DateTime.UtcNow.AddDays(-10));}
        public static int Run(string parent){string root=System.IO.Path.Combine(System.IO.Path.GetFullPath(parent),"cleaner-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
            try {string cache=System.IO.Path.Combine(root,"cache");Directory.CreateDirectory(cache);string old=System.IO.Path.Combine(cache,"old.tmp"),fresh=System.IO.Path.Combine(cache,"fresh.tmp"),changed=System.IO.Path.Combine(cache,"changed.tmp"),locked=System.IO.Path.Combine(cache,"locked.tmp"),outside=System.IO.Path.Combine(root,"personal.txt");Old(old);Old(changed);Old(locked);Old(outside);File.WriteAllText(fresh,"recent");var c=new Category {Roots=new List<string>{cache}};DateTime cutoff=DateTime.UtcNow.AddDays(-1);
                Engine.Scan(c,cutoff,()=>false);Assert(c.Files.Count==3,"Age exclusion failed");Assert(!Engine.Inside(outside,cache),"Boundary check failed");Assert(!Engine.Inside(cache+"-other\\x",cache),"Sibling boundary failed");Assert(!Engine.SafePath(outside,cache),"Outside path accepted");
                File.AppendAllText(changed," changed after scan");using(var hold=new FileStream(locked,FileMode.Open,FileAccess.Read,FileShare.None)){var r=Engine.Clean(new List<Category>{c},cutoff,()=>false);Assert(r.Deleted==1&&r.Skipped==2,"Changed or locked file protection failed");}
                Assert(!File.Exists(old)&&File.Exists(fresh)&&File.Exists(changed)&&File.Exists(locked)&&File.Exists(outside),"Preservation failed");
                var forged=new Category {Roots=new List<string>{cache},Files=new List<Entry>{new Entry {Path=outside,Root=root,Size=new FileInfo(outside).Length,Modified=File.GetLastWriteTimeUtc(outside),Created=File.GetCreationTimeUtc(outside)}}};Assert(Engine.Clean(new List<Category>{forged},cutoff,()=>false).Deleted==0,"Forged root accepted");
                Engine.Scan(c,cutoff,()=>true);Assert(c.Files.Count==0,"Cancel scan failed");
                Engine.Scan(c,cutoff,()=>false);Assert(Engine.Clean(new List<Category>{c},cutoff,()=>true).Deleted==0,"Cancel cleaning failed");
                var running=new Category {Roots=new List<string>{cache},ProcessName=Process.GetCurrentProcess().ProcessName};Engine.Scan(running,cutoff,()=>false);Assert(running.Files.Count>0&&running.BrowserOpen,"Running browser cache should remain visible in read-only scan");var liveClean=Engine.Clean(new List<Category>{running},cutoff,()=>false);Assert(liveClean.Deleted>0&&liveClean.Skipped==0,"Safe cache files should clean while browser is open");
                string hardlink=System.IO.Path.Combine(cache,"hardlink.tmp");Assert(CreateHardLink(hardlink,outside,IntPtr.Zero),"Hardlink fixture creation failed");Engine.Scan(c,cutoff,()=>false);var onlyLink=new Category {Roots=c.Roots,Files=c.Files.Where(x=>x.Path==hardlink).ToList()};Assert(onlyLink.Files.Count==1&&Engine.Clean(new List<Category>{onlyLink},cutoff,()=>false).Deleted==0,"Hardlink protection failed");
                string junction=System.IO.Path.Combine(System.IO.Path.GetFullPath(parent),"junction-fixture");Assert(Directory.Exists(junction),"Junction fixture is required");Assert(!Engine.SafePath(System.IO.Path.Combine(junction,"old.txt"),junction),"Junction accepted");var linked=new Category {Roots=new List<string>{junction}};Engine.Scan(linked,cutoff,()=>false);Assert(linked.Files.Count==0&&linked.Skipped>0,"Junction scan failed");
                ExtendedTests.Run(root);
                File.WriteAllText(System.IO.Path.Combine(parent,"self-test.txt"),"PASS: age filtering and Any age deletion; 8.3 path normalization and deletion; expanded cache categories; Chromium and Firefox profile discovery; service-worker, media and network cache discovery; live-browser cache cleaning; locked files remain protected; cookie/history/password preservation; update service pause and restore on success, failure and cancellation; path boundaries; changed files; personal-file preservation; forged-root rejection; cancellation; hardlink/junction protection.\r\nFixtures: "+root);return 0;
            }catch(Exception ex){File.WriteAllText(System.IO.Path.Combine(parent,"self-test.txt"),"FAIL: "+ex);return 1;}
            // Fixtures are retained for inspection. No recursive removal is used.
        }
    }
}
