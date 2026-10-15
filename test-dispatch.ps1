param([string]$AppRoot=$PSScriptRoot)
$ErrorActionPreference='Stop'
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /nologo /r:System.Web.Extensions.dll "/out:$PSScriptRoot\build\DispatchTests.exe" "$PSScriptRoot\tests\DispatchTests.cs"
Copy-Item -LiteralPath "$PSScriptRoot\app.config" -Destination "$PSScriptRoot\build\DispatchTests.exe.config" -Force
& "$PSScriptRoot\build\DispatchTests.exe" $PSScriptRoot (Split-Path $PSScriptRoot)
if($LASTEXITCODE-ne 0){throw 'Dispatch tests failed'}
