using System;
using System.IO;
using System.Text;

namespace CiyuanWindows {
    public sealed class StateStore {
        private readonly string root;
        private readonly object gate = new object();
        public StateStore(string directory) { root = Path.GetFullPath(directory); Directory.CreateDirectory(root); }
        private static object Empty() { return J.Map("favorites",new object[0],"history",new object[0],"settings",J.Map()); }
        private static void Validate(object value) {
            var state=value as System.Collections.Generic.Dictionary<string,object>;
            if(state==null||!state.ContainsKey("favorites")||!state.ContainsKey("history")||!(J.Get(state,"settings") is System.Collections.Generic.Dictionary<string,object>))throw new FormatException("状态结构不完整");
            foreach(string key in new[]{"favorites","history"}) {
                object records=state[key];if(!(records is object[])&&!(records is System.Collections.ArrayList))throw new FormatException("记录列表格式无效");
                foreach(object entry in J.Arr(records)) {
                    var item=entry as System.Collections.Generic.Dictionary<string,object>;Uri link;
                    if(item==null||!(J.Get(item,"title") is string)||!Uri.TryCreate(J.S(item,"link"),UriKind.Absolute,out link)||(link.Scheme!="http"&&link.Scheme!="https"))throw new FormatException("记录条目格式无效");
                    object position=J.Get(item,"position");double number;
                    if(position!=null&&(!Double.TryParse(J.Str(position),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out number)||Double.IsNaN(number)||Double.IsInfinity(number)||number<0))throw new FormatException("进度格式无效");
                }
            }
        }
        public object Load() {
            lock(gate) {
                string path = Path.Combine(root,"state.json");
                if(!File.Exists(path)) return Empty();
                try {
                    if(new FileInfo(path).Length > 8000000) throw new FormatException("状态文件超过限制");
                    var state = J.Obj(J.Parse(File.ReadAllText(path,Encoding.UTF8)));Validate(state);
                    state["favorites"] = J.Arr(state["favorites"]); state["history"] = J.Arr(state["history"]); state["settings"] = J.Obj(state["settings"]);
                    return state;
                } catch(Exception e) {
                    if(!(e is ArgumentException || e is InvalidOperationException || e is FormatException || e is IOException)) throw;
                    string retained = Path.Combine(root,"state.corrupt-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N").Substring(0,6) + ".json");
                    File.Move(path,retained);
                    return Empty();
                }
            }
        }
        public void Save(object state) {
            lock(gate) {
                Validate(state);
                string text = J.Stringify(state);
                byte[] bytes = new UTF8Encoding(false).GetBytes(text);
                if(bytes.Length > 8000000) throw new InvalidOperationException("个人记录太大，请先清理历史");
                string path = Path.Combine(root,"state.json"), temporary = path + ".tmp";
                using(var stream = new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None)) {
                    stream.Write(bytes,0,bytes.Length); stream.Flush(true);
                }
                if(File.Exists(path)) File.Replace(temporary,path,null); else File.Move(temporary,path);
            }
        }
    }
}
