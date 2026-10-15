using System;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;
class DispatchTests {
 static int Main(string[] args) { try {
 var core=Assembly.LoadFrom(Path.Combine(args[0],"build","CiyuanCore.dll"));
 var type=core.GetType("CiyuanWindows.ContentClient",true);
 var json=new JavaScriptSerializer {MaxJsonLength=16000000};
 var home=json.DeserializeObject(File.ReadAllText(Path.Combine(args[0],"tests","fixtures","home-expected.json")));
 var normalized=(System.Collections.Generic.Dictionary<string,object>)type.GetMethod("NormalizeHome").Invoke(null,new[]{home,"video"});
 if(((object[])normalized["sections"]).Length!=2)throw new Exception("home lost sections");
 var age=json.DeserializeObject(File.ReadAllText(Path.Combine(args[0],"tests","fixtures","age-api-detail.json")));
 var detail=(System.Collections.Generic.Dictionary<string,object>)type.GetMethod("ParseAgeApi").Invoke(null,new[]{"https://www.agedm.io/detail/20030013",age});
 if(((object[])detail["lines"]).Length!=2)throw new Exception("lost API playlists");
 var search=json.DeserializeObject("[{\"links\":[\"https://www.agedm.io/search?query=神枪少女&page=1\",\"ZY2\"]}]");
 var urls=(string[])type.GetMethod("SearchUrls").Invoke(null,new[]{search});
 if(urls.Length!=1)throw new Exception("Chinese search URL rejected");
 Console.WriteLine("PASS real homepage fields, AGE API episode fields, Unicode search URL");return 0;
 }catch(Exception e){Console.WriteLine("FAIL "+e.GetBaseException().Message);return 1;} }
}
