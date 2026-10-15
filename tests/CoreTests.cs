using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

class CoreTests {
    static string Root;
    static Assembly Core;
    static int Passed, Failed;
    static JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 16000000 };
    static object Call(string name, string method, object instance, params object[] args) {
        if (Core == null) {
            string path = Path.Combine(Root, "build", "CiyuanCore.dll");
            if (!File.Exists(path)) throw new Exception("Production behavior not implemented: CiyuanCore.dll is absent");
            Core = Assembly.LoadFrom(path);
        }
        Type type = Core.GetType("CiyuanWindows." + name, true);
        try { return type.GetMethod(method).Invoke(instance, args); }
        catch (TargetInvocationException e) { throw e.InnerException; }
    }
    static string Decode(string body) { return (string)Call("Transport", "Decode", null, body); }
    static object Store(string dir) {
        if (Core == null) Decode("{}");
        return Activator.CreateInstance(Core.GetType("CiyuanWindows.StateStore", true), dir);
    }
    static void Equal(object actual, object expected) {
        if (!Object.Equals(actual, expected)) throw new Exception("Expected " + expected + ", received " + actual);
    }
    static void Reject(Action action) {
        try { action(); } catch (FormatException) { return; } catch (System.Security.Cryptography.CryptographicException) { return; }
        throw new Exception("Malformed protected response was accepted");
    }
    static void Test(string name, Action action) {
        try { action(); Passed++; Console.WriteLine("PASS " + name); }
        catch (Exception e) { Failed++; Console.WriteLine("FAIL " + name + ": " + e.Message); }
    }
    static Dictionary<string, object> State(string title) {
        return new Dictionary<string,object> { {"favorites", new object[] {new Dictionary<string,object> {{"title",title},{"link","https://example.invalid/detail/1"}}}}, {"history",new object[0]}, {"settings",new Dictionary<string,object>()} };
    }
    static object[] Favorites(object state) { return (object[])((Dictionary<string,object>)state)["favorites"]; }
    static void ApiBoundary(int status, string method, Action<string,object,Exception> assert, string responseBody="{\"ok\":1}") {
        var listener = new TcpListener(IPAddress.Loopback,0); listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var captured = Task.Run(() => {
            using(var socket=listener.AcceptTcpClient()) using(var stream=socket.GetStream()) {
                var bytes=new List<byte>(); int current;
                while((current=stream.ReadByte())>=0) { bytes.Add((byte)current); int n=bytes.Count; if(n>=4&&bytes[n-4]==13&&bytes[n-3]==10&&bytes[n-2]==13&&bytes[n-1]==10) break; }
                string response=responseBody;
                byte[] answer=Encoding.UTF8.GetBytes("HTTP/1.1 "+status+" Test\r\nContent-Type: application/json\r\nContent-Length: "+Encoding.UTF8.GetByteCount(response)+"\r\nConnection: close\r\n\r\n"+response);
                stream.Write(answer,0,answer.Length);return Encoding.UTF8.GetString(bytes.ToArray());
            }
        });
        object result=null; Exception error=null; object api=null;
        try {
            if(Core==null) Decode("{}");
            api=Activator.CreateInstance(Core.GetType("CiyuanWindows.ApiClient",true),"http://127.0.0.1:"+port);
            var task=method=="INIT"?(Task)Call("ApiClient","InitializeAsync",api):(Task)Call("ApiClient","RequestAsync",api,method,"/answer",new Dictionary<string,string>{{"key","中文 空格"},{"page","0"}});
            task.GetAwaiter().GetResult();result=task.GetType().GetProperty("Result").GetValue(task,null);
        } catch(Exception e) { error=e; }
        string request="";
        if(captured.Wait(1500))request=captured.Result;
        listener.Stop(); if(api is IDisposable)((IDisposable)api).Dispose();
        assert(request,result,error);
    }
    static int Main(string[] args) {
        Root = args[0];
        Test("plain JSON preserves decoded payload", () => Equal(Decode("{\"ok\":1}"), "{\"ok\":1}"));
        Test("Dddm decodes UTF8 Chinese", () => Equal(Decode("Dddm" + Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"标题\":\"测试\"}"))), "{\"标题\":\"测试\"}"));
        Test("real encrypted homepage equals independent decoded fixture", () => {
            object got = Json.DeserializeObject(Decode(File.ReadAllText(Path.Combine(Root,"tests","fixtures","home-encrypted.txt"))));
            object want = Json.DeserializeObject(File.ReadAllText(Path.Combine(Root,"tests","fixtures","home-expected.json")));
            Equal(Json.Serialize(got), Json.Serialize(want));
        });
        Test("bad Base64 rejected", () => Reject(() => Decode("Dddm%%%%")));
        Test("short CBC IV rejected", () => Reject(() => Decode("CYK2026AA==:AAAAAAAAAAAAAAAAAAAAAA==")));
        Test("incomplete CBC ciphertext rejected", () => Reject(() => Decode("CYK2026AAAAAAAAAAAAAAAAAAAAAA==:AQ==")));
        Test("anonymous request preserves UTF8 query without session", () => ApiBoundary(200,"GET",(request,result,error)=>{
            if(error!=null)throw error;
            if(!request.StartsWith("GET /answer?key=%E4%B8%AD%E6%96%87%20%E7%A9%BA%E6%A0%BC&page=0 "))throw new Exception("Query was corrupted: "+request.Split('\r')[0]);
            if(request.IndexOf("\r\nsession:",StringComparison.OrdinalIgnoreCase)>=0)throw new Exception("Anonymous session was fabricated");
            Equal(((Dictionary<string,object>)result)["ok"],1);
        }));
        Test("HTTP failure is propagated instead of decoded as success",()=>ApiBoundary(503,"GET",(request,result,error)=>{
            if(error==null||!error.Message.Contains("503"))throw new Exception("HTTP503 was not propagated");
        }));
        Test("HTTP200 protected business error is decoded and propagated",()=>ApiBoundary(200,"GET",(request,result,error)=>{
            if(!(error is InvalidOperationException)||!error.Message.Contains("来源拒绝"))throw new Exception("Protected error appeared as successful content");
        },"Dddm"+Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"status\":\"ERROR\",\"msg\":\"来源拒绝\"}"))));
        Test("unsupported request method rejected before network",()=>ApiBoundary(200,"PUT",(request,result,error)=>{
            if(!(error is ArgumentException)||request.Length!=0)throw new Exception("PUT silently became another network operation");
        }));
        Test("configuration failure retains APK-sourced fallback hosts and status",()=>ApiBoundary(503,"INIT",(request,result,error)=>{
            if(error!=null)throw error;var config=(Dictionary<string,object>)result;if(!Object.Equals(config["isFallback"],true))throw new Exception("Fallback status missing");Equal(((object[])config["hosts"]).Length,108);if(!Convert.ToString(config["warning"]).Contains("503"))throw new Exception("Failure reason lost");
        }));
        string dir = Path.Combine(Path.GetTempPath(), "CiyuanWindowsCoreTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Test("save and restart retain Chinese favorite exactly once", () => {
            object store = Store(dir);
            Call("StateStore","Save",store,State("神枪少女"));
            Call("StateStore","Save",store,State("神枪少女"));
            object loaded = Call("StateStore","Load",Store(dir));
            Equal(Favorites(loaded).Length,1);
            Equal(((Dictionary<string,object>)Favorites(loaded)[0])["title"],"神枪少女");
        });
        Test("abandoned temporary write never overrides committed state", () => {
            File.WriteAllText(Path.Combine(dir,"state.json.tmp"),"{truncated");
            object loaded = Call("StateStore","Load",Store(dir));
            Equal(Favorites(loaded).Length,1);
        });
        Test("corrupt state retained while startup receives empty favorites", () => {
            File.WriteAllText(Path.Combine(dir,"state.json"),"{broken");
            object loaded = Call("StateStore","Load",Store(dir));
            Equal(Favorites(loaded).Length,0);
            if (Directory.GetFiles(dir,"state.corrupt-*.json").Length != 1) throw new Exception("Corrupt original was not retained");
        });
        Test("state limit uses disk bytes and rejects oversized UTF8 before replacement",()=>{
            object store=Store(dir);Call("StateStore","Save",store,State("保留内容"));
            bool refused=false;try{Call("StateStore","Save",store,State(new string('中',2800000)));}catch(InvalidOperationException){refused=true;}if(!refused)throw new Exception("Oversized UTF8 state was accepted");
            object loaded=Call("StateStore","Load",Store(dir));Equal(((Dictionary<string,object>)Favorites(loaded)[0])["title"],"保留内容");
        });
        Test("malformed record shape is retained instead of crashing record pages",()=>{
            File.WriteAllText(Path.Combine(dir,"state.json"),"{\"favorites\":[null],\"history\":[],\"settings\":{}}");var loaded=Call("StateStore","Load",Store(dir));Equal(Favorites(loaded).Length,0);if(Directory.GetFiles(dir,"state.corrupt-*.json").Length<2)throw new Exception("Malformed record was not retained");
        });
        Test("wrong top-level record types are rejected before save",()=>{
            var invalid=new Dictionary<string,object>{{"favorites","wrong"},{"history",new object[0]},{"settings",new Dictionary<string,object>()}};bool refused=false;try{Call("StateStore","Save",Store(dir),invalid);}catch(FormatException){refused=true;}if(!refused)throw new Exception("Invalid record shape silently saved");
        });
        Console.WriteLine("RESULT " + Passed + " passed, " + Failed + " failed");
        return Failed == 0 ? 0 : 1;
    }
}
