using System;
using System.IO;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Web;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace CiyuanWindows {
 public sealed class MainWindow : Form {
  private readonly WebView2 view=new WebView2();
  private readonly ContentClient content=new ContentClient();
  private readonly AssetProxy proxy=new AssetProxy();
  private readonly StateStore store;
  private readonly string dataDir,testScript;
  private object state;
  private CoreWebView2Environment environment;
  public MainWindow(string data,string script){dataDir=data;testScript=script;store=new StateStore(data);state=store.Load();Text="次元库 Windows 复刻版";Width=1280;Height=840;MinimumSize=new Size(850,600);StartPosition=FormStartPosition.CenterScreen;BackColor=Color.White;view.Dock=DockStyle.Fill;Controls.Add(view);Shown+=async (s,e)=>await Initialize();FormClosed+=(s,e)=>{content.Dispose();proxy.Dispose();view.Dispose();};}
  private async Task Initialize(){
   try{
    File.AppendAllText(Path.Combine(dataDir,"startup.log"),DateTime.UtcNow.ToString("o")+" environment begin\n");
    environment=await CoreWebView2Environment.CreateAsync(null,Path.Combine(dataDir,"WebView2"),new CoreWebView2EnvironmentOptions("--autoplay-policy=no-user-gesture-required"));
    File.AppendAllText(Path.Combine(dataDir,"startup.log"),"environment ready\n");
    await view.EnsureCoreWebView2Async(environment);
    File.AppendAllText(Path.Combine(dataDir,"startup.log"),"view ready\n");
    view.CoreWebView2.Settings.AreHostObjectsAllowed=false;
    view.CoreWebView2.Settings.IsWebMessageEnabled=true;
    view.CoreWebView2.SetVirtualHostNameToFolderMapping("app.ciyuan.local",Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ui"),CoreWebView2HostResourceAccessKind.DenyCors);
    view.CoreWebView2.NavigationStarting+=(s,e)=>{if(!BridgePolicy.Trusted(e.Uri)){e.Cancel=true;}};
    view.CoreWebView2.NewWindowRequested+=(s,e)=>{e.Handled=true;if(HttpUrl(e.Uri))OpenBrowser(e.Uri);};
    view.CoreWebView2.PermissionRequested+=(s,e)=>e.State=CoreWebView2PermissionState.Deny;
    view.CoreWebView2.WebMessageReceived+=Message;
    view.CoreWebView2.AddWebResourceRequestedFilter(AssetProxy.Origin+"/*",CoreWebView2WebResourceContext.All);
    view.CoreWebView2.WebResourceRequested+=Resource;
    view.CoreWebView2.NavigationCompleted+=(s,e)=>File.AppendAllText(Path.Combine(dataDir,"startup.log"),"navigation "+e.IsSuccess+" "+e.WebErrorStatus+"\n");
    if(testScript!="")view.CoreWebView2.NavigationCompleted+=async (s,e)=>{if(e.IsSuccess&&File.Exists(testScript)){await Task.Delay(500);await view.CoreWebView2.ExecuteScriptAsync(File.ReadAllText(testScript));}};
    view.CoreWebView2.Navigate("https://app.ciyuan.local/index.html");
    File.AppendAllText(Path.Combine(dataDir,"startup.log"),"navigate called\n");
   }catch(WebView2RuntimeNotFoundException){MessageBox.Show(this,"需要 Microsoft Edge WebView2 运行环境。请安装后重新打开程序。","缺少运行环境");Close();}
   catch(Exception e){File.AppendAllText(Path.Combine(dataDir,"startup.log"),e.ToString());MessageBox.Show(this,"界面启动失败："+e.GetBaseException().Message,"启动错误");Close();}
  }
  private async void Resource(object sender,CoreWebView2WebResourceRequestedEventArgs e){var deferral=e.GetDeferral();try{
    var query=HttpUtility.ParseQueryString(new Uri(e.Request.Uri).Query);string range=e.Request.Headers.Contains("Range")?e.Request.Headers.GetHeader("Range"):"";
    var result=await proxy.FetchAsync(query["url"],query["ref"],range);
    if(!IsDisposed)e.Response=environment.CreateWebResourceResponse(new MemoryStream(result.Body),result.Status,result.Status==206?"Partial Content":"OK",result.Headers);
   }catch(Exception error){if(!IsDisposed)e.Response=environment.CreateWebResourceResponse(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(error.GetBaseException().Message)),502,"Bad Gateway","Content-Type: text/plain; charset=utf-8\r\nAccess-Control-Allow-Origin: https://app.ciyuan.local\r\n");}finally{deferral.Complete();}}
  private async void Message(object sender,CoreWebView2WebMessageReceivedEventArgs e){
   if(!BridgePolicy.Trusted(e.Source)||e.WebMessageAsJson.Length>8000000)return;
   object id=null,result=null;string error=null;
   try{var request=J.Parse(e.WebMessageAsJson);id=J.Get(request,"id");string action=J.S(request,"action");var args=J.Obj(J.Get(request,"args"));
    if(action=="loadState")result=state;
    else if(action=="saveState"){var candidate=J.Get(args,"state");if(J.Get(candidate,"favorites")==null||J.Get(candidate,"history")==null||J.Get(candidate,"settings")==null)throw new ArgumentException("个人记录格式无效");store.Save(candidate);state=candidate;result=true;}
    else if(action=="openBrowser"){string link=J.S(args,"link");if(!HttpUrl(link))throw new ArgumentException("来源地址无效");OpenBrowser(link,J.Get(args,"context"));result=true;}
    else if(action=="testReport"&&testScript!=""){File.WriteAllText(Path.Combine(dataDir,"acceptance.json"),J.Stringify(args));result=true;}
    else if(action=="testCapture"&&testScript!=""){string name=Path.GetFileName(J.S(args,"name","capture.png"));using(var output=File.Create(Path.Combine(dataDir,name)))await view.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,output);result=true;}
    else if(action=="testFullscreen"&&testScript!=""){result=await view.CoreWebView2.CallDevToolsProtocolMethodAsync("Runtime.evaluate",J.Stringify(J.Map("expression","document.getElementById('fullscreen').click()","userGesture",true)));}
    else if(action=="testCloseSource"&&testScript!=""){var source=new SourceWindow(environment,"https://www.agedm.io/",false);source.Show(this);source.Close();result=true;}
    else if(action=="testClose"&&testScript!=""){BeginInvoke(new Action(Close));result=true;}
    else result=await content.InvokeAsync(action,args);
   }catch(Exception failure){error=failure.GetBaseException().Message;}
   if(!IsDisposed&&view.CoreWebView2!=null&&BridgePolicy.Trusted(view.Source.AbsoluteUri))view.CoreWebView2.PostWebMessageAsJson(J.Stringify(J.Map("id",id,"result",result,"error",error)));
  }
  private static bool HttpUrl(string url){Uri uri;return Uri.TryCreate(url,UriKind.Absolute,out uri)&&(uri.Scheme=="http"||uri.Scheme=="https");}
  private void OpenBrowser(string url,object context=null){var browser=new SourceWindow(environment,url,context!=null);if(context!=null)browser.MediaSelected+=async (media,referer)=>{if(!IsDisposed)await view.CoreWebView2.ExecuteScriptAsync("window.acceptCapturedMedia("+J.Stringify(media)+","+J.Stringify(referer)+","+J.Stringify(context)+")");};browser.Show(this);}
 }
 public sealed class SourceWindow : Form {
  public event Action<string,string> MediaSelected;
  private readonly WebView2 browser=new WebView2();
  private readonly Button use=new Button{Text="发现媒体后可切换桌面播放器",Dock=DockStyle.Top,Height=42,Enabled=false};
  private string media="",referer;
  public SourceWindow(CoreWebView2Environment environment,string url,bool transfer){referer=url;Text="来源网页 — "+new Uri(url).Host;Width=1100;Height=780;browser.Dock=DockStyle.Fill;Controls.Add(browser);Controls.Add(use);use.Visible=transfer;Shown+=async(s,e)=>{
   await browser.EnsureCoreWebView2Async(environment);browser.CoreWebView2.Settings.IsWebMessageEnabled=false;browser.CoreWebView2.Settings.AreHostObjectsAllowed=false;
   browser.CoreWebView2.PermissionRequested+=(a,b)=>b.State=CoreWebView2PermissionState.Deny;
   browser.CoreWebView2.NewWindowRequested+=(a,b)=>b.Handled=true;
   browser.CoreWebView2.NavigationStarting+=(a,b)=>{Uri uri;if(!Uri.TryCreate(b.Uri,UriKind.Absolute,out uri)||(uri.Scheme!="http"&&uri.Scheme!="https")||uri.Host.EndsWith(".ciyuan.local"))b.Cancel=true;};
   browser.CoreWebView2.AddWebResourceRequestedFilter("*",CoreWebView2WebResourceContext.All);
   browser.CoreWebView2.WebResourceRequested+=(a,b)=>{if(MediaResolver.IsMedia(b.Request.Uri)){media=b.Request.Uri;use.Enabled=true;use.Text="使用桌面播放器打开已发现的视频";}};
   browser.CoreWebView2.Navigate(url);
  };use.Click+=(s,e)=>{if(media!=""&&MediaSelected!=null){MediaSelected(media,referer);Close();}};FormClosed+=(s,e)=>browser.Dispose();}
 }
}
