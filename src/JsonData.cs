using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Web.Script.Serialization;

namespace CiyuanWindows {
    public static class J {
        public static JavaScriptSerializer Serializer() { return new JavaScriptSerializer { MaxJsonLength = 16000000, RecursionLimit = 100 }; }
        public static object Parse(string text) { return Serializer().DeserializeObject(text); }
        public static string Stringify(object value) { return Serializer().Serialize(value); }
        public static Dictionary<string,object> Obj(object value) { return value as Dictionary<string,object> ?? new Dictionary<string,object>(); }
        public static object Get(object value, string key, object fallback = null) { object item; return Obj(value).TryGetValue(key,out item) ? item : fallback; }
        public static string Str(object value) { return value == null ? "" : Convert.ToString(value,CultureInfo.InvariantCulture); }
        public static string S(object value, string key, string fallback = "") { object item = Get(value,key); return item == null ? fallback : Str(item); }
        public static object[] Arr(object value) { return value as object[] ?? (value is ArrayList ? ((ArrayList)value).ToArray() : new object[0]); }
        public static int Int(object value, int fallback = 0) { int number; return Int32.TryParse(Str(value),out number) ? number : fallback; }
        public static Dictionary<string,object> Map(params object[] pairs) {
            var result = new Dictionary<string,object>();
            for(int i=0;i<pairs.Length;i+=2) result[Str(pairs[i])] = pairs[i+1];
            return result;
        }
    }
}
