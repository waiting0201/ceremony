# Ceremony.Api sidecar 發佈（Windows，framework-dependent .NET 10）
#
# 產出單一 Ceremony.Api.exe（不內包 .NET runtime；client 須裝 .NET 10 ASP.NET Core Runtime，
# 由 Electron prereq 偵測引導安裝）。輸出供 electron-builder 的 extraResources 引用。
#
# 用法：pwsh backend/publish.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$proj = Join-Path $root 'src/Ceremony.Api/Ceremony.Api.csproj'
$out  = Join-Path $root 'publish/win-x64'
$formProj = Join-Path $root 'src/Ceremony.PrintForm/Ceremony.PrintForm.csproj'
$formOut  = Join-Path $root 'publish/win-x64-printform'

Write-Host "Publishing sidecar (framework-dependent .NET 10, win-x64) -> $out"
dotnet publish $proj `
  -c Release `
  -r win-x64 `
  -p:SelfContained=false `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:DebugType=none `
  -p:DebugSymbols=false `
  -o $out

# 移除第三方原生庫的 debug symbols（如 libSkiaSharp.pdb ~80MB），不該進 installer。
Get-ChildItem -Path $out -Filter *.pdb -Recurse | Remove-Item -Force -ErrorAction SilentlyContinue

# 列印前預選驅動自訂表單的小工具（Windows-only，~2MB）。獨立輸出資料夾供 extraResources 對應。
# 只需 Microsoft.NETCore.App 10（已含在既有的 ASP.NET Core Runtime prereq 裡），不新增 prereq。
Write-Host "Publishing print-form helper (win-x64) -> $formOut"
dotnet publish $formProj `
  -c Release `
  -r win-x64 `
  -p:SelfContained=false `
  -p:PublishSingleFile=true `
  -p:DebugType=none `
  -p:DebugSymbols=false `
  -o $formOut

Get-ChildItem -Path $formOut -Filter *.pdb -Recurse | Remove-Item -Force -ErrorAction SilentlyContinue

Write-Host "Done. Ceremony.Api.exe in $out, Ceremony.PrintForm.exe in $formOut"
