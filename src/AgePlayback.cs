using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AngleSharp.Html.Parser;
namespace CiyuanWindows {
 public static class AgePlayback {
  private static byte[] Hex(string value){if(value.Length%2!=0)throw new FormatException("播放数据长度无效");return Enumerable.Range(0,value.Length/2).Select(i=>Convert.ToByte(value.Substring(i*2,2),16)).ToArray();}
  private static string Encrypt(string clear){using(var aes=Aes.Create()){aes.Key=Encoding.UTF8.GetBytes("ni po jie ni  **");aes.IV=aes.Key;using(var enc=aes.CreateEncryptor())return BitConverter.ToString(enc.TransformFinalBlock(Encoding.UTF8.GetBytes(clear),0,Encoding.UTF8.GetByteCount(clear))).Replace("-","");}}
  private static string Decrypt(string value,byte[] key,byte[] iv){using(var aes=Aes.Create()){aes.Key=key;aes.IV=iv;using(var dec=aes.CreateDecryptor()){byte[] bytes=Hex(value);return Encoding.UTF8.GetString(dec.TransformFinalBlock(bytes,0,bytes.Length));}}}
  public static string EncodeRequest(string value){return Encrypt(value);}
  public static string DecodeRequest(string value){byte[] key=Encoding.UTF8.GetBytes("ni po jie ni  **");return Decrypt(value,key,key);}
  private static string Variable(string html,string name){var m=Regex.Match(html,@"\b(?:var\s+)?"+Regex.Escape(name)+@"\s*=\s*['"" ]([^'""\r\n]*)['""]",RegexOptions.IgnoreCase);return m.Success?m.Groups[1].Value:"";}
  public static async Task<object> ResolveAsync(string link) {
   using(var http=new HttpClient(new HttpClientHandler{AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate})) {
    http.Timeout=TimeSpan.FromSeconds(30);http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_14_6) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/102.0.0.0 Safari/537.36");
    string page=link;
    for(int depth=0;depth<3;depth++) {
     string html;using(var request=new HttpRequestMessage(HttpMethod.Get,page)){request.Headers.Referrer=new Uri(depth==0?"https://www.agedm.io/":link);using(var response=await http.SendAsync(request).ConfigureAwait(false)){response.EnsureSuccessStatusCode();html=await response.Content.ReadAsStringAsync().ConfigureAwait(false);}}
     var found=MediaResolver.ExtractCandidates(page,html);if(found.Length>0)return J.Map("url",found[0],"referer",page,"browserFallback",false);
     string api=Variable(html,"Api"),refValue=Variable(html,"Ref"),time=Variable(html,"Time"),version=Variable(html,"Version"),vurl=Variable(html,"Vurl"),host=new Uri(page).Host;
     if(api==""||refValue==""||vurl==""){var frame=new HtmlParser().ParseDocument(html).QuerySelector("iframe#iframeForVideo[src],iframe[src]");if(frame!=null){page=SourceAdapters.FixUrl(page,frame.GetAttribute("src"));continue;}throw new FormatException("AGE 播放页面没有返回正常播放参数");}
     Exception last=null;
     foreach(string wap in new[]{"1","0"})try {
      string parameters=Encrypt(J.Stringify(J.Map("url",vurl,"wap",wap,"ios","0","host",host,"referer",refValue,"time",time))),uuid=Guid.NewGuid().ToString("N");
      using(var request=new HttpRequestMessage(HttpMethod.Post,api.TrimEnd('/')+"/Api.php")) {
       request.Headers.Referrer=new Uri(page);request.Headers.TryAddWithoutValidation("Origin",new Uri(page).GetLeftPart(UriPartial.Authority));request.Headers.TryAddWithoutValidation("X-Requested-With","XMLHttpRequest");
       request.Headers.TryAddWithoutValidation("Video-Parse-Uuid",uuid);request.Headers.TryAddWithoutValidation("Video-Parse-Time",time);request.Headers.TryAddWithoutValidation("Video-Parse-Version",version);request.Headers.TryAddWithoutValidation("Video-Parse-Sign",Encrypt(host+" | "+uuid+" | "+time+" | "+version+" | "+parameters));
       request.Content=new FormUrlEncodedContent(new Dictionary<string,string>{{"Params",parameters}});
       using(var response=await http.SendAsync(request).ConfigureAwait(false)) {
        response.EnsureSuccessStatusCode();var data=J.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));string code=J.S(data,"Code",J.S(data,"code"));
        if(J.Int(J.Get(data,"Status",J.Get(data,"status")))!=1)throw new InvalidOperationException("AGE 来源拒绝播放："+code+" "+J.S(data,"Data"));
        string keySeed=code+J.S(data,"Appkey",J.S(data,"appkey"))+J.S(data,"Version",version);
        if(code=="10"){var doc=new HtmlParser().ParseDocument(html);var a=doc.QuerySelector("meta[http-equiv][id]");var b=doc.QuerySelector("meta[name=viewport][id]");keySeed=code+((a==null?"":a.Id)+(b==null?"":b.Id)).Replace("viewport","")+J.S(data,"Appkey",J.S(data,"appkey"))+J.S(data,"Version",version);}
        string hash;using(var md5=MD5.Create())hash=BitConverter.ToString(md5.ComputeHash(Encoding.UTF8.GetBytes(keySeed))).Replace("-","").ToLowerInvariant();
        var decoded=J.Parse(Decrypt(J.S(data,"Data"),Encoding.UTF8.GetBytes(hash.Substring(0,16)),Encoding.UTF8.GetBytes(hash.Substring(16,16))));
        string url=J.S(decoded,"url");if(code=="10")url=DecodeRequest(url);url=WebUtility.HtmlDecode(url.Replace("\\/","/")).Trim();if(url.StartsWith("http%",StringComparison.OrdinalIgnoreCase)||url.StartsWith("https%",StringComparison.OrdinalIgnoreCase))url=Uri.UnescapeDataString(url);
        if(J.S(decoded,"type")=="url"){page=SourceAdapters.FixUrl(page,url);break;}
        if(!url.StartsWith("http"))throw new FormatException("AGE 没有返回媒体地址");if(!MediaResolver.IsMedia(url))throw new FormatException("AGE 来源返回了暂不可用的提示片，已排除");return J.Map("url",url,"referer",page,"browserFallback",false);
       }
      }
     }catch(Exception e){last=e;}
     if(last!=null)throw last;
    }
    throw new FormatException("AGE 播放页面需要网页播放器");
   }
  }
 }
}
