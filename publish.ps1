# Builds the distributable ShareSwitch.exe (and the installer if Inno Setup is installed).
# Usage:  .\publish.ps1 [-Version 0.2.0]
param([string]$Version = "0.2.0")

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

Write-Host "Publishing ShareSwitch $Version (single-file, self-contained win-x64)..." -ForegroundColor Cyan
dotnet publish ShareSwitch.csproj -c Release -o publish -p:Version=$Version
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }
Write-Host "Portable exe: publish\ShareSwitch.exe" -ForegroundColor Green

# Installer (optional — requires Inno Setup 6: winget install JRSoftware.InnoSetup)
$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if ($iscc) {
    Write-Host "Building installer..." -ForegroundColor Cyan
    & $iscc "/DAppVersion=$Version" "installer\ShareSwitch.iss"
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup compile failed" }
    Write-Host "Installer: installer\Output\ShareSwitch-Setup-$Version.exe" -ForegroundColor Green
} else {
    Write-Host "Inno Setup not found - skipped installer. Install with: winget install JRSoftware.InnoSetup" -ForegroundColor Yellow
}
