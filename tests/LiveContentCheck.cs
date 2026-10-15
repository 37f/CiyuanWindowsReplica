using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CiyuanWindows;
class LiveContentCheck {
 static int Main(string[] args){try{Run(args).GetAwaiter().GetResult();return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
 static async Task Run(string[] args){using(var client=new ContentClient()){
  var search=await client.InvokeAsync("search",J.Map("key","青之芦苇","page",0,"source","www.agedm.io"));
  File.WriteAllText(Path.Combine(args[0],"live-search.json"),J.Stringify(search));Console.WriteLine("SEARCH "+J.Arr(J.Get(search,"items")).Length+"; errors="+J.Stringify(J.Get(search,"errors")));
  if(J.Arr(J.Get(search,"items")).Length==0)throw new Exception("Live search returned no items");
  var detail=await client.InvokeAsync("detail",J.Map("link","https://www.agedm.io/detail/20260328","kind","video"));
  File.WriteAllText(Path.Combine(args[0],"live-video-detail.json"),J.Stringify(detail));
  object selected=null;foreach(var line in J.Arr(J.Get(detail,"lines")))if(J.S(line,"name")=="bfzym3u8")selected=line;if(selected==null)throw new Exception("Live HLS line unavailable");var episode=J.Arr(J.Get(selected,"episodes"))[0];
  var media=await client.InvokeAsync("resolve",J.Map("link",J.S(episode,"link")));
  File.WriteAllText(Path.Combine(args[0],"live-media.json"),J.Stringify(media));Console.WriteLine("MEDIA "+J.Stringify(media));
  if(J.S(media,"url")==""||!MediaResolver.IsMedia(J.S(media,"url")))throw new Exception("Live media URL was not resolved");
  using(var proxy=new AssetProxy()){var manifest=await proxy.FetchAsync(J.S(media,"url"),J.S(media,"referer"),"");if(manifest.Status!=200)throw new Exception("Live manifest failed");Console.WriteLine("MANIFEST "+manifest.Status+" "+manifest.Body.Length+" bytes");}
  var comic=await client.InvokeAsync("detail",J.Map("link","https://www.manwaai.cc/comic/1728","kind","comic"));
  File.WriteAllText(Path.Combine(args[0],"live-comic-detail.json"),J.Stringify(comic));
  var chapter=J.Arr(J.Get(J.Arr(J.Get(comic,"lines"))[0],"episodes"))[0];Console.WriteLine("CHAPTER "+J.Stringify(chapter));
  var images=await client.InvokeAsync("chapter",J.Map("link",J.S(chapter,"link")));
  File.WriteAllText(Path.Combine(args[0],"live-chapter.json"),J.Stringify(images));Console.WriteLine("IMAGES "+J.Arr(J.Get(images,"images")).Length);
 }}
}
