using System;
namespace CiyuanWindows {
 public static class BridgePolicy {
  public static bool Trusted(string source){Uri uri;return Uri.TryCreate(source,UriKind.Absolute,out uri)&&uri.Scheme=="https"&&uri.Host=="app.ciyuan.local"&&uri.IsDefaultPort&&uri.AbsolutePath=="/index.html";}
 }
}
