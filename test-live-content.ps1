$ErrorActionPreference='Stop'
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /nologo /r:System.Web.Extensions.dll "/r:$PSScriptRoot\build\CiyuanCore.dll" "/out:$PSScriptRoot\build\LiveContentCheck.exe" "$PSScriptRoot\tests\LiveContentCheck.cs"
Copy-Item -LiteralPath "$PSScriptRoot\app.config" -Destination "$PSScriptRoot\build\LiveContentCheck.exe.config" -Force
& "$PSScriptRoot\build\LiveContentCheck.exe" "$PSScriptRoot\build"
if($LASTEXITCODE-ne 0){throw 'Live content check failed'}
