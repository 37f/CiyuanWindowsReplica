using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;

namespace CiyuanWindows {
    public static class MediaResolver {
        public static bool IsMedia(string url){string value=url??"";return !Regex.IsMatch(value,@"/play/adposter\.mp4(?:[?#]|$)|kjsaas-sh\.oss-cn-shanghai\.aliyuncs\.com|oss-cn-shanghai\.aliyuncs\.com/u/.*20240818-936952",RegexOptions.IgnoreCase)&&Regex.IsMatch(value,@"\.(m3u8|mp4|webm|m4v|mov)(?:[?#]|$)",RegexOptions.IgnoreCase);}
        public static object[] ExtractCandidates(string page,string html) {
            var result=new List<object>();var seen=new HashSet<string>();
            Action<string> add=value=>{string url=SourceAdapters.FixUrl(page,(value??"").Replace("\\/","/"));if(url!=""&&IsMedia(url)&&seen.Add(url))result.Add(url);};
            if(IsMedia(page))add(page);
            var doc=new HtmlParser().ParseDocument(html??"");
            foreach(var element in doc.QuerySelectorAll("video[src],source[src]"))add(element.GetAttribute("src"));
            foreach(Match match in Regex.Matches(html??"",@"(?:var\s+)?player_\w+\s*=\s*(\{[^;\r\n]+\})")) {
                try {object data=J.Parse(match.Groups[1].Value);string url=J.S(data,"url");int encrypt=J.Int(J.Get(data,"encrypt"));if(encrypt==1)url=Uri.UnescapeDataString(url);if(encrypt==2)url=Encoding.UTF8.GetString(Convert.FromBase64String(url));add(url);}catch{}
            }
            foreach(Match match in Regex.Matches(html??"",@"https?(?::|%3[aA])(?:/|\\/|%2[fF])(?:/|\\/|%2[fF])[^'""\s<>]+\.(?:m3u8|mp4|webm)(?:\?[^'""\s<>]*)?",RegexOptions.IgnoreCase))add(Uri.UnescapeDataString(match.Value));
            foreach(var frame in doc.QuerySelectorAll("iframe[src]")) {
                string src=SourceAdapters.FixUrl(page,frame.GetAttribute("src"));if(src=="")continue;
                var match=Regex.Match(src,@"[?&](?:url|v|play)=([^&]+)");if(match.Success)add(Uri.UnescapeDataString(match.Groups[1].Value));
            }
            return result.ToArray();
        }
    }
}
