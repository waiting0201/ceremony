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

Write-Host "Publishing sidecar (framework-dependent .NET 10, win-x64) -> $out"
dotnet publish $proj `
  -c Release `
  -r win-x64 `
  --self-contained false `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:DebugType=none `
  -p:DebugSymbols=false `
  -o $out

# 移除第三方原生庫的 debug symbols（如 libSkiaSharp.pdb ~80MB），不該進 installer。
Get-ChildItem -Path $out -Filter *.pdb -Recurse | Remove-Item -Force -ErrorAction SilentlyContinue

Write-Host "Done. Ceremony.Api.exe in $out"
