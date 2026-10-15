param([string]$AppRoot = $PSScriptRoot)
$ErrorActionPreference = 'Stop'
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$testOutput = Join-Path $AppRoot 'build\CoreTests.exe'
& $compiler /nologo /target:exe /out:$testOutput /r:System.Web.Extensions.dll (Join-Path $AppRoot 'tests\CoreTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Core test harness compilation failed.' }
& $testOutput $AppRoot
exit $LASTEXITCODE
