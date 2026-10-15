# 固定依赖

该项目使用 Windows 自带的 .NET Framework 4.8 编译器，生成 x64 程序。源码包带有下列固定版本的最小编译依赖，不需要自动下载或全局安装开发工具。

| 依赖 | 版本 | 官方分发位置 |
| --- | --- | --- |
| Microsoft.Web.WebView2 | 1.0.4258.31 | https://www.nuget.org/packages/Microsoft.Web.WebView2/1.0.4258.31 |
| AngleSharp | 1.8.4 | https://www.nuget.org/packages/AngleSharp/1.8.4 |
| System.Text.Encoding.CodePages | 8.0.0 | https://www.nuget.org/packages/System.Text.Encoding.CodePages/8.0.0 |
| System.Memory | 4.5.5 | https://www.nuget.org/packages/System.Memory/4.5.5 |
| System.Runtime.CompilerServices.Unsafe | 6.0.0 | https://www.nuget.org/packages/System.Runtime.CompilerServices.Unsafe/6.0.0 |
| System.Buffers | 4.5.1 | https://www.nuget.org/packages/System.Buffers/4.5.1 |
| System.Numerics.Vectors | 4.5.0 | https://www.nuget.org/packages/System.Numerics.Vectors/4.5.0 |
| hls.js | 1.7.3 | https://registry.npmjs.org/hls.js/-/hls.js-1.7.3.tgz |

hls.js 分发包 SHA-512（Base64）：`MsPlx6yVW4Qv4C7mEVou4/gk/5cN2dVxOTnrNjPSmyH2f0Ln+/UkG10Ax1PE6OvrmTVPKgQYozTtBaPOuCzq7A==`。

依赖许可证与声明在 `licenses/`。`app.config` 的程序集版本重定向用于这些固定 .NET Framework 依赖。独立安装的 WebView2 Runtime 由 Microsoft Edge 更新；本包包含加载器和托管接口，不包含完整浏览器运行时。

应用只复用原服务的正常匿名内容请求及普通传输格式。没有复制用户会话、账户数据或第三方广告 SDK 授权字符串。原 APK 不参与 Windows 程序运行。
