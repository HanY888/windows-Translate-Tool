using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace LinguaDesk {
public class HotkeySpec {
    public uint Modifiers=1;
    public int Key=(int)Keys.Q;
    public HotkeySpec Copy(){return new HotkeySpec{Modifiers=Modifiers,Key=Key};}
    public bool Same(HotkeySpec other){return other!=null&&Modifiers==other.Modifiers&&Key==other.Key;}
    [System.Web.Script.Serialization.ScriptIgnore] public string Display {get{var parts=new List<string>();if((Modifiers&2)!=0)parts.Add("Ctrl");if((Modifiers&1)!=0)parts.Add("Alt");if((Modifiers&4)!=0)parts.Add("Shift");parts.Add(Key>=(int)Keys.D0&&Key<=(int)Keys.D9?((char)Key).ToString():Key==(int)Keys.Return?"Enter":((Keys)Key).ToString());return String.Join("+",parts);}}
    public bool Matches(KeyEventArgs args){uint mods=(args.Alt?1u:0u)|(args.Control?2u:0u)|(args.Shift?4u:0u);return Key==(int)args.KeyCode&&Modifiers==mods;}
    public static HotkeySpec[] Defaults(){return new[]{new HotkeySpec{Key=(int)Keys.Q},new HotkeySpec{Key=(int)Keys.W},new HotkeySpec{Key=(int)Keys.E},new HotkeySpec{Modifiers=2,Key=(int)Keys.Enter}};}
    public static string Validate(HotkeySpec value){if(value==null)return "快捷键不能为空。";if((value.Modifiers&3)==0||(value.Modifiers&~7u)!=0)return "快捷键至少需要 Ctrl 或 Alt，可同时加上 Shift。";int key=value.Key;if(!((key>=(int)Keys.A&&key<=(int)Keys.Z)||(key>=(int)Keys.D0&&key<=(int)Keys.D9)||(key>=(int)Keys.F1&&key<=(int)Keys.F12)||key==(int)Keys.Enter))return "请选择字母、数字、F1–F12 或 Enter。";if(value.Modifiers==1&&key==(int)Keys.F4)return "Alt+F4 用于关闭窗口，请选择其他组合。";if(value.Modifiers==2&&key==(int)Keys.C)return "Ctrl+C 用于复制和读取选区，请选择其他组合。";return null;}
    public static string ValidateSet(HotkeySpec[] values){if(values==null||values.Length!=4)return "需要设置四项快捷键。";for(int i=0;i<values.Length;i++){string error=Validate(values[i]);if(error!=null)return error;for(int j=0;j<i;j++)if(values[i].Same(values[j]))return "不同功能不能使用相同的快捷键："+values[i].Display;}return null;}
}
// Stage new registrations before releasing old ones, including when swapping actions.
class HotkeyRegistry : IDisposable {
    class Binding { public int Id; public HotkeySpec Spec; }
    readonly Func<int,HotkeySpec,bool> register;readonly Action<int> unregister;Binding[] active=new Binding[3];int nextId=100;
    public HotkeyRegistry(Func<int,HotkeySpec,bool> add,Action<int> remove){register=add;unregister=remove;}
    public List<string> Initialize(HotkeySpec[] specs){var failures=new List<string>();for(int i=0;i<3;i++){int id=nextId++;if(register(id,specs[i]))active[i]=new Binding{Id=id,Spec=specs[i].Copy()};else failures.Add(specs[i].Display);}return failures;}
    public int ActionFor(int id){for(int i=0;i<3;i++)if(active[i]!=null&&active[i].Id==id)return i+1;return 0;}
    public bool TryApply(HotkeySpec[] desired,Action persist,out string error){error=HotkeySpec.ValidateSet(desired);if(error!=null)return false;var staged=new List<Binding>();var proposed=new Binding[3];try{for(int i=0;i<3;i++){proposed[i]=active.FirstOrDefault(b=>b!=null&&b.Spec.Same(desired[i]));if(proposed[i]!=null)continue;int id=nextId++;if(!register(id,desired[i])){error="快捷键 "+desired[i].Display+" 已被其他程序或系统占用，请换一个组合。原设置保持不变。";return false;}proposed[i]=new Binding{Id=id,Spec=desired[i].Copy()};staged.Add(proposed[i]);}try{persist();}catch(Exception ex){error="保存失败，原快捷键保持不变："+ex.Message;return false;}foreach(var binding in active)if(binding!=null&&!proposed.Contains(binding))unregister(binding.Id);active=proposed;staged.Clear();return true;}finally{foreach(var binding in staged)unregister(binding.Id);}}
    public void Dispose(){foreach(var binding in active)if(binding!=null)unregister(binding.Id);active=new Binding[3];}
}
class HotkeySettingsForm : Form {
    readonly CheckBox[] ctrl=new CheckBox[4],alt=new CheckBox[4],shift=new CheckBox[4];readonly ComboBox[] keys=new ComboBox[4];readonly Label feedback;readonly Func<HotkeySpec[],string> save;
    static readonly int[] AllowedKeys=Enumerable.Range((int)Keys.A,26).Concat(Enumerable.Range((int)Keys.D0,10)).Concat(Enumerable.Range((int)Keys.F1,12)).Concat(new[]{(int)Keys.Enter}).ToArray();
    public HotkeySettingsForm(HotkeySpec[] current,Func<HotkeySpec[],string> apply){save=apply;Text="快捷键设置";ClientSize=new Size(620,410);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterParent;Font=new Font("Microsoft YaHei UI",10);BackColor=Color.White;Icon=System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        var title=new Label{Text="设置你的快捷键",Font=new Font("Microsoft YaHei UI",17,FontStyle.Bold),Location=new Point(24,20),AutoSize=true};Controls.Add(title);Controls.Add(new Label{Text="勾选 Ctrl / Alt / Shift，再选择按键；保存后立即生效。",Location=new Point(26,64),AutoSize=true,ForeColor=Color.DimGray});
        string[] labels={"选词翻译（全局）","截图翻译（全局）","打开窗口（全局）","翻译文字（窗口内）"};
        for(int i=0;i<4;i++){int top=105+i*43;Controls.Add(new Label{Text=labels[i],Location=new Point(26,top+5),AutoSize=true});ctrl[i]=new CheckBox{Text="Ctrl",Location=new Point(216,top),Size=new Size(72,30)};alt[i]=new CheckBox{Text="Alt",Location=new Point(292,top),Size=new Size(64,30)};shift[i]=new CheckBox{Text="Shift",Location=new Point(360,top),Size=new Size(80,30)};keys[i]=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Location=new Point(458,top),Width=116};foreach(int key in AllowedKeys)keys[i].Items.Add(new HotkeySpec{Modifiers=0,Key=key}.Display);Controls.Add(ctrl[i]);Controls.Add(alt[i]);Controls.Add(shift[i]);Controls.Add(keys[i]);}
        SetValues(current);feedback=new Label{Text="至少包含 Ctrl 或 Alt；避免占用常用编辑快捷键。",Location=new Point(26,286),Size=new Size(568,52),ForeColor=Color.DimGray};Controls.Add(feedback);
        var reset=new Button{Text="恢复默认",Location=new Point(26,351),Size=new Size(118,36)};reset.Click+=(s,e)=>{SetValues(HotkeySpec.Defaults());feedback.Text="已填入默认组合，点击“保存并生效”后应用。";feedback.ForeColor=Color.DimGray;};Controls.Add(reset);
        var cancel=new Button{Text="取消",Location=new Point(322,351),Size=new Size(104,36),DialogResult=DialogResult.Cancel};Controls.Add(cancel);CancelButton=cancel;
        var button=new Button{Text="保存并生效",Location=new Point(442,351),Size=new Size(152,36),BackColor=Color.FromArgb(34,95,220),ForeColor=Color.White,FlatStyle=FlatStyle.Flat};button.Click+=(s,e)=>{var values=ReadValues();string error=HotkeySpec.ValidateSet(values);if(error==null)error=save(values);if(error!=null){feedback.Text=error;feedback.ForeColor=Color.Firebrick;return;}DialogResult=DialogResult.OK;Close();};Controls.Add(button);AcceptButton=button;
    }
    void SetValues(HotkeySpec[] values){for(int i=0;i<4;i++){ctrl[i].Checked=(values[i].Modifiers&2)!=0;alt[i].Checked=(values[i].Modifiers&1)!=0;shift[i].Checked=(values[i].Modifiers&4)!=0;keys[i].SelectedIndex=Array.IndexOf(AllowedKeys,values[i].Key);}}
    HotkeySpec[] ReadValues(){var values=new HotkeySpec[4];for(int i=0;i<4;i++)values[i]=new HotkeySpec{Modifiers=(ctrl[i].Checked?2u:0u)|(alt[i].Checked?1u:0u)|(shift[i].Checked?4u:0u),Key=keys[i].SelectedIndex<0?0:AllowedKeys[keys[i].SelectedIndex]};return values;}
}
}
