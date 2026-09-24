using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("随译 LinguaDesk 安装程序")]
[assembly: AssemblyProduct("LinguaDesk")]
[assembly: AssemblyVersion("1.3.0.0")]
[assembly: AssemblyFileVersion("1.3.0.0")]
namespace LinguaDeskInstaller {
static class Installer {
    [DllImport("shell32.dll")]static extern void SHChangeNotify(uint eventId,uint flags,IntPtr first,IntPtr second);
    public static readonly string InstallDir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","LinguaDesk");
    public const string RegistryPath=@"Software\Microsoft\Windows\CurrentVersion\Uninstall\LinguaDesk";
    public static string StartMenuDir {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"随译 LinguaDesk");}}
    public static string DesktopLink {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"随译 LinguaDesk.lnk");}}
    static readonly string[] Extensions={".txt",".md",".srt",".csv",".log",".docx",".pdf",".png",".jpg",".jpeg",".bmp",".tif",".tiff"};
    static readonly string[] Backgrounds={@"Software\Classes\DesktopBackground\Shell\LinguaDesk",@"Software\Classes\Directory\Background\shell\LinguaDesk"};
    static void Verb(string path,string label,string args){using(var key=Registry.CurrentUser.CreateSubKey(path)){key.SetValue("",label);key.SetValue("Icon",Path.Combine(InstallDir,"LinguaDesk.exe"));}using(var command=Registry.CurrentUser.CreateSubKey(path+@"\command"))command.SetValue("","\""+Path.Combine(InstallDir,"LinguaDesk.exe")+"\" "+args);}
    public static void RegisterMenus(){foreach(string ext in Extensions)Verb(@"Software\Classes\SystemFileAssociations\"+ext+@"\shell\LinguaDesk","用随译翻译","--file \"%1\"");foreach(string path in Backgrounds){using(var root=Registry.CurrentUser.CreateSubKey(path)){root.SetValue("MUIVerb","随译 LinguaDesk");root.SetValue("Icon",Path.Combine(InstallDir,"LinguaDesk.exe"));root.SetValue("SubCommands","");}Verb(path+@"\shell\01screen","截图翻译","--screenshot");Verb(path+@"\shell\02clipboard","翻译剪贴板","--clipboard");Verb(path+@"\shell\03open","打开随译","--show");}}
    public static void RemoveMenus(){foreach(string ext in Extensions)Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\SystemFileAssociations\"+ext+@"\shell\LinguaDesk",false);foreach(string path in Backgrounds)Registry.CurrentUser.DeleteSubKeyTree(path,false);}
    public static void Unpack(string target) {
        target=Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
        Directory.CreateDirectory(target);
        using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip")) {
            if(stream==null)throw new Exception("安装包内容缺失。");
            using(var zip=new ZipArchive(stream,ZipArchiveMode.Read)) {
                foreach(var entry in zip.Entries) {
                    string path=Path.GetFullPath(Path.Combine(target,entry.FullName));
                    if(!path.StartsWith(target,StringComparison.OrdinalIgnoreCase)||entry.FullName.IndexOfAny(new[]{'/', '\\', ':'})>=0)throw new Exception("安装包文件路径无效。");
                    using(var input=entry.Open())using(var output=File.Create(path))input.CopyTo(output);
                }
            }
        }
    }
    public static bool IsRunning(){foreach(var p in Process.GetProcessesByName("LinguaDesk")){using(p){try{if(String.Equals(p.MainModule.FileName,Path.Combine(InstallDir,"LinguaDesk.exe"),StringComparison.OrdinalIgnoreCase))return true;}catch{return true;}}}return false;}
    static void Shortcut(string path,string target,string arguments) {
        var shellType=Type.GetTypeFromProgID("WScript.Shell");object shell=Activator.CreateInstance(shellType);object link=null;
        try{link=shellType.InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{path});Type t=link.GetType();t.InvokeMember("TargetPath",BindingFlags.SetProperty,null,link,new object[]{target});t.InvokeMember("Arguments",BindingFlags.SetProperty,null,link,new object[]{arguments});t.InvokeMember("WorkingDirectory",BindingFlags.SetProperty,null,link,new object[]{InstallDir});t.InvokeMember("IconLocation",BindingFlags.SetProperty,null,link,new object[]{Path.Combine(InstallDir,"LinguaDesk.exe")+",0"});t.InvokeMember("Save",BindingFlags.InvokeMethod,null,link,null);}finally{if(link!=null)Marshal.FinalReleaseComObject(link);Marshal.FinalReleaseComObject(shell);}
    }
    public static void Install(bool desktop){
        if(IsRunning())throw new Exception("随译正在运行，请先右键托盘图标选择“退出”，再点击安装。");
        if(String.Equals(Path.GetFullPath(Application.ExecutablePath),Path.Combine(InstallDir,"Uninstall.exe"),StringComparison.OrdinalIgnoreCase))throw new Exception("请使用原始安装包更新程序。");
        Unpack(InstallDir);
        File.Copy(Application.ExecutablePath,Path.Combine(InstallDir,"Uninstall.exe"),true);
        Directory.CreateDirectory(StartMenuDir);Shortcut(Path.Combine(StartMenuDir,"随译 LinguaDesk.lnk"),Path.Combine(InstallDir,"LinguaDesk.exe"),"");Shortcut(Path.Combine(StartMenuDir,"卸载随译.lnk"),Path.Combine(InstallDir,"Uninstall.exe"),"--uninstall");if(desktop)Shortcut(DesktopLink,Path.Combine(InstallDir,"LinguaDesk.exe"),"");
        RegisterMenus();
        SHChangeNotify(0x08000000,0,IntPtr.Zero,IntPtr.Zero);
        using(var key=Registry.CurrentUser.CreateSubKey(RegistryPath)){key.SetValue("DisplayName","随译 LinguaDesk");key.SetValue("DisplayVersion","1.3.0");key.SetValue("Publisher","LinguaDesk");key.SetValue("InstallLocation",InstallDir);key.SetValue("DisplayIcon",Path.Combine(InstallDir,"LinguaDesk.exe"));key.SetValue("UninstallString","\""+Path.Combine(InstallDir,"Uninstall.exe")+"\" --uninstall");key.SetValue("NoModify",1);key.SetValue("NoRepair",1);key.SetValue("EstimatedSize",(int)(new FileInfo(Application.ExecutablePath).Length/1024+200));}
    }
    public static void BeginUninstall(){if(IsRunning())throw new Exception("随译正在运行，请先右键托盘图标选择“退出”，再卸载。");string temp=Path.Combine(Path.GetTempPath(),"LinguaDesk-Uninstall-"+Guid.NewGuid().ToString("N")+".exe");File.Copy(Application.ExecutablePath,temp);Process.Start(new ProcessStartInfo(temp,"--remove"){UseShellExecute=false});}
    public static void Remove(){
        using(var key=Registry.CurrentUser.OpenSubKey(RegistryPath)){if(key==null||!String.Equals(Convert.ToString(key.GetValue("InstallLocation")),InstallDir,StringComparison.OrdinalIgnoreCase))throw new Exception("未找到当前账户的有效安装记录。");}
        if(IsRunning())throw new Exception("随译正在运行，请退出后重试卸载。");
        using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip"))using(var zip=new ZipArchive(stream,ZipArchiveMode.Read)){foreach(var entry in zip.Entries){if(entry.FullName.IndexOfAny(new[]{'/','\\',':'})>=0)throw new Exception("文件路径无效。");File.Delete(Path.Combine(InstallDir,entry.FullName));}}
        string self=Path.Combine(InstallDir,"Uninstall.exe");for(int i=0;;i++){try{File.Delete(self);break;}catch(IOException){if(i>=20)throw;Thread.Sleep(250);}}
        if(Directory.Exists(InstallDir)&&Directory.GetFileSystemEntries(InstallDir).Length==0)Directory.Delete(InstallDir);
        File.Delete(DesktopLink);File.Delete(Path.Combine(StartMenuDir,"随译 LinguaDesk.lnk"));File.Delete(Path.Combine(StartMenuDir,"卸载随译.lnk"));if(Directory.Exists(StartMenuDir)&&Directory.GetFileSystemEntries(StartMenuDir).Length==0)Directory.Delete(StartMenuDir);
        using(var run=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run",true)){if(run!=null)run.DeleteValue("LinguaDesk",false);}RemoveMenus();Registry.CurrentUser.DeleteSubKey(RegistryPath,false);
    }
}
class SetupForm:Form {
    Button install;CheckBox desktop,launch;Label message;bool working;bool uninstall;bool complete;
    public SetupForm(bool remove){uninstall=remove;Text=remove?"卸载随译":"随译 LinguaDesk · 安装";Icon=System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);ClientSize=new Size(570,390);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;StartPosition=FormStartPosition.CenterScreen;Font=new Font("Microsoft YaHei UI",10);BackColor=Color.FromArgb(245,248,253);
        Controls.Add(new Label{Text=remove?"卸载随译":"随译，让理解更轻松。",Font=new Font("Microsoft YaHei UI",22,FontStyle.Bold),AutoSize=true,Location=new Point(30,28),ForeColor=Color.FromArgb(25,42,70)});
        Controls.Add(new Label{Text=remove?"删除程序与快捷方式；已保存的译文和个人设置会保留。":"选词翻译  /  截图识别  /  文件翻译\nWindows 10 / 11 · 64 位 · 无需管理员权限",AutoSize=true,Location=new Point(32,88),ForeColor=Color.DimGray});
        message=new Label{Text="安装位置：\n"+Installer.InstallDir,Location=new Point(32,152),Size=new Size(510,65),AutoEllipsis=true};Controls.Add(message);
        desktop=new CheckBox{Text="创建桌面快捷方式",Checked=true,AutoSize=true,Location=new Point(32,232),Visible=!remove};launch=new CheckBox{Text="安装完成后打开随译",Checked=true,AutoSize=true,Location=new Point(280,232),Visible=!remove};Controls.Add(desktop);Controls.Add(launch);
        var note=new Label{Text=remove?"可在卸载后重新安装，继续使用原有翻译服务设置。":"默认使用免费在线翻译；OCR 在本机进行，文字发送到所选服务。\n安装包不含任何个人 API Key。",Location=new Point(32,278),Size=new Size(510,48),ForeColor=Color.DimGray};Controls.Add(note);
        install=new Button{Text=remove?"卸载":"安装并开始使用",Location=new Point(360,335),Size=new Size(180,38),BackColor=Color.FromArgb(34,95,220),ForeColor=Color.White,FlatStyle=FlatStyle.Flat};Controls.Add(install);install.Click+=async(s,e)=>await Execute();FormClosing+=(s,e)=>{if(working)e.Cancel=true;};
    }
    async Task Execute(){if(complete){Close();return;}working=true;install.Enabled=false;desktop.Enabled=launch.Enabled=false;message.Text=uninstall?"正在准备卸载…":"正在安装…";try{if(uninstall){Installer.BeginUninstall();working=false;Close();return;}bool shortcut=desktop.Checked;await Task.Run(()=>Installer.Install(shortcut));message.Text="安装完成！\n在主窗口“快捷键设置”中可自定义组合键。";if(launch.Checked)Process.Start(new ProcessStartInfo(Path.Combine(Installer.InstallDir,"LinguaDesk.exe")){UseShellExecute=true});working=false;install.Text="完成";install.Enabled=true;complete=true;uninstall=false;desktop.Visible=launch.Visible=false;}catch(Exception ex){working=false;message.Text="未完成："+ex.Message;install.Enabled=true;desktop.Enabled=launch.Enabled=true;MessageBox.Show(this,ex.Message,"随译安装程序",MessageBoxButtons.OK,MessageBoxIcon.Information);}}
}
static class Entry {
    [DllImport("user32.dll")]static extern bool SetProcessDPIAware();
    [STAThread]static void Main(string[] args){SetProcessDPIAware();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);try{if(args.Length==2&&args[0]=="--verify"){Installer.Unpack(args[1]);return;}if(args.Length==1&&args[0]=="--remove"){Installer.Remove();MessageBox.Show("随译已卸载。个人设置与已保存的译文已保留。","随译");return;}Application.Run(new SetupForm(args.Length>0&&args[0]=="--uninstall"));}catch(Exception ex){MessageBox.Show(ex.Message,"随译安装程序",MessageBoxButtons.OK,MessageBoxIcon.Error);Environment.ExitCode=1;}}
}
}
