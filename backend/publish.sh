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

echo "Publishing sidecar (framework-dependent .NET 10, win-x64) -> $OUT"
dotnet publish "$PROJ" \
  -c Release \
  -r win-x64 \
  --self-contained false \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true \
  -p:DebugType=none \
  -p:DebugSymbols=false \
  -o "$OUT"

# 移除第三方原生庫的 debug symbols（如 libSkiaSharp.pdb ~80MB），不該進 installer。
find "$OUT" -name '*.pdb' -delete 2>/dev/null || true

echo "Done. Ceremony.Api.exe in $OUT"
