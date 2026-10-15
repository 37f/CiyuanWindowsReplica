param([string]$AppRoot=$PSScriptRoot)
$ErrorActionPreference='Stop'
New-Item -ItemType Directory -Path (Join-Path $AppRoot 'build') -Force | Out-Null
& "$AppRoot\prepare-runtime.ps1"
& "$AppRoot\build-core.ps1"
$compiler='C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$target=Join-Path $AppRoot 'build\次元库 Windows 复刻版.exe'
& $compiler /nologo /target:winexe /platform:x64 "/win32icon:$AppRoot\ui\app.ico" "/out:$target" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Net.Http.dll /r:System.Web.dll /r:System.Web.Extensions.dll "/r:$AppRoot\build\CiyuanCore.dll" "/r:$AppRoot\build\Microsoft.Web.WebView2.Core.dll" "/r:$AppRoot\build\Microsoft.Web.WebView2.WinForms.dll" "$AppRoot\src\Program.cs" "$AppRoot\src\MainWindow.cs"
if($LASTEXITCODE-ne 0){throw 'Desktop compilation failed'}
Copy-Item -LiteralPath "$AppRoot\app.config" -Destination ($target+'.config') -Force
Copy-Item -LiteralPath "$AppRoot\ui" -Destination "$AppRoot\build" -Recurse -Force
Write-Output $target
