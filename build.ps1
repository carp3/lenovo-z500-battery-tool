param([switch]$Test)
$ErrorActionPreference='Stop'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path $compiler)) { throw '.NET Framework 4.x compiler is required on 64-bit Windows.' }
$output=Join-Path $PSScriptRoot 'build'
New-Item -ItemType Directory -Force $output | Out-Null
$sources=@(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' -Recurse | ForEach-Object FullName)
$references=@('/r:System.Management.dll','/r:System.Drawing.dll','/r:System.Windows.Forms.dll','/r:System.Windows.Forms.DataVisualization.dll','/r:System.Web.Extensions.dll')
& $compiler /nologo /target:winexe /platform:x64 "/out:$output\Z500Battery.exe" "/win32manifest:$PSScriptRoot\src\app.manifest" @references @sources
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
if ($Test) {
 & $compiler /nologo /target:exe /platform:x64 /define:TESTS "/out:$output\Z500Battery.Tests.exe" @references @sources (Join-Path $PSScriptRoot "tests\BatteryFormChecks.cs")
 if ($LASTEXITCODE -ne 0) { throw 'Test build failed' }
 Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'tests\sample.json') -Destination $output
 & "$output\Z500Battery.Tests.exe" --self-test
 if ($LASTEXITCODE -ne 0) { throw 'Decoder tests failed' }
 & "$output\Z500Battery.Tests.exe" --ui-check
 if ($LASTEXITCODE -ne 0) { throw 'UI checks failed; see build\ui-check-error.txt' }
 Get-Content "$output\self-test.txt","$output\ui-check.txt"
}
Write-Output "Built $output\Z500Battery.exe"
