using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace CiyuanWindows {
    public sealed class SourceAdapters : IDisposable {
        private readonly HttpClient http;
        public SourceAdapters() {
            http=new HttpClient(new HttpClientHandler {AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate});
            http.Timeout=TimeSpan.FromSeconds(15);
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/150.0.0.0 Safari/537.36");
        }
        public static string CleanUrl(string url) {return (url??"").Split(new[]{"@headers@"},StringSplitOptions.None)[0].Trim();}
        public static string FixUrl(string page,string value) {
            if(String.IsNullOrWhiteSpace(value)||value.StartsWith("#"))return "";
            Uri result;
            if(!Uri.TryCreate(new Uri(CleanUrl(page)),WebUtility.HtmlDecode(value.Trim()),out result))return "";
            return result.Scheme=="http"||result.Scheme=="https"?result.AbsoluteUri:"";
        }
        public async Task<string> GetTextAsync(string url) {
            url=CleanUrl(url); Uri parsed;
            if(!Uri.TryCreate(url,UriKind.Absolute,out parsed)||(parsed.Scheme!="http"&&parsed.Scheme!="https"))throw new ArgumentException("来源链接无效");
            using(var request=new HttpRequestMessage(HttpMethod.Get,url)) {
                request.Headers.Referrer=new Uri(parsed.GetLeftPart(UriPartial.Authority)+"/");
                using(var response=await http.SendAsync(request).ConfigureAwait(false)) {
                    if(!response.IsSuccessStatusCode)throw new HttpRequestException("来源 "+parsed.Host+" 返回 HTTP "+(int)response.StatusCode);
                    if(response.Content.Headers.ContentLength>12000000)throw new HttpRequestException("来源页面过大");
                    return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                }
            }
        }
        private static string Text(IElement element){return element==null?"":Regex.Replace(element.TextContent??"",@"\s+"," ").Trim();}
        private static IElement First(IParentNode node,params string[] selectors){foreach(var selector in selectors){var element=node.QuerySelector(selector);if(element!=null)return element;}return null;}
        private static string Cover(string url,IElement node) {
            if(node==null)return ""; var image=node.LocalName=="img"?node:node.QuerySelector("img");
            if(image!=null)foreach(string key in new[]{"data-original-cover","data-original","data-src","data-lazy-src","src"}){string value=image.GetAttribute(key);if(!String.IsNullOrWhiteSpace(value))return FixUrl(url,value);}
            string style=node.GetAttribute("style")??"";var match=Regex.Match(style,@"url\(['"" ]?([^)'"" ]+)");return match.Success?FixUrl(url,match.Groups[1].Value):"";
        }
        public static object[] ParseSearch(string url,string html,string kind) {
            var document=new HtmlParser().ParseDocument(html);var result=new List<object>();var seen=new HashSet<string>();
            IElement[] cards=document.QuerySelectorAll("div.video_cover_wrapper,.module-search-item,.module-item,.stui-vodlist__box,.myui-vodlist__box,li.fed-list-item,.comic-card,.book-item,.comic-item,.video-item").ToArray();
            if(cards.Length==0)cards=document.QuerySelectorAll("a[href]").Where(a=>a.QuerySelector("img")!=null).ToArray();
            foreach(var card in cards) {
                var anchor=card.LocalName=="a"?card:card.QuerySelector("a[href]");if(anchor==null)continue;
                string link=FixUrl(url,anchor.GetAttribute("href"));if(link==""||!seen.Add(link))continue;
                string title=anchor.GetAttribute("title")??"";
                if(title=="")title=Text(First(card,".title",".module-info-heading","h2","h3","h4",".fed-list-title",".book-name"));
                if(title=="")title=Text(anchor);if(title==""){var img=anchor.QuerySelector("img");title=img==null?"":img.GetAttribute("alt")??"";}
                if(title=="")continue;
                result.Add(J.Map("title",title,"link",link,"img",Cover(url,card),"update",Text(First(card,".module-item-note",".pic-text",".fed-list-remarks",".update","span")),"kind",kind,"source",new Uri(url).Host));
            }
            return result.ToArray();
        }
        public static object ParseDetail(string url,string html,string kind) {
            var doc=new HtmlParser().ParseDocument(html);
            string title=Text(First(doc,"#page-title","h2.video_detail_title",".module-info-heading h1",".stui-content__detail h1",".myui-content__detail h1","h1","h2"));
            if(title=="")title=Regex.Split(doc.Title??"",@"[_|\-]")[0].Trim();
            string intro=Text(First(doc,".comic-desc",".video_detail_desc",".module-info-introduction-content",".stui-content__desc",".myui-content__desc",".vod_content",".detail-content",".fed-deta-content"));
            var cover=First(doc,"img.comic-cover",".video_detail_cover",".module-item-pic",".stui-content__thumb",".myui-content__thumb",".fed-deta-info .fed-list-pics");
            var lines=new List<object>();var containers=doc.QuerySelectorAll(kind=="comic"?"#chapter-grid-container,.chapter-list,.chapters,.chapterlist,.list-chapter,.chapter-items,.detail-list-select":"ul.video_detail_episode,.stui-content__playlist,.myui-content__list,.module-play-list-content,.playlist,.fed-play-item .fed-play-list");
            var global=new HashSet<string>();
            foreach(var container in containers) {
                var episodes=new List<object>();
                foreach(var a in container.QuerySelectorAll("a[href]")) {
                    string name=Text(a),link=FixUrl(url,a.GetAttribute("href"));
                    if(link==""||name==""||!global.Add(link))continue;
                    // Keep ordinary guest access: do not label a paid chapter as free.
                    if(kind=="comic"&&(name.IndexOf("VIP",StringComparison.OrdinalIgnoreCase)>=0||a.QuerySelector(".vip,.locked,.lock")!=null))continue;
                    episodes.Add(J.Map("name",name,"link",link));
                }
                if(episodes.Count>0)lines.Add(J.Map("name",kind=="comic"?"章节目录":"线路 "+(lines.Count+1),"episodes",episodes.ToArray()));
            }
            if(lines.Count==0) {
                var episodes=new List<object>();
                foreach(var a in doc.QuerySelectorAll("a[href]")) {
                    string href=a.GetAttribute("href")??"",name=Text(a);
                    if((kind=="comic"?Regex.IsMatch(href,@"/(chapter|read)/|/manga/.+/.+|/comic/.+/\d+",RegexOptions.IgnoreCase):Regex.IsMatch(href,@"/(play|vodplay)/",RegexOptions.IgnoreCase))&&name!=""&&!name.Contains("VIP")) {
                        string link=FixUrl(url,href);if(link!=""&&global.Add(link))episodes.Add(J.Map("name",name,"link",link));
                    }
                }
                if(episodes.Count>0)lines.Add(J.Map("name",kind=="comic"?"章节目录":"播放列表","episodes",episodes.ToArray()));
            }
            return J.Map("title",title,"img",Cover(url,cover),"link",url,"intro",intro,"kind",kind,"source",new Uri(url).Host,"lines",lines.ToArray(),"browserFallback",lines.Count==0);
        }
        public static object ParseChapter(string url,string html) {
            var doc=new HtmlParser().ParseDocument(html);var images=new List<object>();var seen=new HashSet<string>();
            foreach(var img in doc.QuerySelectorAll(".chapter-img-box img,.readerContainer img,.reading-content img,#comicContain img,#manga img,.comic-content img,.manga-reading img,.chapter-content img")) {
                string image=Cover(url,img);if(image!=""&&seen.Add(image))images.Add(image);
            }
            string previous="",next="";
            foreach(var a in doc.QuerySelectorAll("a[href]")){string text=Text(a);if(text.Contains("上一章")||text.Contains("上一话"))previous=FixUrl(url,a.GetAttribute("href"));if(text.Contains("下一章")||text.Contains("下一话"))next=FixUrl(url,a.GetAttribute("href"));}
            return J.Map("title",doc.Title??"","link",url,"images",images.ToArray(),"previous",previous,"next",next);
        }
        public static object ParseCatalog(string url,string html) {
            var doc=new HtmlParser().ParseDocument(html);var filters=new List<object>();var seen=new HashSet<string>();string next="",previous="";
            foreach(var a in doc.QuerySelectorAll("a[href*='/catalog/']")){string name=Text(a),link=FixUrl(url,a.GetAttribute("href"));if(name.Contains("下一页")||name==">"){next=link;continue;}if(name.Contains("上一页")||name=="<"){previous=link;continue;}int page;if(name==""||name=="目录"||name=="首页"||name=="末页"||Int32.TryParse(name,out page)||!seen.Add(link))continue;filters.Add(J.Map("name",name,"link",link));}
            return J.Map("items",ParseSearch(url,html,"video"),"filters",filters.ToArray(),"next",next,"previous",previous,"link",url);
        }
        public static object ParseCatalogApi(string url,object data) {
            var query=new Dictionary<string,string>();foreach(string pair in new Uri(url).Query.TrimStart('?').Split('&')){string[] parts=pair.Split(new[]{'='},2);if(parts.Length==2)query[Uri.UnescapeDataString(parts[0])]=Uri.UnescapeDataString(parts[1]);}
            if(!query.ContainsKey("page"))query["page"]="1";if(!query.ContainsKey("size"))query["size"]="20";int page=J.Int(query["page"],1),size=J.Int(query["size"],20);
            Func<Dictionary<string,string>,string> address=q=>"https://api.agedm.io/v2/catalog?"+String.Join("&",q.Select(x=>Uri.EscapeDataString(x.Key)+"="+Uri.EscapeDataString(x.Value)));
            var items=J.Arr(J.Get(data,"videos")).Select(x=>(object)J.Map("title",J.S(x,"name"),"img",J.S(x,"cover"),"link","https://www.agedm.io/detail/"+J.S(x,"id"),"update",J.S(x,"uptodate"),"kind","video","source","www.agedm.io")).ToArray();
            var filters=new List<object>();
            foreach(var group in new[]{new[]{"region","地区","all","日本","中国","欧美"},new[]{"year","年份","all","2026","2025","2024","2023","2022","2021"},new[]{"label","类型","all","搞笑","运动","热血","战斗","校园","青春","爱情","冒险","治愈","悬疑","科幻","剧情"}}){for(int i=2;i<group.Length;i++){var nextQuery=new Dictionary<string,string>(query);nextQuery[group[0]]=group[i];nextQuery["page"]="1";filters.Add(J.Map("name",group[1]+" · "+(group[i]=="all"?"全部":group[i]),"link",address(nextQuery)));}}
            var next=new Dictionary<string,string>(query);next["page"]=(page+1).ToString();var previous=new Dictionary<string,string>(query);previous["page"]=(page-1).ToString();
            return J.Map("items",items,"filters",filters.ToArray(),"next",page*size<J.Int(J.Get(data,"total"))?address(next):"","previous",page>1?address(previous):"","link",url,"browserLink","https://www.agedm.io/catalog/all-all-all-all-all-time-1");
        }
        public void Dispose(){http.Dispose();}
    }
}
