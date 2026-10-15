param([string]$Destination=(Join-Path $PSScriptRoot 'package-stage'))
$ErrorActionPreference='Stop'
$runtime=Join-Path $Destination '程序包'
$source=Join-Path $Destination '源码'
New-Item -ItemType Directory -Path $runtime,$source -Force | Out-Null
$runtimeFiles=@('次元库 Windows 复刻版.exe','次元库 Windows 复刻版.exe.config','CiyuanCore.dll','AngleSharp.dll','System.Text.Encoding.CodePages.dll','System.Memory.dll','System.Runtime.CompilerServices.Unsafe.dll','System.Buffers.dll','System.Numerics.Vectors.dll','Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.WinForms.dll','WebView2Loader.dll')
foreach($file in $runtimeFiles){Copy-Item -LiteralPath (Join-Path $PSScriptRoot ('build\'+$file)) -Destination (Join-Path $runtime $file) -Force}
Copy-Item -LiteralPath "$PSScriptRoot\ui" -Destination $runtime -Recurse -Force
foreach($document in @('使用说明.md','功能与来源对照.md')){if(Test-Path -LiteralPath (Join-Path $PSScriptRoot $document)){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $document) -Destination $runtime -Force;Copy-Item -LiteralPath (Join-Path $PSScriptRoot $document) -Destination $source -Force;}}
foreach($directory in @('src','ui','tests','licenses')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $directory) -Destination $source -Recurse -Force}
foreach($file in @('app.config','build.ps1','build-core.ps1','prepare-runtime.ps1','test-core.ps1','test-content.ps1','test-dispatch.ps1','test-all.ps1','test-live-content.ps1','package.ps1','DEPENDENCIES.md','README.md')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination $source -Force}
$vendorFiles=@('anglesharp\lib\net472\AngleSharp.dll','system.text.encoding.codepages\lib\net462\System.Text.Encoding.CodePages.dll','system.memory\lib\net461\System.Memory.dll','system.runtime.compilerservices.unsafe\lib\net461\System.Runtime.CompilerServices.Unsafe.dll','system.buffers\lib\net461\System.Buffers.dll','system.numerics.vectors\lib\net46\System.Numerics.Vectors.dll','webview2\lib\net462\Microsoft.Web.WebView2.Core.dll','webview2\lib\net462\Microsoft.Web.WebView2.WinForms.dll','webview2\runtimes\win-x64\native\WebView2Loader.dll')
foreach($file in $vendorFiles){$target=Join-Path $source ('vendor\'+$file);New-Item -ItemType Directory -Path (Split-Path $target) -Force | Out-Null;Copy-Item -LiteralPath (Join-Path $PSScriptRoot ('vendor\'+$file)) -Destination $target -Force}
foreach($directory in Get-ChildItem -LiteralPath "$PSScriptRoot\vendor" -Directory){foreach($file in Get-ChildItem -LiteralPath $directory.FullName -File | Where-Object {$_.Name -match 'LICENSE|NOTICE|nuspec$'}){Copy-Item -LiteralPath $file.FullName -Destination (Join-Path "$PSScriptRoot\licenses" ($directory.Name+'-'+$file.Name)) -Force}}
Copy-Item -LiteralPath "$PSScriptRoot\licenses" -Destination $runtime -Recurse -Force
Copy-Item -LiteralPath "$PSScriptRoot\licenses" -Destination $source -Recurse -Force
$records=foreach($file in Get-ChildItem -LiteralPath $runtime -Recurse -File){[pscustomobject]@{path=$file.FullName.Substring($runtime.Length+1);sha256=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant();bytes=$file.Length}}
$records | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $Destination 'runtime-files.json') -Encoding utf8
Write-Output $Destination
