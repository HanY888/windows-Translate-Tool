using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
namespace LinguaDesk {
class SmokeTests {
    static int count;
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);Console.WriteLine("PASS "+label);count++;}
    [STAThread] static int Main(string[] args){ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;try{Run(args[0]).GetAwaiter().GetResult();Console.WriteLine("TOTAL "+count+" passed");return 0;}catch(Exception ex){Console.WriteLine("FAIL "+ex);return 1;}}
    static async Task Run(string folder){Directory.CreateDirectory(folder);
        string text=String.Concat(Enumerable.Repeat("A sentence with words and 中文 😀.\r\n",200));var chunks=Translation.Split(text,480);Check(String.Concat(chunks)==text && chunks.All(c=>Encoding.UTF8.GetByteCount(c)<=480),"Unicode chunks retain every character within API byte limit");
        Check(Translation.Detect("Das ist ein deut scher Beispieltext mit einigen feh lerhaften Leerzeichen.")=="de","German OCR regression, local detection only");
        Check(Translation.Detect("hello")=="en"&&Translation.Detect("danke")=="de","Common short word detection");
        var examples=new[]{Tuple.Create("Hello world","en"),Tuple.Create("Guten Morgen. Das Wetter ist heute sehr schön.","de"),Tuple.Create("Bonjour, comment allez-vous aujourd’hui ?","fr"),Tuple.Create("Hola, este es un documento en español.","es"),Tuple.Create("Buongiorno, questo documento è scritto in italiano.","it"),Tuple.Create("Dit is een Nederlands document voor iedereen.","nl"),Tuple.Create("Olá, este documento está escrito em português.","pt"),Tuple.Create("你好世界","zh-CN"),Tuple.Create("こんにちは世界","ja"),Tuple.Create("안녕하세요","ko"),Tuple.Create("Доброе утро","ru")};foreach(var example in examples)Check(Translation.Detect(example.Item1)==example.Item2,"Language detection "+example.Item2);
        var settings=new Settings();settings.Key="synthetic-test-key";Check(settings.Key=="synthetic-test-key","DPAPI encryption round-trip");Check(!new JavaScriptSerializer().Serialize(settings).Contains("synthetic-test-key"),"Serialized settings contain no plaintext API key");
        foreach(var pair in new[]{Tuple.Create<string,Encoding>("utf8",new UTF8Encoding(true)),Tuple.Create<string,Encoding>("gbk",Encoding.GetEncoding(936)),Tuple.Create<string,Encoding>("utf16",Encoding.Unicode),Tuple.Create<string,Encoding>("utf16be",Encoding.BigEndianUnicode)}) {string path=Path.Combine(folder,pair.Item1+".txt");File.WriteAllText(path,"Hello 世界",pair.Item2);Check(Documents.ReadText(path)=="Hello 世界",pair.Item1+" file decode");}
        string docx=Path.Combine(folder,"sample.docx");if(File.Exists(docx))File.Delete(docx);using(var zip=ZipFile.Open(docx,ZipArchiveMode.Create)){var e=zip.CreateEntry("word/document.xml");using(var w=new StreamWriter(e.Open()))w.Write("<w:document xmlns:w='http://schemas.openxmlformats.org/wordprocessingml/2006/main'><w:body><w:p><w:r><w:t>Hello </w:t></w:r><w:r><w:t>world</w:t></w:r></w:p><w:tbl><w:tr><w:tc><w:p><w:r><w:t>Table cell</w:t></w:r></w:p></w:tc></w:tr></w:tbl></w:body></w:document>");}Check(Documents.ReadDocx(docx)=="Hello world"+Environment.NewLine+"Table cell","DOCX text runs and table cells");
        string ocr=await Documents.Ocr(Path.Combine(folder,"ocr-test.png"),"auto",CancellationToken.None);Check(ocr.Contains("Hello")&&ocr.Contains("世"),"Windows OCR image English and Chinese");
        string pdf=await Documents.Ocr(Path.Combine(folder,"sample.pdf"),"auto",CancellationToken.None);Check(pdf.Contains("world")&&pdf.Contains("第 1 页"),"PDF page render and OCR");
        string translated=await Translation.Segment("Hello world","en","zh-CN",new Settings(),CancellationToken.None);Check(translated.Contains("你好")||translated.Contains("世界"),"Live MyMemory translation");
        string german=await Translation.Segment("Guten Morgen. Das Wetter ist heute sehr schön.","de","zh-CN",new Settings(),CancellationToken.None);Check(System.Text.RegularExpressions.Regex.IsMatch(german,@"[\u4e00-\u9fff]")&&!german.Contains("Wetter"),"Live synthetic German translation");Console.WriteLine("German synthetic result: "+german);
        using(var listener=new HttpListener()){listener.Prefixes.Add("http://localhost:18763/");listener.Start();settings.Provider="AI";settings.Endpoint="http://localhost:18763/chat/completions";settings.Model="test-model";
            var serve=Task.Run(async()=>{var ctx=await listener.GetContextAsync();string body;using(var r=new StreamReader(ctx.Request.InputStream))body=await r.ReadToEndAsync();Check(body.Contains("test-model")&&body.Contains("Hello")&&ctx.Request.Headers["Authorization"]=="Bearer synthetic-test-key","AI request model, message and authorization");byte[] payload=Encoding.UTF8.GetBytes("{\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"content\":\"你好世界\"}}]}");ctx.Response.ContentType="application/json";ctx.Response.OutputStream.Write(payload,0,payload.Length);ctx.Response.Close();});Check(await Translation.Segment("Hello","en","zh-CN",settings,CancellationToken.None)=="你好世界","Compatible AI response parsing");await serve;
            var serverError=Task.Run(async()=>{var ctx=await listener.GetContextAsync();ctx.Response.StatusCode=429;ctx.Response.Close();});bool failed=false;try{await Translation.Segment("Hello","en","zh-CN",settings,CancellationToken.None);}catch(Exception ex){failed=ex.Message.Contains("429");}Check(failed,"HTTP quota errors are reported");await serverError;
            using(var cancel=new CancellationTokenSource()){cancel.Cancel();bool cancelled=false;try{await Translation.Segment("Hello","en","zh-CN",settings,cancel.Token);}catch(OperationCanceledException){cancelled=true;}Check(cancelled,"Translation cancellation");}
            listener.Stop();
        }
    }
}
}
