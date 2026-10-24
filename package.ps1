param([Parameter(Mandatory=$true)][string]$Version)
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^v\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$') { throw 'Use a tag such as v0.1.0 or v0.1.0-beta.1.' }
$dist = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Force $dist | Out-Null
$exe = Join-Path $PSScriptRoot 'build\Z500Battery.exe'
if (!(Test-Path $exe)) { throw 'Build the app before packaging.' }
$zip = Join-Path $dist "lenovo-z500-battery-tool-$Version-windows-x64.zip"
$files = @($exe, (Join-Path $PSScriptRoot 'README.md'), (Join-Path $PSScriptRoot 'LICENSE'), (Join-Path $PSScriptRoot 'docs\COMPATIBILITY.md'))
Compress-Archive -LiteralPath $files -DestinationPath $zip -Force
$checksums = @(
    "$((Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant())  $([IO.Path]::GetFileName($zip))",
    "$((Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToLowerInvariant())  Z500Battery.exe"
)
$checksums | Set-Content -LiteralPath (Join-Path $dist 'SHA256SUMS.txt') -Encoding ascii
Write-Output $zip
