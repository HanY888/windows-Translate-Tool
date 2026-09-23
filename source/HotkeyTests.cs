using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows.Forms;
namespace LinguaDesk {
class HotkeyTests {
    static int passed;
    static void Check(bool value,string label){if(!value)throw new Exception(label);Console.WriteLine("PASS "+label);passed++;}
    class PreviewMain : MainForm { protected override bool ShowWithoutActivation {get{return true;}} protected override void OnShown(EventArgs e){} }
    class PreviewHotkeys : HotkeySettingsForm { public PreviewHotkeys():base(HotkeySpec.Defaults(),keys=>null){} protected override bool ShowWithoutActivation {get{return true;}} }
    static void Render(Form form,string path){using(form){form.StartPosition=FormStartPosition.Manual;form.Location=new System.Drawing.Point(-30000,-30000);form.ShowInTaskbar=false;form.Opacity=0;form.Show();form.PerformLayout();Application.DoEvents();using(var bitmap=new System.Drawing.Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new System.Drawing.Rectangle(0,0,form.Width,form.Height));bitmap.Save(path);}form.Hide();}}
    [STAThread]static int Main(string[] args){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);try{Run();if(args.Length==1){Render(new PreviewHotkeys(),System.IO.Path.Combine(args[0],"hotkey-dialog.png"));Render(new PreviewMain(),System.IO.Path.Combine(args[0],"main-window.png"));}Console.WriteLine("TOTAL "+passed+" passed");return 0;}catch(Exception ex){Console.WriteLine("FAIL "+ex);return 1;}}
    static void Run(){
        var serializer=new JavaScriptSerializer();var legacy=serializer.Deserialize<Settings>("{\"Provider\":\"MyMemory\"}");Check(legacy.Hotkeys.Length==4&&legacy.Hotkeys[0].Display=="Alt+Q","Existing configuration gains default hotkeys");
        var spec=new HotkeySpec{Modifiers=6,Key=(int)Keys.F8};Check(spec.Display=="Ctrl+Shift+F8"&&spec.Matches(new KeyEventArgs(Keys.Control|Keys.Shift|Keys.F8))&&!spec.Matches(new KeyEventArgs(Keys.Control|Keys.F8)),"Modifier display and exact matching");
        legacy.Hotkeys[0]=spec;var saved=serializer.Deserialize<Settings>(serializer.Serialize(legacy));Check(saved.Hotkeys[0].Same(spec),"Custom hotkeys survive configuration serialization");
        Check(HotkeySpec.Validate(new HotkeySpec{Modifiers=4,Key=(int)Keys.Q})!=null&&HotkeySpec.Validate(new HotkeySpec{Modifiers=2,Key=(int)Keys.C})!=null,"Unsafe unmodified and copy bindings rejected");
        var map=new Dictionary<int,HotkeySpec>();var taken=new List<HotkeySpec>();bool persisted=false;string error;
        using(var registry=new HotkeyRegistry((id,key)=>{if(map.Values.Any(k=>k.Same(key))||taken.Any(k=>k.Same(key)))return false;map.Add(id,key.Copy());return true;},id=>map.Remove(id))){
            var defaults=HotkeySpec.Defaults();Check(registry.Initialize(defaults).Count==0&&map.Count==3,"Three initial global registrations");
            var originalIds=map.Keys.ToArray();var duplicate=HotkeySpec.Defaults();duplicate[1]=duplicate[0].Copy();Check(!registry.TryApply(duplicate,()=>persisted=true,out error)&&!persisted&&map.Count==3,"Duplicate actions rejected without changing active keys");
            var desired=HotkeySpec.Defaults();desired[0]=new HotkeySpec{Modifiers=3,Key=(int)Keys.J};desired[1]=new HotkeySpec{Modifiers=3,Key=(int)Keys.K};taken.Add(desired[1]);Check(!registry.TryApply(desired,()=>persisted=true,out error)&&!persisted&&originalIds.All(map.ContainsKey)&&map.Count==3,"OS conflict rolls back staged keys and preserves original keys");taken.Clear();
            Check(!registry.TryApply(desired,()=>{throw new System.IO.IOException("disk unavailable");},out error)&&originalIds.All(map.ContainsKey)&&map.Count==3,"Save failure preserves original registrations");
            var swapped=HotkeySpec.Defaults();swapped[0]=defaults[1].Copy();swapped[1]=defaults[0].Copy();Check(registry.TryApply(swapped,()=>persisted=true,out error)&&persisted&&registry.ActionFor(originalIds[0])==2&&registry.ActionFor(originalIds[1])==1,"Swapping existing bindings updates dispatch without losing registrations");
            Check(registry.TryApply(desired,()=>{},out error)&&map.Values.Any(k=>k.Same(desired[0]))&&map.Count==3&&!map.ContainsKey(originalIds[0]),"Successful change replaces old bindings");
            var localOnly=desired.Select(k=>k.Copy()).ToArray();localOnly[3]=new HotkeySpec{Modifiers=6,Key=(int)Keys.T};var ids=map.Keys.ToArray();Check(registry.TryApply(localOnly,()=>{},out error)&&ids.All(map.ContainsKey),"Window-only shortcut change leaves global registrations intact");
        }Check(map.Count==0,"Dispose unregisters all active hotkeys");
        using(var form=new HotkeySettingsForm(HotkeySpec.Defaults(),keys=>null)){Check(form.Controls.OfType<ComboBox>().Count()==4&&form.Controls.OfType<CheckBox>().Count()==12,"Settings dialog exposes four independently configurable actions");}
        // Real OS conflict probe, on hidden test-only windows. No UI input or user settings.
        using(var window=new Form())using(var blocker=new Form()){
            IntPtr handle=window.Handle,other=blocker.Handle;var available=new List<int>();
            for(int key=(int)Keys.F1;key<=(int)Keys.F11&&available.Count<5;key++){if(Native.RegisterHotKey(other,9000,0x4007,(uint)key)){Native.UnregisterHotKey(other,9000);available.Add(key);}}
            Check(available.Count>=5,"Found unused test-only modifier combinations");
            var nativeKeys=HotkeySpec.Defaults();for(int i=0;i<3;i++)nativeKeys[i]=new HotkeySpec{Modifiers=7,Key=available[i]};
            using(var registry=new HotkeyRegistry((id,key)=>Native.RegisterHotKey(handle,id,0x4000|key.Modifiers,(uint)key.Key),id=>Native.UnregisterHotKey(handle,id))){
                Check(registry.Initialize(nativeKeys).Count==0,"Windows accepts configured global hotkeys");
                if(!Native.RegisterHotKey(other,9100,0x4007,(uint)available[3]))throw new Exception("Could not reserve test conflict");
                try{var desired=nativeKeys.Select(k=>k.Copy()).ToArray();desired[0].Key=available[4];desired[1].Key=available[3];Check(!registry.TryApply(desired,()=>{},out error),"Windows hotkey conflict reported");bool oldTaken=!Native.RegisterHotKey(other,9101,0x4007,(uint)available[0]);if(!oldTaken)Native.UnregisterHotKey(other,9101);Check(oldTaken,"Original shortcut remains registered after Windows conflict");bool stagedFree=Native.RegisterHotKey(other,9102,0x4007,(uint)available[4]);if(stagedFree)Native.UnregisterHotKey(other,9102);Check(stagedFree,"Staged Windows registration is released on failure");}finally{Native.UnregisterHotKey(other,9100);}
            }
        }
    }
}
}
