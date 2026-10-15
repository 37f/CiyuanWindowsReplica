param([string]$AppRoot=$PSScriptRoot)
$ErrorActionPreference='Stop'
$compiler='C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$output=Join-Path $AppRoot 'build\ContentTests.exe'
& $compiler /nologo "/out:$output" /r:System.Web.Extensions.dll (Join-Path $AppRoot 'tests\ContentTests.cs')
if($LASTEXITCODE -ne 0){throw 'Content test compilation failed'}
Copy-Item -LiteralPath (Join-Path $AppRoot 'app.config') -Destination ($output+'.config') -Force
& $output $AppRoot
exit $LASTEXITCODE
