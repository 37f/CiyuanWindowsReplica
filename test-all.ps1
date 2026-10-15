param([string]$AppRoot=$PSScriptRoot)
$ErrorActionPreference='Stop'
foreach($script in @('test-core.ps1','test-content.ps1','test-dispatch.ps1')){& (Join-Path $AppRoot $script) -AppRoot $AppRoot;if($LASTEXITCODE-ne 0){throw "Failed: $script"}}
foreach($name in @('ProxyTests','AgeTests','RecoveryTests')){
 & 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /nologo "/r:$AppRoot\build\CiyuanCore.dll" "/out:$AppRoot\build\$name.exe" "$AppRoot\tests\$name.cs"
 if($LASTEXITCODE-ne 0){throw "Compilation failed: $name"}
 Copy-Item -LiteralPath "$AppRoot\app.config" -Destination "$AppRoot\build\$name.exe.config" -Force
 & "$AppRoot\build\$name.exe"
 if($LASTEXITCODE-ne 0){throw "Failed: $name"}
}
Write-Output 'ALL OFFLINE TESTS PASSED'
