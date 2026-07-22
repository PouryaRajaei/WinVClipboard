param(
    [ValidateSet('win-x64','win-arm64')][string]$Runtime = 'win-x64',
    [Parameter(Mandatory)][string]$Publisher,
    [string]$IdentityName = 'PouryaRajaei.WinVClipboard',
    [string]$Version = '1.4.0.0',
    [string]$Output = 'artifacts'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$stage = Join-Path $Output "msix-$Runtime"
$publish = Join-Path $stage 'app'
New-Item -ItemType Directory -Path $publish -Force | Out-Null
dotnet publish (Join-Path $projectRoot 'WinVClipboard.csproj') -c Release -r $Runtime --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $publish
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }
$architecture = if ($Runtime -eq 'win-arm64') { 'arm64' } else { 'x64' }
$manifest = Get-Content (Join-Path $projectRoot 'packaging\Package.appxmanifest.template') -Raw
$manifest = $manifest.Replace('__IDENTITY_NAME__', $IdentityName).Replace('__PUBLISHER__', $Publisher).Replace('__VERSION__', $Version).Replace('__ARCHITECTURE__', $architecture)
$manifest | Set-Content (Join-Path $publish 'AppxManifest.xml') -Encoding utf8
Copy-Item (Join-Path $projectRoot 'Assets\WinVClipboard.png') (Join-Path $publish 'WinVClipboard.png') -Force
$makeappx = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Recurse -Filter makeappx.exe | Where-Object FullName -Match '\\x64\\' | Sort-Object FullName -Descending | Select-Object -First 1
if (-not $makeappx) { throw 'makeappx.exe was not found. Install the Windows SDK.' }
New-Item -ItemType Directory -Path $Output -Force | Out-Null
$msix = Join-Path $Output "WinVClipboard-$Runtime.msix"
& $makeappx.FullName pack /d $publish /p $msix /o
if ($LASTEXITCODE -ne 0) { throw 'MSIX packaging failed.' }
Write-Host "Created $msix"
