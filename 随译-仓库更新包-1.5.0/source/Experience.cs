using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
namespace LinguaDesk {
public class HistoryEntry {
 public string Id=Guid.NewGuid().ToString("N"),Original,Translated,Source,Target;
 public DateTime Time=DateTime.Now;
 public override string ToString(){return Time.ToString("MM-dd HH:mm")+"  "+Original.Replace('\r',' ').Replace('\n',' ').Substring(0,Math.Min(65,Original.Length));}
}
public class HistoryStore {
 readonly string path; public List<HistoryEntry> Items=new List<HistoryEntry>();
 public HistoryStore(string file){path=file;if(File.Exists(path)){var json=new JavaScriptSerializer{MaxJsonLength=16000000};Items=json.Deserialize<List<HistoryEntry>>(File.ReadAllText(path))??new List<HistoryEntry>();Items=Items.Where(x=>x!=null&&x.Original!=null&&x.Translated!=null).Take(200).ToList();}}
 public void Save(){Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+".tmp";try{File.WriteAllText(temp,new JavaScriptSerializer{MaxJsonLength=16000000}.Serialize(Items),new UTF8Encoding(false));if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}}
 public bool Add(HistoryEntry entry){if(entry.Original.Length+entry.Translated.Length>100000)return false;var next=Items.ToList();next.Insert(0,entry);while(next.Count>200||next.Sum(x=>(long)x.Original.Length+x.Translated.Length)>1000000)next.RemoveAt(next.Count-1);var old=Items;Items=next;try{Save();return true;}catch{Items=old;throw;}}
 public void Delete(string id){var old=Items;Items=Items.Where(x=>x.Id!=id).ToList();try{Save();}catch{Items=old;throw;}}
 public void Clear(){var old=Items;Items=new List<HistoryEntry>();try{Save();}catch{Items=old;throw;}}
}
class HistoryForm:Form {
 public HistoryEntry Selected; ListBox list; RichTextBox preview; TextBox search;
 public HistoryForm(HistoryStore store){Text="翻译历史 · 仅保存在本机";Size=new Size(860,580);MinimumSize=new Size(660,420);StartPosition=FormStartPosition.CenterParent;Font=new Font("Microsoft YaHei UI",10);BackColor=Color.White;
 var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(18),RowCount=3,ColumnCount=1};root.RowStyles.Add(new RowStyle(SizeType.Absolute,38));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,44));Controls.Add(root);
 var searchRow=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2};searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,120));searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));searchRow.Controls.Add(new Label{Text="搜索原文 / 译文",AutoSize=true},0,0);search=new TextBox{Dock=DockStyle.Fill,AccessibleName="搜索原文或译文"};searchRow.Controls.Add(search,1,0);root.Controls.Add(searchRow,0,0);
 var panes=new SplitContainer{Dock=DockStyle.Fill,SplitterDistance=330};list=new ListBox{Dock=DockStyle.Fill,IntegralHeight=false};preview=new RichTextBox{Dock=DockStyle.Fill,ReadOnly=true,BackColor=Color.White,BorderStyle=BorderStyle.FixedSingle};panes.Panel1.Controls.Add(list);panes.Panel2.Controls.Add(preview);root.Controls.Add(panes,0,1);
 Action refresh=()=>{list.BeginUpdate();list.Items.Clear();foreach(var item in store.Items.Where(x=>(x.Original+"\n"+x.Translated).IndexOf(search.Text,StringComparison.OrdinalIgnoreCase)>=0))list.Items.Add(item);list.EndUpdate();preview.Clear();if(list.Items.Count>0)list.SelectedIndex=0;};
 list.SelectedIndexChanged+=(s,e)=>{var item=list.SelectedItem as HistoryEntry;preview.Text=item==null?"":item.Original+"\n\n—— 译文 ——\n\n"+item.Translated;};search.TextChanged+=(s,e)=>refresh();
 Action restore=()=>{Selected=list.SelectedItem as HistoryEntry;if(Selected!=null){DialogResult=DialogResult.OK;Close();}};list.DoubleClick+=(s,e)=>restore();
 var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill};root.Controls.Add(buttons,0,2);
 foreach(var label in new[]{"恢复到主窗口","删除选中","清空历史"}){var b=new Button{Text=label,AutoSize=true,Height=34};buttons.Controls.Add(b);b.Click+=(s,e)=>{try{if(b.Text=="恢复到主窗口")restore();else if(b.Text=="删除选中"){var item=list.SelectedItem as HistoryEntry;if(item!=null)store.Delete(item.Id);refresh();}else if(MessageBox.Show(this,"确定删除全部历史记录？","清空历史",MessageBoxButtons.YesNo)==DialogResult.Yes){store.Clear();refresh();}}catch(Exception ex){MessageBox.Show(this,"历史记录未修改："+ex.Message);}};}
 refresh();}
}
static class OcrWorker {
 public static string LastNotice="";
 static readonly SemaphoreSlim Gate=new SemaphoreSlim(1,1);static Process process;
 public static void Stop(){var old=process;process=null;if(old!=null){try{if(!old.HasExited)old.Kill();}catch{}old.Dispose();}}
 public static async Task<string> Read(string path,string language,CancellationToken token,bool enhance=false){await Gate.WaitAsync(token).ConfigureAwait(false);try{
 if(process==null||process.HasExited){Stop();string helper=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ocr.ps1");var info=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),@"WindowsPowerShell\v1.0\powershell.exe"),"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \""+helper+"\" -Worker"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8};process=Process.Start(info);process.ErrorDataReceived+=(s,e)=>{};process.BeginErrorReadLine();}
 LastNotice="";var active=process;using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(token)){timeout.CancelAfter(TimeSpan.FromMinutes(3));using(timeout.Token.Register(()=>{try{active.Kill();}catch{}})){
 var json=new JavaScriptSerializer{MaxJsonLength=16000000};string request=json.Serialize(new{InputPath=path,Language=language,Enhance=enhance});string encoded=Convert.ToBase64String(Encoding.UTF8.GetBytes(request));active.StandardInput.WriteLine(encoded);active.StandardInput.Flush();string line=await active.StandardOutput.ReadLineAsync().ConfigureAwait(false);token.ThrowIfCancellationRequested();if(timeout.IsCancellationRequested)throw new Exception("识别超时，请缩小截图区域后重试。");if(line==null)throw new Exception("识别进程已结束，请重新截图。");var response=json.Deserialize<Dictionary<string,object>>(Encoding.UTF8.GetString(Convert.FromBase64String(line)));if(!(bool)response["ok"])throw new Exception(Convert.ToString(response["error"]));LastNotice=response.ContainsKey("notice")?Convert.ToString(response["notice"]):"";string result=Convert.ToString(response["text"]);if(String.IsNullOrWhiteSpace(result))throw new Exception("未识别到文字，请检查原文语言或框选更清晰的区域。");return result;
 }}}catch{Stop();token.ThrowIfCancellationRequested();throw;}finally{Gate.Release();}}
}
}
namespace LinguaDesk {
class SurfaceCard:System.Windows.Forms.Panel {
 public SurfaceCard(){DoubleBuffered=true;BackColor=System.Drawing.Color.White;Padding=new System.Windows.Forms.Padding(20);}
 protected override void OnPaint(System.Windows.Forms.PaintEventArgs e){base.OnPaint(e);e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;using(var path=Round(new System.Drawing.Rectangle(0,0,Width-1,Height-1),14))using(var pen=new System.Drawing.Pen(System.Drawing.Color.FromArgb(226,232,240)))e.Graphics.DrawPath(pen,path);}
 internal static System.Drawing.Drawing2D.GraphicsPath Round(System.Drawing.Rectangle r,int radius){var p=new System.Drawing.Drawing2D.GraphicsPath();int d=radius*2;p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
}
class SoftButton:System.Windows.Forms.Button {
 bool hover;
 public SoftButton(){SetStyle(System.Windows.Forms.ControlStyles.UserPaint|System.Windows.Forms.ControlStyles.OptimizedDoubleBuffer|System.Windows.Forms.ControlStyles.AllPaintingInWmPaint,true);FlatStyle=System.Windows.Forms.FlatStyle.Flat;FlatAppearance.BorderSize=0;Cursor=System.Windows.Forms.Cursors.Hand;}
 protected override void OnMouseEnter(System.EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
 protected override void OnMouseLeave(System.EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
 protected override void OnPaint(System.Windows.Forms.PaintEventArgs e){
 e.Graphics.Clear(Parent==null?System.Drawing.Color.White:Parent.BackColor);e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
 var fill=Enabled?BackColor:System.Drawing.Color.FromArgb(241,244,248);if(hover&&Enabled)fill=System.Windows.Forms.ControlPaint.Dark(fill,0.04f);
 using(var path=SurfaceCard.Round(new System.Drawing.Rectangle(0,0,Width-1,Height-1),8))using(var brush=new System.Drawing.SolidBrush(fill))e.Graphics.FillPath(brush,path);
 System.Windows.Forms.TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,Enabled?ForeColor:System.Drawing.Color.FromArgb(155,163,175),System.Windows.Forms.TextFormatFlags.HorizontalCenter|System.Windows.Forms.TextFormatFlags.VerticalCenter|System.Windows.Forms.TextFormatFlags.EndEllipsis);
 if(Focused&&ShowFocusCues)System.Windows.Forms.ControlPaint.DrawFocusRectangle(e.Graphics,new System.Drawing.Rectangle(4,4,Width-9,Height-9));
 }
}
}
namespace LinguaDesk {
partial class MainForm {
 void Redesign(System.Windows.Forms.TableLayoutPanel oldRoot,System.Windows.Forms.FlowLayoutPanel toolbar,System.Windows.Forms.FlowLayoutPanel actions,System.Windows.Forms.FlowLayoutPanel langs,System.Windows.Forms.FlowLayoutPanel reading,System.Windows.Forms.Button settingsMenu){
 SuspendLayout();oldRoot.Visible=false;
 BackColor=System.Drawing.Color.FromArgb(247,249,252);MinimumSize=new System.Drawing.Size(620,720);Size=new System.Drawing.Size(660,840);
 var shell=new System.Windows.Forms.TableLayoutPanel{Dock=System.Windows.Forms.DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=System.Windows.Forms.Padding.Empty};
 shell.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute,0));shell.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent,100));
 var side=new System.Windows.Forms.Panel{Dock=System.Windows.Forms.DockStyle.Fill,BackColor=System.Drawing.Color.FromArgb(235,240,249),Padding=new System.Windows.Forms.Padding(16,28,16,20)};
 var brand=new System.Windows.Forms.Label{Text="随译",Font=new System.Drawing.Font("Microsoft YaHei UI",22,System.Drawing.FontStyle.Bold),ForeColor=System.Drawing.Color.FromArgb(27,45,76),Dock=System.Windows.Forms.DockStyle.Top,Height=48};
 var caption=new System.Windows.Forms.Label{Text="桌面翻译助手",Dock=System.Windows.Forms.DockStyle.Top,Height=42,ForeColor=System.Drawing.Color.FromArgb(106,121,145)};
 var nav=new System.Windows.Forms.FlowLayoutPanel{Dock=System.Windows.Forms.DockStyle.Top,Height=172,FlowDirection=System.Windows.Forms.FlowDirection.TopDown,WrapContents=false};
 var active=new SoftButton{Text="文字翻译",Width=140,Height=42,BackColor=System.Drawing.Color.FromArgb(220,231,255),ForeColor=System.Drawing.Color.FromArgb(36,87,194),Margin=new System.Windows.Forms.Padding(0,0,0,10)};active.Click+=(s,e)=>input.Focus();nav.Controls.Add(active);
 var historyButton=toolbar.Controls.OfType<System.Windows.Forms.Button>().First(x=>x.Text=="历史记录");historyButton.Width=140;historyButton.Height=42;historyButton.BackColor=side.BackColor;nav.Controls.Add(historyButton);settingsMenu.Width=140;settingsMenu.Height=42;settingsMenu.BackColor=side.BackColor;nav.Controls.Add(settingsMenu);
 var sideFoot=new System.Windows.Forms.Label{Text="随译 1.5.0\n文字识别在本机完成",Dock=System.Windows.Forms.DockStyle.Bottom,Height=48,ForeColor=System.Drawing.Color.FromArgb(111,125,146),Font=new System.Drawing.Font("Microsoft YaHei UI",9)};
 side.Controls.Add(nav);side.Controls.Add(caption);side.Controls.Add(brand);side.Controls.Add(sideFoot);shell.Controls.Add(side,0,0);
 var main=new System.Windows.Forms.TableLayoutPanel{Dock=System.Windows.Forms.DockStyle.Fill,Padding=new System.Windows.Forms.Padding(18,16,18,12),ColumnCount=1,RowCount=6};
 foreach(int height in new[]{56,48,46})main.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute,height));main.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent,100));main.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute,56));main.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute,30));
 var heading=new System.Windows.Forms.Panel{Dock=System.Windows.Forms.DockStyle.Fill};
 heading.Controls.Add(new System.Windows.Forms.Label{Text="随译",AutoSize=true,Font=new System.Drawing.Font("Microsoft YaHei UI",19,System.Drawing.FontStyle.Bold),ForeColor=System.Drawing.Color.FromArgb(29,42,65),Location=new System.Drawing.Point(0,0)});
 heading.Controls.Add(new System.Windows.Forms.Label{Text="随选 · 随译",AutoSize=true,ForeColor=System.Drawing.Color.FromArgb(115,128,148),Location=new System.Drawing.Point(94,13)});main.Controls.Add(heading,0,0);toolbar.Controls.Add(historyButton);toolbar.Controls.Add(settingsMenu);historyButton.Dock=settingsMenu.Dock=System.Windows.Forms.DockStyle.None;
 toolbar.Padding=System.Windows.Forms.Padding.Empty;toolbar.Margin=System.Windows.Forms.Padding.Empty;main.Controls.Add(toolbar,0,1);
 foreach(System.Windows.Forms.Button b in toolbar.Controls.OfType<System.Windows.Forms.Button>()){b.Width=102;b.Height=36;b.Margin=new System.Windows.Forms.Padding(0,0,8,0);b.BackColor=System.Drawing.Color.White;b.ForeColor=System.Drawing.Color.FromArgb(54,70,96);}
 langs.Padding=new System.Windows.Forms.Padding(0,6,0,0);langs.Margin=System.Windows.Forms.Padding.Empty;source.Width=178;providerLabel.Visible=false;source.FlatStyle=target.FlatStyle=System.Windows.Forms.FlatStyle.Flat;source.BackColor=target.BackColor=System.Drawing.Color.White;main.Controls.Add(langs,0,2);
 var cards=new System.Windows.Forms.TableLayoutPanel{Dock=System.Windows.Forms.DockStyle.Fill,ColumnCount=1,RowCount=2,Margin=System.Windows.Forms.Padding.Empty};cards.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent,100));cards.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent,42));cards.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent,58));
 var clear=actions.Controls.OfType<System.Windows.Forms.Button>().First(x=>x.Text=="清空");var retry=actions.Controls.OfType<System.Windows.Forms.Button>().First(x=>x.Text=="增强重识别");var copy=actions.Controls.OfType<System.Windows.Forms.Button>().First(x=>x.Text=="复制译文");var save=actions.Controls.OfType<System.Windows.Forms.Button>().First(x=>x.Text=="另存译文");
 for(int i=0;i<2;i++){
 bool original=i==0;var box=original?input:output;
 var card=new SurfaceCard{Dock=System.Windows.Forms.DockStyle.Fill,Margin=original?new System.Windows.Forms.Padding(0,0,0,8):new System.Windows.Forms.Padding(0,0,0,0)};
 var layout=new System.Windows.Forms.TableLayoutPanel{Dock=System.Windows.Forms.DockStyle.Fill,ColumnCount=1,RowCount=3,BackColor=System.Drawing.Color.White};layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute,36));layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent,100));layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute,44));
 var title=new System.Windows.Forms.Label{Text=original?"原文":"译文",Dock=System.Windows.Forms.DockStyle.Fill,ForeColor=System.Drawing.Color.FromArgb(90,105,127),Font=new System.Drawing.Font("Microsoft YaHei UI",10,System.Drawing.FontStyle.Bold)};layout.Controls.Add(title,0,0);
 var body=new System.Windows.Forms.Panel{Dock=System.Windows.Forms.DockStyle.Fill,BackColor=System.Drawing.Color.White};box.BorderStyle=System.Windows.Forms.BorderStyle.None;box.BackColor=System.Drawing.Color.White;box.ForeColor=System.Drawing.Color.FromArgb(34,48,70);box.Dock=System.Windows.Forms.DockStyle.Fill;body.Controls.Add(box);
 var placeholder=new System.Windows.Forms.Label{Text=original?"在这里输入或粘贴文字\n\n也可以截图，或拖入文件":"译文将在这里呈现\n\n开始翻译后，可复制或保存结果",ForeColor=System.Drawing.Color.FromArgb(154,166,184),Font=new System.Drawing.Font("Microsoft YaHei UI",12),Dock=System.Windows.Forms.DockStyle.Fill,Padding=new System.Windows.Forms.Padding(0,22,0,0),BackColor=System.Drawing.Color.White};
 placeholder.Click+=(s,e)=>{placeholder.Visible=false;box.Focus();};body.Controls.Add(placeholder);
 System.Action update=()=>{title.Text=(original?"原文":"译文 · "+(settings.Provider=="MyMemory"?"免费翻译":settings.Model))+"   ·   "+box.TextLength+" 字符";placeholder.Visible=box.TextLength==0&&!box.Focused;if(placeholder.Visible)placeholder.BringToFront();};box.TextChanged+=(s,e)=>update();box.GotFocus+=(s,e)=>update();box.LostFocus+=(s,e)=>update();update();layout.Controls.Add(body,0,1);
 var foot=new System.Windows.Forms.FlowLayoutPanel{Dock=System.Windows.Forms.DockStyle.Fill,Padding=new System.Windows.Forms.Padding(0,8,0,0),BackColor=System.Drawing.Color.White,WrapContents=false};
 foreach(var b in original?new[]{clear,retry}:new[]{copy,save}){b.BackColor=System.Drawing.Color.FromArgb(245,247,251);b.ForeColor=System.Drawing.Color.FromArgb(77,93,118);b.Height=34;foot.Controls.Add(b);}
 layout.Controls.Add(foot,0,2);card.Controls.Add(layout);cards.Controls.Add(card,0,i);
 }
  main.Controls.Add(cards,0,3);
 bool expanded=false;
 var expand=new SoftButton{Text="展开对照",Width=96,Height=34,BackColor=System.Drawing.Color.White,ForeColor=System.Drawing.Color.FromArgb(77,93,118),Anchor=System.Windows.Forms.AnchorStyles.Top|System.Windows.Forms.AnchorStyles.Right};
 heading.Controls.Add(expand);heading.Resize+=(s,e)=>expand.Location=new System.Drawing.Point(heading.Width-expand.Width,4);
 expand.Click+=(s,e)=>{expanded=!expanded;var first=cards.Controls[0];var second=cards.Controls[1];cards.SuspendLayout();cards.Controls.Remove(first);cards.Controls.Remove(second);cards.ColumnStyles.Clear();cards.RowStyles.Clear();cards.ColumnCount=expanded?2:1;cards.RowCount=expanded?1:2;if(expanded){cards.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent,50));cards.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent,50));cards.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent,100));first.Margin=new System.Windows.Forms.Padding(0,0,8,0);second.Margin=new System.Windows.Forms.Padding(8,0,0,0);}else{cards.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent,100));cards.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent,42));cards.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent,58));first.Margin=new System.Windows.Forms.Padding(0,0,0,8);second.Margin=System.Windows.Forms.Padding.Empty;}cards.Controls.Add(first,0,0);cards.Controls.Add(second,expanded?1:0,expanded?0:1);MinimumSize=new System.Drawing.Size(expanded?1060:620,720);Size=new System.Drawing.Size(expanded?1200:660,840);expand.Text=expanded?"紧凑面板":"展开对照";cards.ResumeLayout(true);};
 var bottom=new System.Windows.Forms.Panel{Dock=System.Windows.Forms.DockStyle.Fill,Padding=new System.Windows.Forms.Padding(0,12,0,0)};
 reading.Dock=System.Windows.Forms.DockStyle.Left;reading.Width=240;reading.Padding=System.Windows.Forms.Padding.Empty;bottom.Controls.Add(reading);
 var primary=new System.Windows.Forms.FlowLayoutPanel{Dock=System.Windows.Forms.DockStyle.Right,Width=300,FlowDirection=System.Windows.Forms.FlowDirection.RightToLeft,WrapContents=false};translateButton.Width=204;translateButton.Height=40;translateButton.BackColor=System.Drawing.Color.FromArgb(46,99,226);translateButton.ForeColor=System.Drawing.Color.White;translateButton.Margin=System.Windows.Forms.Padding.Empty;cancelButton.Width=76;cancelButton.Height=40;cancelButton.Margin=new System.Windows.Forms.Padding(0,0,12,0);primary.Controls.Add(translateButton);primary.Controls.Add(cancelButton);bottom.Controls.Add(primary);main.Controls.Add(bottom,0,4);
 elapsedLabel.Visible=false;status.Margin=System.Windows.Forms.Padding.Empty;status.ForeColor=System.Drawing.Color.FromArgb(119,131,150);status.Font=new System.Drawing.Font("Microsoft YaHei UI",9);main.Controls.Add(status,0,5);
 shell.Controls.Add(main,1,0);Controls.Add(shell);shell.BringToFront();var tips=new System.Windows.Forms.ToolTip();tips.SetToolTip(expand,"切换紧凑面板与左右对照");tips.SetToolTip(translateButton,"回车翻译；Shift（上档键）+ Enter（回车键）换行");tips.SetToolTip(retry,"重新识别最近一次截图");Disposed+=(s,e)=>tips.Dispose();ResumeLayout(true);
 }
}
}
