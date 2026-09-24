using System;
using System.Reflection;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace LinguaDesk {
class FeatureTests {
 static int count;
 static void Check(bool ok,string label){if(!ok)throw new Exception(label);Console.WriteLine("PASS "+label);count++;}
 static object Field(object o,string n){return o.GetType().GetField(n,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);}
 class Handler:HttpMessageHandler{
 public int Calls;public bool Fail;
 protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){Calls++;await Task.Delay(40,token);return new HttpResponseMessage(Fail?(HttpStatusCode)429:HttpStatusCode.OK){Content=new StringContent("{\"choices\":[{\"message\":{\"content\":\"译文\"}}]}")};}
 }
 [STAThread] static void Main(){try{
 using(var form=new MainForm()){
 var src=(ComboBox)Field(form,"source");var dst=(ComboBox)Field(form,"target");var input=(RichTextBox)Field(form,"input");var output=(RichTextBox)Field(form,"output");
 var swap=typeof(MainForm).GetMethod("SwapDirection",BindingFlags.NonPublic|BindingFlags.Instance);
 src.SelectedItem="英语";dst.SelectedItem="简体中文";input.Text="hello";output.Text="你好";swap.Invoke(form,new object[]{false});
 Check((string)src.SelectedItem=="简体中文"&&(string)dst.SelectedItem=="英语"&&input.Text=="你好"&&output.Text=="hello","Languages and text swap together");
 swap.Invoke(form,new object[]{false});Check(input.Text=="hello"&&output.Text=="你好","Double swap restores content");
 src.SelectedItem="自动识别语言";swap.Invoke(form,new object[]{false});Check((string)dst.SelectedItem=="英语","Automatic source resolves before swap");
 src.SelectedItem="自动识别语言";input.Clear();swap.Invoke(form,new object[]{false});Check((string)src.SelectedItem=="自动识别语言","Empty automatic source does not change direction");
 }
 using(var form=new MainForm(new[]{"--startup"})){form.Show();Check(!form.Visible,"Startup remains hidden");}
 Check(Startup.Command(@"C:\A B\LinguaDesk.exe")=="\"C:\\A B\\LinguaDesk.exe\" --startup","Startup command quotes spaced paths");
 SynchronizationContext.SetSynchronizationContext(null);var handler=new Handler();typeof(Translation).GetField("Client",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,new HttpClient(handler));
 var settings=new Settings{Provider="Custom",Endpoint="http://localhost/translation",Model="test"};
 var watch=System.Diagnostics.Stopwatch.StartNew();
 Translation.Segment("hello","en","zh-CN",settings,CancellationToken.None).GetAwaiter().GetResult();long first=watch.ElapsedMilliseconds;watch.Restart();
 Translation.Segment("hello","en","zh-CN",settings,CancellationToken.None).GetAwaiter().GetResult();
 Check(handler.Calls==1,"Repeated translation avoids network");Console.WriteLine("TIMING first="+first+"ms cached="+watch.ElapsedMilliseconds+"ms (simulated service)");
 Translation.Segment("hello","en","ja",settings,CancellationToken.None).GetAwaiter().GetResult();Check(handler.Calls==2,"Target language separates cache");
 settings.Model="different";Translation.Segment("hello","en","ja",settings,CancellationToken.None).GetAwaiter().GetResult();Check(handler.Calls==3,"Model separates cache");
 var cancel=new CancellationTokenSource();cancel.Cancel();try{Translation.Segment("hello","en","ja",settings,cancel.Token).GetAwaiter().GetResult();throw new Exception("cancel ignored");}catch(OperationCanceledException){Check(true,"Cancellation applies to cached results");}
 handler.Fail=true;for(int i=0;i<2;i++)try{Translation.Segment("failure","en","ja",settings,CancellationToken.None).GetAwaiter().GetResult();throw new Exception("expected error");}catch(Exception ex){if(!ex.Message.Contains("429"))throw;}
 Check(handler.Calls==5,"Failures are never cached");
 Console.WriteLine("TOTAL "+count+" passed");
 }catch(Exception ex){Console.WriteLine(ex);Environment.ExitCode=1;}}
}
}


