using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;
class ContentTests {
    static Assembly Core; static int Pass,Fail;
    static JavaScriptSerializer Json=new JavaScriptSerializer();
    static object Call(string type,string method,params object[] args) {
        try {return Core.GetType("CiyuanWindows."+type,true).GetMethod(method).Invoke(null,args);}catch(TargetInvocationException e){throw e.InnerException;}
    }
    static void Eq(object got,object expected){if(!Object.Equals(got,expected))throw new Exception("Expected "+expected+", got "+got);}
    static Dictionary<string,object> Obj(object value){return (Dictionary<string,object>)value;}
    static void Test(string name,Action action){try{action();Pass++;Console.WriteLine("PASS "+name);}catch(Exception e){Fail++;Console.WriteLine("FAIL "+name+": "+e.GetBaseException().Message);}}
    static int Main(string[] args) {
        Core=Assembly.LoadFrom(Path.Combine(args[0],"build","CiyuanCore.dll"));
        Test("AGE search resolves relative link and lazy cover",()=>{
            object[] items=(object[])Call("SourceAdapters","ParseSearch","https://www.agedm.io/search?query=test","<div class='video_cover_wrapper'><a href='/detail/123' title='测试动画'><img data-original='/cover.jpg'></a><span>第01集</span></div>","video");
            Eq(items.Length,1);Eq(Obj(items[0])["title"],"测试动画");Eq(Obj(items[0])["link"],"https://www.agedm.io/detail/123");Eq(Obj(items[0])["img"],"https://www.agedm.io/cover.jpg");
        });
        Test("detail keeps episode order and separate lines",()=>{
            var detail=Obj(Call("SourceAdapters","ParseDetail","https://example.invalid/detail/123","<h1>测试动画</h1><div class='video_detail_desc'>故事简介</div><ul class='video_detail_episode'><li><a href='/play/123/1'>第1集</a></li><li><a href='/play/123/2'>第2集</a></li></ul><ul class='video_detail_episode'><li><a href='/play/123/3'>备用1</a></li></ul>","video"));
            var lines=(object[])detail["lines"];Eq(lines.Length,2);var episodes=(object[])Obj(lines[0])["episodes"];Eq(episodes.Length,2);Eq(Obj(episodes[1])["link"],"https://example.invalid/play/123/2");
        });
        Test("comic directory does not treat paid VIP chapter as free",()=>{
            var detail=Obj(Call("SourceAdapters","ParseDetail","https://example.invalid/comic/1","<h2 id='page-title'>测试漫画</h2><div id='chapter-grid-container'><a href='/chapter/1'><div class='chapter-name'>第1章</div></a><a href='/chapter/2'><div class='chapter-name'>VIP 第2章</div></a></div>","comic"));
            var lines=(object[])detail["lines"];Eq(((object[])Obj(lines[0])["episodes"]).Length,1);
        });
        Test("comic reader accepts lazy images and excludes decorative logo",()=>{
            var chapter=Obj(Call("SourceAdapters","ParseChapter","https://example.invalid/read/1/1.html","<img src='/logo.png'><div class='chapter-img-box'><img data-src='../img/1.jpg'></div><div class='chapter-img-box'><img data-src='../img/2.jpg'></div>"));
            var images=(object[])chapter["images"];Eq(images.Length,2);Eq(images[0],"https://example.invalid/read/img/1.jpg");
        });
        Test("encoded player URL resolves without accepting javascript",()=>{
            string encoded=Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("https://video.example.invalid/master.m3u8?token=abc"));
            var media=(object[])Call("MediaResolver","ExtractCandidates","https://example.invalid/play/1","<script>var player_aaaa={\"encrypt\":2,\"url\":\""+encoded+"\"};</script><iframe src='javascript:alert(1)'></iframe>");
            Eq(media.Length,1);Eq(media[0],"https://video.example.invalid/master.m3u8?token=abc");
        });
        Test("AGE advertisement is not a content media candidate",()=>{
            var media=(object[])Call("MediaResolver","ExtractCandidates","https://jx.wuzhoupai.com:8443/vip/","<script>const poster='https://175.178.227.159:7788/Play/adposter.mp4';</script>");
            Eq(media.Length,0);
        });
        Test("AGE known unavailable-content placeholder is rejected",()=>{
            var media=(object[])Call("MediaResolver","ExtractCandidates","https://jx.wuzhoupai.com:8443/vip/","<video src='https://kjjsaas-sh.oss-cn-shanghai.aliyuncs.com/u/3401405881/20240818-936952-fc31b16575e80a7562cdb1f81a39c6b0.mp4'></video>");Eq(media.Length,0);
        });
        Test("catalog keeps real source filter and next-page links",()=>{
            var catalog=Obj(Call("SourceAdapters","ParseCatalog","https://www.agedm.io/catalog/all-all-all-all-all-time-1","<div class='filter'><a href='/catalog/japan-all-all-all-all-time-1'>日本</a></div><a href='/catalog/all-all-all-all-all-time-2'>下一页</a><div class='video_cover_wrapper'><a href='/detail/1' title='测试'><img src='/1.jpg'></a></div>"));
            Eq(((object[])catalog["items"]).Length,1);Eq(((object[])catalog["filters"]).Length,1);Eq(catalog["next"],"https://www.agedm.io/catalog/all-all-all-all-all-time-2");
        });
        Test("AGE catalog API normalizes pagination and stable detail links",()=>{
            var result=Obj(Call("SourceAdapters","ParseCatalogApi","https://api.agedm.io/v2/catalog?page=1&size=1",Json.DeserializeObject("{\"total\":2,\"videos\":[{\"id\":123,\"name\":\"目录作品\",\"cover\":\"https://example.invalid/c.jpg\",\"uptodate\":\"第1集\"}]}")));
            Eq(Obj(((object[])result["items"])[0])["link"],"https://www.agedm.io/detail/123");if(!((string)result["next"]).Contains("page=2"))throw new Exception("Next page lost");
        });
        Console.WriteLine("RESULT "+Pass+" passed, "+Fail+" failed");return Fail==0?0:1;
    }
}
