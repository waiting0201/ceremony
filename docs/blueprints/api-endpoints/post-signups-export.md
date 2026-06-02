---
title: POST /api/v1/signups/export
purpose: 報名查詢結果匯出為 Excel（32 欄）
status: shipped
endpoint: post-signups-export
http_method: POST
route: /api/v1/signups/export
legacy_form: SignupForm.cs
legacy_lines: 655-728
related_agents:
  - backend-engineer
related_docs:
  - get-signups.md
  - ../legacy-coverage/signup-form.md
keywords: [signups, export, excel, closedxml]
last_updated: 2026-05-27
---

## 規格

`POST /api/v1/signups/export`，需要 JWT。

### Request DTO

同 [get-signups.md](get-signups.md) `SignupSearchQuery`（重用，body 而非 query）。

### Response

- HTTP 200
- `Content-Type: application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`
- `Content-Disposition: attachment; filename="signups-{yyyyMMddHHmmss}.xlsx"`
- Binary：xlsx file

### 32 個欄位（對齊舊 line 670-702）

| 欄 | 內容 | 來源 |
|---|---|---|
| 0 | 年 | Year |
| 1 | 法會 | CeremonyTitle |
| 2 | 號別 | NumberTitle (No/寺/觀/普/郵) |
| 3 | 編號 | Number |
| 4 | 金額 | Fee |
| 5 | 員工類別 | Employee (非員工/大殿/地藏殿) |
| 6 | 姓名 | Name |
| 7 | 備註 | Remark |
| 8 | 堂號 | HallName |
| 9-14 | 往生1-6 | DeadName1-6 |
| 15-20 | 陽上1-6 | LivingName1-6 |
| 21 | 預繳年 | PrepayYear |
| 22 | 預繳法會 | PrepayCeremonyTitle |
| 23 | 電話 | Phone |
| 24-26 | 寄件城市/區/地址 | MailCity/Zone/Address |
| 27-29 | 文牒城市/區/地址 | TextCity/Zone/Address |
| 30 | 建立者 | AdminName |
| 31 | 建立時間 | Createdate |

### 錯誤碼

| HTTP | errorCode | message verbatim | 觸發 |
|---|---|---|---|
| 400 | `VALIDATION_REQUIRED` | `請先搜尋，才可匯出 Excel！` | search 條件全空且 total=0（不太可能因 GET /signups 預設 list semantics；本 endpoint 補強 UI 體感） |

> 註：實作上不強制驗 "有結果" — 0 結果也回 Excel（空 sheet）；驗證留前端 UX。

## 舊系統對照

| 舊方法/事件 | 行 | 對應新版 |
|---|---|---|
| `SignupForm.btnExportExcel_Click` | 655-728 | `ExportSignupsHandler` |
| NPOI HSSF (.xls) | 661 | **ClosedXML (.xlsx)**（per [backend-design.md](../../design/backend-design.md) 決策） |
| 取 DataGridView Cells | 670-700 | 直接用 `SignupListItem` 的欄位（不依賴 UI binding） |
| `DateTime.Now.ToString("yyyyMMddHHmmss")` 檔名 | 718 | 同 |
| SaveFileDialog | 723 | API 回 binary stream，前端負責下載對話框 |

## 業務規則

- **重用 SearchSignupsHandler**：先用 search query 取 items，再 build Excel
- **保留 TOP 200 限制**：與 `GET /signups` 一致（防止超大檔；前端可未來加分頁/全量 export）
- **無 header row**（對齊舊系統 — 舊版第 0 row 就是資料；新版維持相同）

## 驗收

- [x] 32 欄位對齊舊 line 670-700
- [x] xlsx (ClosedXML) 取代 xls (NPOI)
- [x] 對應 [signup-form.md](../legacy-coverage/signup-form.md) row 17 ✅
- [x] 含 integration test (200 + 檢查 content-type + 檔名 + 至少 1 row)
