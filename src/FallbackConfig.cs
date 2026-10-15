using System;
using System.IO;
namespace CiyuanWindows {
 public static class FallbackConfig {
  public static object Load(string reason){using(var stream=typeof(FallbackConfig).Assembly.GetManifestResourceStream("CiyuanFallback"))using(var reader=new StreamReader(stream)){var config=J.Obj(J.Parse(reader.ReadToEnd()));config["isFallback"]=true;config["warning"]="原服务暂时未连接，使用 APK 中筛选的备用来源配置。"+reason;return config;}}
 }
}
