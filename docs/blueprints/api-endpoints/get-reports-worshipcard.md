---
title: GET /api/v1/reports/worshipcard
purpose: 產生「普桌資料卡」PDF（A5 橫預印卡紙套印；葫蘆內編號＋陽上 6 變體；僅 SignupType=4）
status: shipped
endpoint: get-reports-worshipcard
http_method: GET
route: /api/v1/reports/worshipcard
legacy_form: N/A（全新複合報表，舊系統無對應 RDLC；樣板紙 reference/template/普桌資料卡.jpg 於 2026-07-02 新增）
legacy_lines: N/A
related_agents:
  - backend-engineer
  - qa-test-engineer
related_docs:
  - ../../design/api-design.md
  - ../printing-reports.md
  - ../printing-reports-positions.md
keywords: [普桌資料卡, worshipcard, 列印, 報表, 葫蘆, 預印卡紙, debugOverlay]
last_updated: 2026-07-04
---

## 規格

### Route + Method

`GET` `/api/v1/reports/worshipcard?signupId=<guid>[&debugOverlay=true]`

### Request

| Query | 型別 | 必填 | 說明 |
|---|---|---|---|
| `signupId` | Guid | ✅ | 報名 ID，須為 `SignupType == 4`（普桌） |
| `debugOverlay` | bool | — | dev-only 樣板疊圖（非 Development 環境回 404），見 [printing-reports.md](../printing-reports.md)「開發用列印位置檢視工具」 |

### Response

- `200` `application/pdf`，`Content-Disposition: attachment; filename="worshipcard-{year}-{numberTitle}-{number}.pdf"`

### 錯誤

| HTTP | code | message | 條件 |
|---|---|---|---|
| 404 | `SIGNUP_NOT_FOUND` | 找不到報名 | signupId 查無 |
| 4xx | `WORSHIP_ONLY_TYPE_4` | 普桌資料卡僅限報名類型為普桌 | `SignupType != 4` |
| 404 | — | — | `debugOverlay=true` 且非 Development 環境 |

## 內容規格（版面）

A5 橫 21×14.8cm **預印卡紙**（左葫蘆輪廓＋右側「電話：／備註：／確認無誤請簽名」＋簽名底線皆預印），程式**只套印內容**、生產 PDF 不嵌樣板圖：

- **葫蘆內**＝普桌牌位（`WorshipRenderer`）縮小版：編號（`WorshipNumber` = NumberTitle＋避4號，Bold 置中）＋陽上姓名直書，依人數套 `PrintTemplateSelector.ChooseWorship` 6 變體（One/Two/Three/Four/Five/Base），座標用墨跡仿射映射（推導見 [printing-reports-positions.md](../printing-reports-positions.md) §20）
- **右側**：電話（`Signup.Phone`）、備註（`Signup.Remark`，過長自動換行不裁字）

實作：[WorshipCardRenderer.cs](../../../backend/src/Ceremony.Infrastructure/Reporting/WorshipCardRenderer.cs)、`GenerateWorshipCardHandler`（GenerateReportHandlers.cs）、`ReportModelBuilders.WorshipCard`。

## 批次

`POST /reports/batch` 的 `reportType` 白名單含 `worshipcard`；SignupType=4 防呆與 `worship` 完全一致（ids 模式過濾非 type-4、區間模式強制 `signupType=4`），見 [post-reports-batch.md](post-reports-batch.md)。

## 驗證

- 單元/煙霧測試：`RendererSmokeTests.WorshipCard_*`（6 變體渲染、姓名不被靜默丟字回歸鎖、電話/備註有渲染、6 情境 dump）
- 目視：`CEREMONY_PDF_DUMP=reference/output dotnet test --filter "FullyQualifiedName~WorshipCard"` → `reference/output/worshipcard_*_overlay.pdf`
- 實體套印：印白紙與預印卡對光疊合 → 實體卡紙試印 → 使用者確認（**尚待實體驗收**）
