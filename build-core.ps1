param([string]$AppRoot = $PSScriptRoot)
$ErrorActionPreference = 'Stop'
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$coreFiles = @('JsonData.cs','Transport.cs','StateStore.cs','ApiClient.cs','SourceAdapters.cs','MediaResolver.cs','ContentClient.cs','AssetProxy.cs','BridgePolicy.cs','AgePlayback.cs','FallbackConfig.cs') | ForEach-Object {Join-Path $AppRoot ('src\' + $_)}
$coreOutput = Join-Path $AppRoot 'build\CiyuanCore.dll'
$angleSharp=Join-Path $AppRoot 'vendor\anglesharp\lib\net472\AngleSharp.dll'
$memory=Join-Path $AppRoot 'vendor\system.memory\lib\net461\System.Memory.dll'
& $compiler /nologo /target:library "/out:$coreOutput" /r:System.Web.Extensions.dll /r:System.Net.Http.dll "/r:$angleSharp" "/r:$memory" "/resource:$AppRoot\ui\fallback.json,CiyuanFallback" $coreFiles
if ($LASTEXITCODE -ne 0) {throw 'Core compilation failed.'}
