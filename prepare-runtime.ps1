param([string]$AppRoot=$PSScriptRoot)
$ErrorActionPreference='Stop'
$copies=@(
 @('anglesharp\lib\net472\AngleSharp.dll','AngleSharp.dll'),
 @('system.text.encoding.codepages\lib\net462\System.Text.Encoding.CodePages.dll','System.Text.Encoding.CodePages.dll'),
 @('system.memory\lib\net461\System.Memory.dll','System.Memory.dll'),
 @('system.runtime.compilerservices.unsafe\lib\net461\System.Runtime.CompilerServices.Unsafe.dll','System.Runtime.CompilerServices.Unsafe.dll'),
 @('system.buffers\lib\net461\System.Buffers.dll','System.Buffers.dll'),
 @('system.numerics.vectors\lib\net46\System.Numerics.Vectors.dll','System.Numerics.Vectors.dll'),
 @('webview2\lib\net462\Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.Core.dll'),
 @('webview2\lib\net462\Microsoft.Web.WebView2.WinForms.dll','Microsoft.Web.WebView2.WinForms.dll'),
 @('webview2\runtimes\win-x64\native\WebView2Loader.dll','WebView2Loader.dll')
)
foreach($copy in $copies){Copy-Item -LiteralPath (Join-Path $AppRoot ('vendor\'+$copy[0])) -Destination (Join-Path $AppRoot ('build\'+$copy[1])) -Force}
