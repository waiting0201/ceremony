# 桌面 Icon 來源

## 用途

新版（Electron 包裝階段）的 Windows 桌面 icon、安裝程式 icon、捷徑 icon **全部沿用舊系統 icon**（客戶 2026-05-26 決策）。

## 預期檔案

| 檔名 | 用途 | 規格 |
|---|---|---|
| `ceremony.ico` | 應用程式 / 安裝檔 / 捷徑 | 多尺寸 ICO（含 16/32/48/256），建議 256×256 為最大 |

## 取得方式

1. 由客戶直接提供舊系統原始 `.ico`，或
2. 從舊版 `Ceremony.exe` 用 ResourceHacker / icoutils (`wrestool -x -t14 Ceremony.exe`) 抽出，或
3. 從舊版 Setup.msi 解壓後找 `ARPPRODUCTICON`（檔名 `_A5AC8D4943C5488AB4F26232A8B52449`）轉成 .ico

## 構建流程引用

- `electron-builder.yml`：`win.icon: build/icon.ico`
- CI step：複製 `reference/icons/ceremony.ico` → `build/icon.ico`
- 開發階段（純 Angular，瀏覽器跑）：不需要 icon，沿用 favicon 即可

## 目前狀態

- [ ] `.ico` 尚未上傳。Electron 打包前必須補齊。
- 對應 task：[status.md](../../docs/status.md) 中「Electron 包裝階段」前置條件
