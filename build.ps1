# ClipBind PDF をビルドしてインストーラーを作る
#   powershell -ExecutionPolicy Bypass -File build.ps1
# 出力: dist\ClipBindPDF-Setup-<version>.exe
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

[xml]$proj = Get-Content ClipboardPDF.csproj
$version = $proj.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1

if (Test-Path dist\app) { Remove-Item dist\app -Recurse -Force }
dotnet publish ClipboardPDF.csproj -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=false -p:DebugType=none -o dist\app
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

$iscc = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup 6 が見つかりません: winget install JRSoftware.InnoSetup' }

& $iscc "/DAppVersion=$version" installer\ClipBindPDF.iss
if ($LASTEXITCODE -ne 0) { throw 'ISCC failed' }
Get-ChildItem dist -Filter *.exe
