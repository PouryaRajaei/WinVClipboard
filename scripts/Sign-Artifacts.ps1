param([Parameter(Mandatory)][string]$Directory)

$ErrorActionPreference = 'Stop'
if (-not $env:WINDOWS_CERTIFICATE) { Write-Host 'Signing certificate is not configured; skipping.'; exit 0 }
$certificatePath = Join-Path $env:RUNNER_TEMP 'WinVClipboard-signing.pfx'
[IO.File]::WriteAllBytes($certificatePath, [Convert]::FromBase64String($env:WINDOWS_CERTIFICATE))
$signtool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Recurse -Filter signtool.exe | Where-Object FullName -Match '\\x64\\' | Sort-Object FullName -Descending | Select-Object -First 1
if (-not $signtool) { throw 'signtool.exe was not found.' }
Get-ChildItem -LiteralPath $Directory -File | Where-Object Extension -In '.exe', '.msix' | ForEach-Object {
    & $signtool.FullName sign /fd SHA256 /f $certificatePath /p $env:WINDOWS_CERTIFICATE_PASSWORD /tr http://timestamp.digicert.com /td SHA256 $_.FullName
    if ($LASTEXITCODE -ne 0) { throw "Signing failed: $($_.FullName)" }
}
Remove-Item -LiteralPath $certificatePath -Force
