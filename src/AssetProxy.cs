using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CiyuanWindows {
 public sealed class AssetProxy : IDisposable {
  public const string Origin="https://assets.ciyuan.local";
  private readonly HttpClient http=new HttpClient(new HttpClientHandler{AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate});
  public AssetProxy(){ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;http.Timeout=TimeSpan.FromSeconds(35);http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/150.0.0.0 Safari/537.36");}
  public static string Url(string url,string referer){return Origin+"/fetch?url="+Uri.EscapeDataString(url)+"&ref="+Uri.EscapeDataString(referer??url);}
  private static bool Image(byte[] b){return b.Length>12&&((b[0]==255&&b[1]==216)||(b[0]==137&&b[1]==80)||(b[0]==71&&b[1]==73)||(b[0]==82&&b[1]==73));}
  public static byte[] DecodeImage(byte[] body) {
   if(Image(body))return body;
   if(body.Length<=16||(body.Length-16)%16!=0)throw new FormatException("漫画图片格式无效");
   using(var aes=Aes.Create()){aes.Key=Encoding.UTF8.GetBytes("0B6666A0-BB59-1381-B746-a0E4C9AC").Concat(new byte[32]).Take(32).ToArray();aes.IV=body.Take(16).ToArray();aes.Mode=CipherMode.CBC;aes.Padding=PaddingMode.PKCS7;using(var decrypt=aes.CreateDecryptor()){var result=decrypt.TransformFinalBlock(body,16,body.Length-16);if(!Image(result))throw new FormatException("漫画图片解码后格式无效");return result;}}
  }
  public async Task<ProxyResult> FetchAsync(string url,string referer,string range) {
   Uri uri;if(!Uri.TryCreate(url,UriKind.Absolute,out uri)||(uri.Scheme!="http"&&uri.Scheme!="https"))throw new ArgumentException("资源链接无效");
   using(var request=new HttpRequestMessage(HttpMethod.Get,uri)){
    Uri refUri;if(Uri.TryCreate(referer,UriKind.Absolute,out refUri)&&(refUri.Scheme=="http"||refUri.Scheme=="https"))request.Headers.Referrer=refUri;
    // A browser's open-ended MP4 request must not buffer the entire movie.
    if(Regex.IsMatch(uri.AbsolutePath,@"\.(mp4|m4v|mov)$",RegexOptions.IgnoreCase)) {
     const long chunk=4194304;var match=Regex.Match(range??"",@"^bytes=(\d+)-(\d*)$");long start=0,end=chunk-1;
     if(match.Success){if(!Int64.TryParse(match.Groups[1].Value,out start)||start>Int64.MaxValue-chunk)throw new ArgumentException("视频范围无效");end=start+chunk-1;long requested;if(match.Groups[2].Value!=""){if(!Int64.TryParse(match.Groups[2].Value,out requested)||requested<start)throw new ArgumentException("视频范围无效");end=Math.Min(end,requested);}}
     else if(!String.IsNullOrWhiteSpace(range)){var suffix=Regex.Match(range,@"^bytes=-(\d+)$");long length;if(!suffix.Success||!Int64.TryParse(suffix.Groups[1].Value,out length)||length<=0)throw new ArgumentException("视频范围无效");range="bytes=-"+Math.Min(chunk,length);}
     if(match.Success||String.IsNullOrWhiteSpace(range))range="bytes="+start+"-"+end;
    }
    if(!String.IsNullOrWhiteSpace(range))request.Headers.TryAddWithoutValidation("Range",range);
    using(var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false)) {
     if(!response.IsSuccessStatusCode)throw new HttpRequestException(uri.Host+" 返回 HTTP "+(int)response.StatusCode);
     var stream=await response.Content.ReadAsStreamAsync().ConfigureAwait(false);var memory=new MemoryStream();byte[] buffer=new byte[65536];int read;
     while((read=await stream.ReadAsync(buffer,0,buffer.Length).ConfigureAwait(false))>0){if(memory.Length+read>64000000)throw new IOException("单个资源过大");memory.Write(buffer,0,read);}
     byte[] body=memory.ToArray();string type=response.Content.Headers.ContentType==null?"application/octet-stream":response.Content.Headers.ContentType.MediaType;
     string real=response.RequestMessage.RequestUri.AbsoluteUri;
     if(type=="text/html"||type=="application/json")throw new IOException("来源返回了提示页面，资源未加载");
     if(uri.AbsolutePath.EndsWith(".m3u8",StringComparison.OrdinalIgnoreCase)||type.IndexOf("mpegurl",StringComparison.OrdinalIgnoreCase)>=0){
      string manifest=Encoding.UTF8.GetString(body);if(!manifest.TrimStart().StartsWith("#EXTM3U"))throw new FormatException("播放清单格式无效");
      var lines=manifest.Split('\n').Select(line=>{
       string clean=line.Trim();if(clean!=""&&!clean.StartsWith("#"))return Url(SourceAdapters.FixUrl(real,clean),referer);
       return Regex.Replace(line,"URI=\"([^\"]+)\"",m=>"URI=\""+Url(SourceAdapters.FixUrl(real,m.Groups[1].Value),referer)+"\"");});
      body=Encoding.UTF8.GetBytes(String.Join("\n",lines));type="application/vnd.apple.mpegurl";
     } else if(uri.AbsolutePath.Contains("en_images/")&&(uri.Host.Contains("mw")||uri.Host.Contains("mhttu"))){body=DecodeImage(body);type=body[0]==137?"image/png":"image/jpeg";}
     string headers="Content-Type: "+type+"\r\nAccess-Control-Allow-Origin: https://app.ciyuan.local\r\nX-Content-Type-Options: nosniff\r\nCache-Control: no-store\r\n";
     if(response.Content.Headers.ContentRange!=null)headers+="Content-Range: "+response.Content.Headers.ContentRange+"\r\n";
     if(response.Headers.AcceptRanges.Any())headers+="Accept-Ranges: bytes\r\n";
     return new ProxyResult{Body=body,Headers=headers,Status=(int)response.StatusCode};
    }
   }
  }
  public void Dispose(){http.Dispose();}
 }
 public sealed class ProxyResult {public byte[] Body;public string Headers;public int Status;}
}
