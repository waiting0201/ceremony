#!/usr/bin/env bash
# Ceremony.Api sidecar 發佈（framework-dependent .NET 10, win-x64）
#
# 在 macOS / Linux 也能 cross-publish 出 win-x64 的 Ceremony.Api.exe（dotnet 會抓 win runtime pack）。
# 產出單一 exe，不內包 .NET runtime（client 須裝 .NET 10 ASP.NET Core Runtime，由 Electron prereq 引導）。
# 輸出供 electron-builder extraResources 引用。
#
# 用法：bash backend/publish.sh
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJ="$ROOT/src/Ceremony.Api/Ceremony.Api.csproj"
OUT="$ROOT/publish/win-x64"
PRINTFORM_PROJ="$ROOT/src/Ceremony.PrintForm/Ceremony.PrintForm.csproj"
PRINTFORM_OUT="$ROOT/publish/win-x64-printform"

echo "Publishing sidecar (framework-dependent .NET 10, win-x64) -> $OUT"
dotnet publish "$PROJ" \
  -c Release \
  -r win-x64 \
  -p:SelfContained=false \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:DebugType=none \
  -p:DebugSymbols=false \
  -o "$OUT"

# 移除第三方原生庫的 debug symbols（如 libSkiaSharp.pdb ~80MB），不該進 installer。
find "$OUT" -name '*.pdb' -delete 2>/dev/null || true

# 列印 helper（Windows-only，~9MB：exe ~2MB ＋ pdfium.dll ~7MB）。獨立輸出資料夾供 extraResources 對應。
# 只需 Microsoft.NETCore.App 10（已含在既有的 ASP.NET Core Runtime prereq 裡），不新增 prereq。
# ⚠️ PublishSingleFile 只打包 managed 組件，native 的 pdfium.dll 會留在 exe 旁邊——這正是我們要的
#    （DllImport("pdfium") 從 app 目錄解析），所以刻意不加 IncludeNativeLibrariesForSelfExtract：
#    那會在每次列印的首次啟動做一次自解壓到 temp，多幾百 ms 又可能被防毒擋。
echo "Publishing print-form helper (win-x64) -> $PRINTFORM_OUT"
dotnet publish "$PRINTFORM_PROJ" \
  -c Release \
  -r win-x64 \
  -p:SelfContained=false \
  -p:PublishSingleFile=true \
  -p:DebugType=none \
  -p:DebugSymbols=false \
  -o "$PRINTFORM_OUT"

find "$PRINTFORM_OUT" -name '*.pdb' -delete 2>/dev/null || true

echo "Done. Ceremony.Api.exe in $OUT, Ceremony.PrintForm.exe in $PRINTFORM_OUT"
