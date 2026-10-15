using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CiyuanWindows {
 public sealed class ContentClient : IDisposable {
  private readonly ApiClient api=new ApiClient();
  private readonly SourceAdapters sources=new SourceAdapters();
  private Task<object> initialization;
  private readonly object gate=new object();
  public Task<object> InitializeAsync(){lock(gate){if(initialization==null||initialization.IsFaulted||(initialization.IsCompleted&&Object.Equals(J.Get(api.Config,"isFallback"),true)))initialization=api.InitializeAsync();return initialization;}}
  public static object NormalizeHome(object value,string kind) {
   var sections=new List<object>();
   foreach(var module in J.Arr(J.Get(value,"animeModules"))) {
    var items=J.Arr(J.Get(module,"animes")).Select(x=>Item(x,kind)).ToArray();
    sections.Add(J.Map("title",J.S(module,"title"),"items",items));
   }
   return J.Map("sections",sections.ToArray());
  }
  public static string[] SearchUrls(object value) {
   var urls=new List<string>();
   foreach(var batch in J.Arr(value))foreach(var item in J.Arr(J.Get(batch,"links"))){string u=SourceAdapters.CleanUrl(J.Str(item));Uri parsed;if(Uri.TryCreate(u,UriKind.Absolute,out parsed)&&(parsed.Scheme=="http"||parsed.Scheme=="https"))urls.Add(parsed.AbsoluteUri);}
   return urls.Distinct().ToArray();
  }
  private static object Item(object value,string kind){string link=J.S(value,"link",J.S(value,"url"));return J.Map("title",J.S(value,"title",J.S(value,"name")),"img",J.S(value,"img",J.S(value,"cover")),"link",link,"update",J.S(value,"update"),"kind",kind,"source",Uri.IsWellFormedUriString(link,UriKind.Absolute)?new Uri(link).Host:"");}
  public static object ParseAgeApi(string url,object data) {
   var video=J.Get(data,"video");var lines=new List<object>();
   foreach(var playlist in J.Obj(J.Get(video,"playlists"))) {
    var episodes=new List<object>();string prefix=J.S(J.Get(data,"player_jx"),"zj");
    if(J.S(data,"player_vip").Contains(playlist.Key))prefix=J.S(J.Get(data,"player_jx"),"vip",prefix);
    foreach(var entry in J.Arr(playlist.Value)){var pair=J.Arr(entry);if(pair.Length>=2)episodes.Add(J.Map("name",J.Str(pair[0]),"link",prefix+J.Str(pair[1])));}
    if(episodes.Count>0)lines.Add(J.Map("name",playlist.Key,"episodes",episodes.ToArray()));
   }
   return J.Map("title",J.S(video,"name"),"img",J.S(video,"cover"),"link",url,"intro",J.S(video,"intro_clean"),"kind","video","source",new Uri(url).Host,"lines",lines.ToArray(),"browserFallback",lines.Count==0);
  }
  public async Task<object> InvokeAsync(string action,IDictionary<string,object> args) {
   string link=J.S(args,"link"),kind=J.S(args,"kind","video");
   if(action=="catalog"){if(link=="")link="https://api.agedm.io/v2/catalog?season=all&region=all&label=all&year=all&page=1&size=20";string body=await sources.GetTextAsync(link).ConfigureAwait(false);return link.Contains("api.agedm.io/v2/catalog")?SourceAdapters.ParseCatalogApi(link,J.Parse(body)):SourceAdapters.ParseCatalog(link,body);}
   if(action=="detail") {
    if(kind=="video"&&Regex.IsMatch(link,@"agedm\.io/detail/\d+")){
     try {string id=Regex.Match(link,@"/detail/(\d+)").Groups[1].Value;return ParseAgeApi(link,J.Parse(await sources.GetTextAsync("https://api.agedm.io/v2/detail/"+id).ConfigureAwait(false)));}catch { }
    }
    return SourceAdapters.ParseDetail(link,await sources.GetTextAsync(link).ConfigureAwait(false),kind);
   }
   if(action=="chapter") {
    var chapter=SourceAdapters.ParseChapter(link,await sources.GetTextAsync(link).ConfigureAwait(false));
    if(J.Arr(J.Get(chapter,"images")).Length==0&&new Uri(link).Host.Contains("manwaai")) {
     var match=Regex.Match(link,@"/comic/(\d+)/(\d+)");if(match.Success) {
      string origin=new Uri(link).GetLeftPart(UriPartial.Authority),id=match.Groups[2].Value,comic=match.Groups[1].Value;
      var first=J.Parse(await sources.GetTextAsync(origin+"/api/comic/image/"+id+"?page=1&page_size=60").ConfigureAwait(false));
      if(J.Int(J.Get(first,"code"))!=200)throw new InvalidOperationException(J.S(first,"msg","来源拒绝读取本章"));
      var images=J.Arr(J.Get(J.Get(first,"data"),"images")).Select(x=>(object)J.S(x,"url")).ToList();
      int pages=J.Int(J.Get(J.Get(J.Get(first,"data"),"pagination"),"total_pages"),1);
      for(int p=2;p<=pages&&p<=50;p++){var more=J.Parse(await sources.GetTextAsync(origin+"/api/comic/image/"+id+"?page="+p+"&page_size=60").ConfigureAwait(false));if(J.Int(J.Get(more,"code"))!=200)throw new InvalidOperationException(J.S(more,"msg","图片分页读取失败"));images.AddRange(J.Arr(J.Get(J.Get(more,"data"),"images")).Select(x=>(object)J.S(x,"url")));}
      var info=J.Get(J.Parse(await sources.GetTextAsync(origin+"/api/comic/chapter/info/"+id).ConfigureAwait(false)),"data");
      string previous=J.S(J.Get(info,"prev"),"id"),next=J.S(J.Get(info,"next"),"id");
      chapter=J.Map("title",J.S(J.Get(info,"current"),"title"),"link",link,"images",images.ToArray(),"previous",previous==""?"":origin+"/comic/"+comic+"/"+previous,"next",next==""?"":origin+"/comic/"+comic+"/"+next);
     }
    }
    if(J.Arr(J.Get(chapter,"images")).Length==0)throw new InvalidOperationException(new Uri(link).Host+" 没有返回可读取的图片；可打开来源网页查看正常登录或加载提示");return chapter;
   }
   if(action=="resolve")return await Resolve(link).ConfigureAwait(false);
   await InitializeAsync().ConfigureAwait(false);
   if(action=="sources")return J.Map("sources",J.Get(api.Config,"sources"),"hosts",J.Get(api.Config,"hosts"),"origin",api.Origin,"isFallback",J.Get(api.Config,"isFallback",false),"warning",J.S(api.Config,"warning"));
   if(action=="home") {
    if(kind=="comic") {
     var data=await api.RequestAsync("GET","/getHotComic",null).ConfigureAwait(false);
     var sections=new List<object>();foreach(string key in new[]{"play","collection","schedule"}){var items=J.Arr(J.Get(data,key)).Select(x=>Item(x,"comic")).ToArray();if(items.Length>0)sections.Add(J.Map("title",key=="play"?"热门阅读":key=="collection"?"热门收藏":"最近更新","items",items));}return J.Map("sections",sections.ToArray());
    }
    if(api.Origin==null){string url="https://www.agedm.io/";return J.Map("sections",new[]{J.Map("title","AGE 来源首页","items",SourceAdapters.ParseSearch(url,await sources.GetTextAsync(url).ConfigureAwait(false),"video").Take(30).ToArray())},"warning",J.S(api.Config,"warning"));}
    return NormalizeHome(await api.RequestAsync("GET","/mobile/home",new Dictionary<string,string>{{"source",J.S(args,"source","嘀哩哩")}}).ConfigureAwait(false),kind);
   }
   if(action=="search") {
    string key=J.S(args,"key").Trim();if(key=="")throw new ArgumentException("请输入搜索内容");
    int page=J.Int(J.Get(args,"page"));
    if(kind=="comic") {
     string comicUrl="https://www.manwaai.cc/api/search?type=mh&keyword="+Uri.EscapeDataString(key)+"&page="+(page+1)+"&pageSize=24";
     var comicData=J.Parse(await sources.GetTextAsync(comicUrl).ConfigureAwait(false));if(J.Int(J.Get(comicData,"code"))!=200)throw new InvalidOperationException(J.S(comicData,"msg","漫画搜索失败"));
     var comicItems=J.Arr(J.Get(J.Get(comicData,"data"),"list")).Select(x=>J.Map("title",J.S(x,"name",J.S(x,"title")),"link",SourceAdapters.FixUrl(comicUrl,J.S(x,"url","/comic/"+J.S(x,"id"))),"img",J.S(x,"cover"),"kind","comic","source","www.manwaai.cc")).ToArray();
     return J.Map("items",comicItems,"errors",new object[0],"page",page);
    }
    if(api.Origin==null){string url="https://www.agedm.io/search?query="+Uri.EscapeDataString(key)+"&page="+(page+1);return J.Map("items",SourceAdapters.ParseSearch(url,await sources.GetTextAsync(url).ConfigureAwait(false),kind),"errors",new[]{J.S(api.Config,"warning")},"page",page);}
    var result=await api.RequestAsync("POST","/search2",new Dictionary<string,string>{{"type",J.S(args,"type","DEFAULT")},{"key",key},{"page",page.ToString()}}).ConfigureAwait(false);
    var urls=SearchUrls(result).ToList();var direct=new List<object>();
    foreach(var batch in J.Arr(result)) {
     if(J.S(batch,"status")=="ERROR")throw new InvalidOperationException(J.S(batch,"msg","搜索来源失败"));
     if(J.S(batch,"title")!="")direct.Add(Item(batch,kind));
    }
    string filter=J.S(args,"source");if(filter!=""&&filter!="全部")urls=urls.Where(u=>new Uri(u).Host==filter).ToList();
    var fetched=await Task.WhenAll(urls.Distinct().Take(16).Select(async u=>{try{return J.Map("items",SourceAdapters.ParseSearch(u,await sources.GetTextAsync(u).ConfigureAwait(false),kind),"error","");}catch(Exception e){return J.Map("items",new object[0],"error",new Uri(u).Host+"："+e.GetBaseException().Message);}})).ConfigureAwait(false);
    var itemsAll=direct.Concat(fetched.SelectMany(x=>J.Arr(J.Get(x,"items")))).GroupBy(x=>J.S(x,"link")).Select(x=>x.First()).ToArray();
    var errors=fetched.Select(x=>J.S(x,"error")).Where(x=>x!="").Cast<object>().ToArray();
    return J.Map("items",itemsAll,"errors",errors,"page",page,"queriedSources",urls.Distinct().ToArray());
   }
   throw new ArgumentException("未支持的内容操作："+action);
  }
  private async Task<object> Resolve(string link) {
   if(MediaResolver.IsMedia(link))return J.Map("url",link,"referer",link,"browserFallback",false);
   string html="",failure="";
   if(new Uri(link).Host.Contains("wuzhoupai.com")||new Uri(link).Host.Contains("agedm.io"))try{return await AgePlayback.ResolveAsync(link).ConfigureAwait(false);}catch(Exception e){return J.Map("url","","referer",link,"browserFallback",true,"message",e.GetBaseException().Message);}
   try {html=await sources.GetTextAsync(link).ConfigureAwait(false);}catch(Exception e){failure=e.GetBaseException().Message;}
   var candidates=MediaResolver.ExtractCandidates(link,html);
   if(candidates.Length>0)return J.Map("url",candidates[0],"referer",link,"browserFallback",false);
   try {
    await InitializeAsync().ConfigureAwait(false);
    var data=await api.RequestAsync("GET","/mobile/animePlay",new Dictionary<string,string>{{"link",link}}).ConfigureAwait(false);
    string candidate=FindMedia(data);if(candidate!="")return J.Map("url",candidate,"referer",link,"browserFallback",false);
   }catch(Exception e){failure=e.GetBaseException().Message;}
   return J.Map("url","","referer",link,"browserFallback",true,"message",failure==""?"来源需要运行网页播放器":failure);
  }
  private static string FindMedia(object value) {
   if(value is string){string s=(string)value;return s.StartsWith("http")&&MediaResolver.IsMedia(s)?s:"";}
   foreach(var pair in J.Obj(value)){string found=FindMedia(pair.Value);if(found!="")return found;}
   foreach(var item in J.Arr(value)){string found=FindMedia(item);if(found!="")return found;}return "";
  }
  public void Dispose(){api.Dispose();sources.Dispose();}
 }
}
