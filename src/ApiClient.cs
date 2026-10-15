using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace CiyuanWindows {
    public sealed class ApiClient : IDisposable {
        public const string DiscoveryUrl = "https://mp-66234e45-f864-4dd3-9eaa-377ad7a6e4df.cdn.bspapp.com/ciyuan";
        private static readonly string[] Defaults = {"http://47.103.33.207:8081","http://47.93.60.215:8081","http://39.106.100.156:8081","http://8.134.159.218:8081"};
        private readonly HttpClient client;
        private readonly string preferred;
        public string Origin { get; private set; }
        public object Config { get; private set; }
        public ApiClient(string preferredOrigin = null) {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            client = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate });
            client.Timeout = TimeSpan.FromSeconds(15);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/150.0.0.0 Safari/537.36");
            Origin = preferredOrigin == null ? null : preferredOrigin.TrimEnd('/');
            preferred=Origin;
        }
        private void AnonymousHeaders(HttpRequestMessage request) {
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Func<long,string> encode = n => Convert.ToBase64String(Encoding.UTF8.GetBytes(n.ToString()));
            request.Headers.TryAddWithoutValidation("app-version","284");
            request.Headers.TryAddWithoutValidation("appId","com.ds.wlss.xl");
            request.Headers.TryAddWithoutValidation("deviceId","1234567890");
            request.Headers.TryAddWithoutValidation("imei",encode(now));
            request.Headers.TryAddWithoutValidation("kkm",encode(now-284));
        }
        private async Task<object> JsonRequest(string method,string url,IDictionary<string,string> parameters,bool central) {
            bool post = method.Equals("POST",StringComparison.OrdinalIgnoreCase);
            if(!post && parameters != null && parameters.Count > 0) url += (url.Contains("?") ? "&" : "?") + String.Join("&",parameters.Select(p => Uri.EscapeDataString(p.Key)+"="+Uri.EscapeDataString(p.Value??"")));
            using(var request = new HttpRequestMessage(post ? HttpMethod.Post : HttpMethod.Get,url)) {
                if(central) AnonymousHeaders(request);
                if(post) request.Content = new FormUrlEncodedContent(parameters??new Dictionary<string,string>());
                using(var response = await client.SendAsync(request).ConfigureAwait(false)) {
                    if(!response.IsSuccessStatusCode) throw new HttpRequestException("请求失败：HTTP " + (int)response.StatusCode + "，" + new Uri(url).Host);
                    if(response.Content.Headers.ContentLength > 16000000) throw new HttpRequestException("服务器响应过大");
                    string decoded = Transport.Decode(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                    object value;
                    try { value = J.Parse(decoded); } catch(Exception e) { throw new FormatException("服务器没有返回可识别的数据",e); }
                    var obj = J.Obj(value);
                    string status = J.S(obj,"status");
                    if(status == "ERROR" || status == "FAIL" || (J.Get(obj,"code") != null && J.Int(J.Get(obj,"code")) >= 400)) throw new InvalidOperationException(J.S(obj,"msg",J.S(obj,"message","来源拒绝了请求")));
                    return value;
                }
            }
        }
        private async Task<string> Probe(string origin) {
            try { await JsonRequest("GET",origin+"/checkNetwork",null,true).ConfigureAwait(false); return origin; } catch { return null; }
        }
        public async Task<object> InitializeAsync() {
            try {return await InitializeCoreAsync().ConfigureAwait(false);}catch(Exception e){Origin=null;Config=FallbackConfig.Load(e.GetBaseException().Message);return Config;}
        }
        private async Task<object> InitializeCoreAsync() {
            if(Origin==null&&preferred!=null)Origin=preferred;
            if(Origin == null) {
                string[] candidates = Defaults;
                try {
                    object discovery = await JsonRequest("GET",DiscoveryUrl,null,false).ConfigureAwait(false);
                    var declared = J.Arr(J.Get(discovery,"domain")).Select(J.Str).Where(s => s.StartsWith("http://",StringComparison.OrdinalIgnoreCase)||s.StartsWith("https://",StringComparison.OrdinalIgnoreCase)).ToArray();
                    if(declared.Length > 0) candidates = declared;
                } catch { }
                var pending = candidates.Select(Probe).ToList();
                while(pending.Count > 0) {
                    var finished = await Task.WhenAny(pending).ConfigureAwait(false); pending.Remove(finished);
                    string selected = await finished.ConfigureAwait(false);
                    if(selected != null) { Origin=selected.TrimEnd('/'); break; }
                }
                if(Origin == null) throw new HttpRequestException("原服务暂时无法连接，请检查网络后重试");
            }
            Config = await RequestAsync("GET","/mobile/config",null).ConfigureAwait(false);
            return Config;
        }
        public Task<object> RequestAsync(string method,string path,IDictionary<string,string> parameters) {
            if(!String.Equals(method,"GET",StringComparison.OrdinalIgnoreCase) && !String.Equals(method,"POST",StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("仅支持内容读取使用的 GET 和 POST");
            if(Origin == null) throw new InvalidOperationException("服务尚未连接");
            if(!path.StartsWith("/",StringComparison.Ordinal)||path.StartsWith("//",StringComparison.Ordinal)) throw new ArgumentException("接口路径必须是相对路径");
            return JsonRequest(method,Origin+path,parameters,true);
        }
        public void Dispose() { client.Dispose(); }
    }
}
