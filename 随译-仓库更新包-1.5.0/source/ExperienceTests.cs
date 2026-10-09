using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using System.Diagnostics;
namespace LinguaDesk {
class ExperienceTests {
 static int count;static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);count++;}
 [STAThread]static void Main(string[] args){try{
 string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"history-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
 string path=Path.Combine(folder,"history.json");var store=new HistoryStore(path);
 store.Add(new HistoryEntry{Original="hello",Translated="你好",Source="en",Target="zh-CN"});
 Check(new HistoryStore(path).Items.Count==1,"History survives restart");
 string id=store.Items[0].Id;store.Add(new HistoryEntry{Original="second",Translated="第二"});store.Delete(id);Check(store.Items.Count==1&&new HistoryStore(path).Items.Count==1,"Delete persists");
 Check(!store.Add(new HistoryEntry{Original=new string('a',100001),Translated=""}),"Large document does not overload history");
 store.Clear();Check(new HistoryStore(path).Items.Count==0,"Clear persists");
 for(int i=0;i<205;i++)store.Add(new HistoryEntry{Original="entry "+i,Translated="译文"});
 Check(store.Items.Count==200&&store.Items[0].Original=="entry 204","History limit and newest-first ordering");
 using(var dialog=new HistoryForm(store)){Check(dialog.Controls.Count>0,"History dialog constructs");}
 File.WriteAllText(path,"invalid");try{new HistoryStore(path);throw new Exception("Expected corruption error");}catch(ArgumentException){Check(File.ReadAllText(path)=="invalid","Corrupt history remains intact");}
 SynchronizationContext.SetSynchronizationContext(null);
 string fixture=args[0];
 long[] times=new long[3];
 for(int i=0;i<3;i++){var watch=Stopwatch.StartNew();string text=OcrWorker.Read(fixture,"zh-CN",CancellationToken.None).GetAwaiter().GetResult();times[i]=watch.ElapsedMilliseconds;Check(text.Replace(" ","").Trim()=="小字截图翻译测试12345","Native worker Chinese recognition "+i);}
 Console.WriteLine("OCR cold="+times[0]+"ms warm="+times[1]+","+times[2]+"ms");
 OcrWorker.Stop();
 using(var cancel=new CancellationTokenSource()){cancel.CancelAfter(10);try{OcrWorker.Read(fixture,"zh-CN",cancel.Token).GetAwaiter().GetResult();throw new Exception("Cancel ignored");}catch(OperationCanceledException){Check(true,"Native worker cancels");}}
 Check(OcrWorker.Read(fixture,"zh-CN",CancellationToken.None).GetAwaiter().GetResult().Contains("截图"),"Worker restarts after cancellation");
 Check(OcrWorker.Read(fixture,"zh-CN",CancellationToken.None,true).GetAwaiter().GetResult().Replace(" ","").Trim()=="小字截图翻译测试12345","Enhanced recognition preserves test text");Check(OcrWorker.Read(fixture,"zz-ZZ",CancellationToken.None).GetAwaiter().GetResult().Contains("截图")&&!String.IsNullOrEmpty(OcrWorker.LastNotice),"Unavailable language falls back with notice");
 OcrWorker.Read(fixture,"zh-CN",CancellationToken.None).GetAwaiter().GetResult();Check(String.IsNullOrEmpty(OcrWorker.LastNotice),"Fallback notice resets on next successful language");
 OcrWorker.Stop();Console.WriteLine("TOTAL "+count+" passed");
 }catch(Exception ex){OcrWorker.Stop();Console.WriteLine(ex);Environment.ExitCode=1;}}
}
}
