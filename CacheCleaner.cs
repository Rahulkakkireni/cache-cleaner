using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32.SafeHandles;

namespace ClearSpace {
    public class Category {
        public string Name, Description, ProcessName;
        public List<string> Roots = new List<string>();
        public List<Entry> Files = new List<Entry>();
        public int Skipped;
        public long Bytes { get { return Files.Sum(x => x.Size); } }
    }
    public class Entry { public string Path, Root; public long Size; public DateTime Modified, Created; }
    public class CleanResult { public long Bytes; public int Deleted, Skipped; }
    public static class Engine {
        [StructLayout(LayoutKind.Sequential)] struct FileTime { public uint Low, High; public DateTime Utc { get { return DateTime.FromFileTimeUtc(((long)High<<32)|Low); } } }
        [StructLayout(LayoutKind.Sequential)] struct HandleInfo { public uint Attributes; public FileTime Creation, Access, Write; public uint Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow; }
        [StructLayout(LayoutKind.Sequential)] struct Disposition { [MarshalAs(UnmanagedType.U1)] public bool Delete; }
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern SafeFileHandle CreateFile(string name,uint access,uint share,IntPtr security,uint mode,uint flags,IntPtr template);
        [DllImport("kernel32.dll",SetLastError=true)] static extern bool GetFileInformationByHandle(SafeFileHandle handle,out HandleInfo info);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern uint GetFinalPathNameByHandle(SafeFileHandle handle,StringBuilder name,uint size,uint flags);
        [DllImport("kernel32.dll",SetLastError=true)] static extern bool SetFileInformationByHandle(SafeFileHandle handle,int kind,ref Disposition info,uint size);
        static bool DeleteExclusive(Entry e,DateTime cutoff) {
            // Keep an exclusive handle through validation and deletion. A path replacement
            // cannot redirect this deletion to a different file after validation.
            using(var handle=CreateFile(e.Path,0x00010080,0,IntPtr.Zero,3,0x00200000,IntPtr.Zero)) {
                if(handle.IsInvalid) return false;
                HandleInfo info; if(!GetFileInformationByHandle(handle,out info)) return false;
                long size=((long)info.SizeHigh<<32)|info.SizeLow;
                if((info.Attributes&0x410)!=0 || info.Links>1 || size!=e.Size || info.Write.Utc!=e.Modified || info.Creation.Utc!=e.Created || info.Write.Utc>=cutoff || info.Creation.Utc>=cutoff) return false;
                var resolved=new StringBuilder(32768);uint length=GetFinalPathNameByHandle(handle,resolved,(uint)resolved.Capacity,0);
                if(length==0 || length>=resolved.Capacity) return false;
                string actual=resolved.ToString();if(actual.StartsWith("\\\\?\\",StringComparison.Ordinal)) actual=actual.Substring(4);
                if(!String.Equals(Canonical(actual),Canonical(e.Path),StringComparison.OrdinalIgnoreCase)||!SafePath(e.Path,e.Root)) return false;
                var disposition=new Disposition {Delete=true};return SetFileInformationByHandle(handle,4,ref disposition,(uint)Marshal.SizeOf(typeof(Disposition)));
            }
        }
        public static string Canonical(string path) { return System.IO.Path.GetFullPath(path).TrimEnd('\\'); }
        public static bool Inside(string path, string root) {
            return Canonical(path).StartsWith(Canonical(root) + "\\", StringComparison.OrdinalIgnoreCase);
        }
        public static bool SafePath(string path, string root) {
            try {
                if (!Inside(path, root)) return false;
                string current = Canonical(path);
                // Check every ancestor, including ancestors above the approved root.
                while (!String.IsNullOrEmpty(current)) {
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return false;
                    current = System.IO.Path.GetDirectoryName(current);
                }
                return true;
            } catch { return false; }
        }
        public static bool Running(string process) {
            if (String.IsNullOrEmpty(process)) return false;
            try { var all = Process.GetProcessesByName(process); bool found = all.Length > 0; foreach(var p in all) p.Dispose(); return found; }
            catch { return true; }
        }
        public static List<Category> Categories() {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var result = new List<Category>();
            result.Add(new Category { Name="Your temporary files", Description="Temporary files older than the selected age", Roots=new List<string>{System.IO.Path.Combine(local,"Temp")} });
            result.Add(new Category { Name="Windows temporary files", Description="Accessible system temp files; no administrator rights required", Roots=new List<string>{System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"Temp")} });
            AddBrowser(result, "Google Chrome cache", "chrome", System.IO.Path.Combine(local,"Google","Chrome","User Data"));
            AddBrowser(result, "Microsoft Edge cache", "msedge", System.IO.Path.Combine(local,"Microsoft","Edge","User Data"));
            return result;
        }
        static void AddBrowser(List<Category> all,string name,string process,string location) {
            var c=new Category { Name=name, Description="Cached content only; cookies, history and passwords stay intact", ProcessName=process };
            try { if(Directory.Exists(location)) foreach(string profile in Directory.GetDirectories(location)) {
                string n=System.IO.Path.GetFileName(profile);
                if(n=="Default" || n.StartsWith("Profile ",StringComparison.Ordinal)) {
                    foreach(string leaf in new[]{"Cache\\Cache_Data","Code Cache","GPUCache"}) c.Roots.Add(System.IO.Path.Combine(profile,leaf));
                }
            }} catch { c.Skipped++; }
            all.Add(c);
        }
        public static void Scan(Category c, DateTime cutoff, Func<bool> cancel) {
            c.Files.Clear(); c.Skipped=0;
            if(Running(c.ProcessName)) { c.Skipped++; return; }
            foreach(string root in c.Roots) {
                if(cancel()) return;
                if(!Directory.Exists(root)) continue;
                if(!SafePath(System.IO.Path.Combine(root,"."),root)) {
                    // Root itself is allowed, but must not be a junction.
                    try { string p=Canonical(root); bool safe=true; while(!String.IsNullOrEmpty(p)) { if((File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0){safe=false;break;} p=System.IO.Path.GetDirectoryName(p); } if(!safe){c.Skipped++;continue;} } catch {c.Skipped++;continue;}
                }
                var pending=new Stack<string>(); pending.Push(root);
                while(pending.Count>0 && !cancel()) {
                    string dir=pending.Pop();
                    try {
                        foreach(string file in Directory.GetFiles(dir)) {
                            if(cancel()) return;
                            try { if(!SafePath(file,root)){c.Skipped++;continue;} var f=new FileInfo(file);
                                if(f.LastWriteTimeUtc>=cutoff || f.CreationTimeUtc>=cutoff) continue;
                                c.Files.Add(new Entry {Path=file,Root=root,Size=f.Length,Modified=f.LastWriteTimeUtc,Created=f.CreationTimeUtc});
                            } catch {c.Skipped++;}
                        }
                        foreach(string sub in Directory.GetDirectories(dir)) { if(SafePath(sub,root)) pending.Push(sub); else c.Skipped++; }
                    } catch { c.Skipped++; }
                }
            }
        }
        public static CleanResult Clean(List<Category> selected,DateTime cutoff,Func<bool> cancel) {
            var r=new CleanResult();
            foreach(var c in selected) {
                if(Running(c.ProcessName)) {r.Skipped+=c.Files.Count;continue;}
                foreach(var e in c.Files) {
                    if(cancel()) return r;
                    try {
                        // Revalidate the allowlist, links, age and identity at deletion time.
                        if(!c.Roots.Any(x=>String.Equals(Canonical(x),Canonical(e.Root),StringComparison.OrdinalIgnoreCase)) || !SafePath(e.Path,e.Root) || Running(c.ProcessName)) {r.Skipped++;continue;}
                        var f=new FileInfo(e.Path);
                        if(!f.Exists || f.Length!=e.Size || f.LastWriteTimeUtc!=e.Modified || f.CreationTimeUtc!=e.Created || f.LastWriteTimeUtc>=cutoff || f.CreationTimeUtc>=cutoff) {r.Skipped++;continue;}
                        if(!DeleteExclusive(e,cutoff)){r.Skipped++;continue;}
                        r.Deleted++; r.Bytes+=e.Size;
                    } catch {r.Skipped++;}
                }
            }
            return r;
        }
        public static string Size(long n) { string[] u={"B","KB","MB","GB","TB"}; double v=n; int i=0; while(v>=1024&&i<4){v/=1024;i++;} return v.ToString(i==0?"0":"0.0")+" "+u[i]; }
    }
    public static class Program {
        [STAThread] public static int Main(string[] args){
            if(args.Length>0&&args[0]=="--self-test")return Tests.Run(args.Length>1?args[1]:".");
            if(args.Length>0&&args[0]=="--scan-report") {var cats=Engine.Categories();foreach(var c in cats)Engine.Scan(c,DateTime.UtcNow.AddDays(-1),()=>false);File.WriteAllLines(args[1],cats.Select(c=>c.Name+": "+c.Files.Count+" files, "+Engine.Size(c.Bytes)+", "+c.Skipped+" skipped; browser running="+Engine.Running(c.ProcessName)));return 0;}
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            if(args.Length>0&&args[0]=="--ui-smoke"){using(var f=new MainWindow()){Task task=null;var timer=new System.Windows.Forms.Timer {Interval=100};f.Shown+=(s,e)=>{task=f.DoScan();timer.Start();};timer.Tick+=(s,e)=>{if(task!=null&&task.IsCompleted){timer.Stop();try{f.VerifyUiAndCapture(args[1]);}catch(Exception ex){File.WriteAllText(Path.ChangeExtension(args[1],"error.txt"),ex.ToString());Environment.ExitCode=1;}f.Close();}};Application.Run(f);timer.Dispose();}return Environment.ExitCode;}
            Application.Run(new MainWindow());return 0;
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
                var running=new Category {Roots=new List<string>{cache},ProcessName=Process.GetCurrentProcess().ProcessName};Engine.Scan(running,cutoff,()=>false);Assert(running.Files.Count==0&&running.Skipped>0,"Running process scan failed");running.Files=c.Files;Assert(Engine.Clean(new List<Category>{running},cutoff,()=>false).Deleted==0,"Running process clean failed");
                string hardlink=System.IO.Path.Combine(cache,"hardlink.tmp");Assert(CreateHardLink(hardlink,outside,IntPtr.Zero),"Hardlink fixture creation failed");Engine.Scan(c,cutoff,()=>false);var onlyLink=new Category {Roots=c.Roots,Files=c.Files.Where(x=>x.Path==hardlink).ToList()};Assert(onlyLink.Files.Count==1&&Engine.Clean(new List<Category>{onlyLink},cutoff,()=>false).Deleted==0,"Hardlink protection failed");
                string junction=System.IO.Path.Combine(System.IO.Path.GetFullPath(parent),"junction-fixture");Assert(Directory.Exists(junction),"Junction fixture is required");Assert(!Engine.SafePath(System.IO.Path.Combine(junction,"old.txt"),junction),"Junction accepted");var linked=new Category {Roots=new List<string>{junction}};Engine.Scan(linked,cutoff,()=>false);Assert(linked.Files.Count==0&&linked.Skipped>0,"Junction scan failed");
                File.WriteAllText(System.IO.Path.Combine(parent,"self-test.txt"),"PASS: age filtering; path boundary; sibling paths; changed-file protection; locked-file protection; personal-file preservation; forged-root rejection; scan and clean cancellation; running-process exclusions; hardlink protection; junction protection.\r\nFixtures: "+root);return 0;
            }catch(Exception ex){File.WriteAllText(System.IO.Path.Combine(parent,"self-test.txt"),"FAIL: "+ex);return 1;}
            // Fixtures are retained for inspection. No recursive removal is used.
        }
    }
}
