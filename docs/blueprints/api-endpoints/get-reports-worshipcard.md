---
title: GET /api/v1/reports/worshipcard
purpose: 產生「普桌資料卡」PDF（A5 橫，template 全印白紙可印；葫蘆內編號＋陽上 6 變體；不限 SignupType）
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
keywords: [普桌資料卡, worshipcard, 列印, 報表, 葫蘆, template 全印, debugOverlay]
last_updated: 2026-08-15 (呼叫端 1 補記前端 enable 條件：報名維護右鍵在「選取列全非普桌」時停用本報表選項〔提示改印資料卡，混選仍可印〕——純前端引導，**本 endpoint 仍無任何 SignupType 檢查**，契約不變；預覽頁下拉不套此規則。見 business-rules-implicit §16.2。先前 2026-08-12 (修正兩處 2026-07-18 解鎖後的殘句〔request 表「signupId 須為 SignupType == 4」、批次段「ids 模式過濾非 type-4、區間模式強制 signupType=4」〕——實作 `BatchReportHandler` 與本檔上方敘述皆為不限型別，殘句與之矛盾。同時補記新入口：新增報名表單的「列印資料卡」鈕在 SignupType=4 時改打本 endpoint〔系統代選，見 business-rules-implicit §16.1〕。先前 2026-07-18 (客訴：template 一樣要全印——DrawTemplate 畫葫蘆/右側標題/簽名底線，白紙可印，座標見 positions §20；同日解鎖：移除 SignupType=4 限制與 WORSHIP_ONLY_TYPE_4 錯誤，對齊舊系統選什麼印什麼))
---

## 規格

### Route + Method

`GET` `/api/v1/reports/worshipcard?signupId=<guid>[&debugOverlay=true]`

### Request

| Query | 型別 | 必填 | 說明 |
|---|---|---|---|
| `signupId` | Guid | ✅ | 報名 ID，**不限 `SignupType`**（2026-07-18 解鎖，見下方；此處原記「須為 SignupType == 4」為解鎖前殘句，2026-08-12 修正） |
| `debugOverlay` | bool | — | dev-only 樣板疊圖（非 Development 環境回 404），見 [printing-reports.md](../printing-reports.md)「開發用列印位置檢視工具」 |

### Response

- `200` `application/pdf`，`Content-Disposition: attachment; filename="worshipcard-{year}-{numberTitle}-{number}.pdf"`

### 錯誤

| HTTP | code | message | 條件 |
|---|---|---|---|
| 404 | `SIGNUP_NOT_FOUND` | 找不到報名 | signupId 查無 |
| 404 | — | — | `debugOverlay=true` 且非 Development 環境 |

> 2026-07-18 解鎖：原 422 `WORSHIP_ONLY_TYPE_4`（`SignupType != 4`）已移除——對齊舊系統「選什麼印什麼」（客訴右鍵選項被鎖／單選非普桌 422）。

## 內容規格（版面）

A5 橫 21×14.8cm，**template 由程式全印、白紙可印**（2026-07-18 客訴改版，取代原「預印卡紙只套印內容」假設）；掃描 jpg 不進生產 PDF，僅 debugOverlay 對位用：

- **template**（`DrawTemplate`）：葫蘆輪廓（重用 `worship2.png` 墨跡對墨跡縮放）＋右側標題「電話：／備註：／確認無誤請簽名」＋簽名底線，座標與回掃校正見 [printing-reports-positions.md](../printing-reports-positions.md) §20 template 元素表
- **葫蘆內**＝普桌牌位（`WorshipRenderer`）縮小版：編號（`WorshipNumber` = NumberTitle＋避4號，Bold 置中）＋陽上姓名直書，依人數套 `PrintTemplateSelector.ChooseWorship` 6 變體（One/Two/Three/Four/Five/Base），座標用墨跡仿射映射（推導見 [printing-reports-positions.md](../printing-reports-positions.md) §20）
- **右側**：電話（`Signup.Phone`）、備註（`Signup.Remark`，過長自動換行不裁字）

實作：[WorshipCardRenderer.cs](../../../backend/src/Ceremony.Infrastructure/Reporting/WorshipCardRenderer.cs)、`GenerateWorshipCardHandler`（GenerateReportHandlers.cs）、`ReportModelBuilders.WorshipCard`。

## 呼叫端

1. **報名維護右鍵選單「列印普桌資料卡」**（單／多選）——**後端不分型別**（本 endpoint 無任何 SignupType 檢查）；
   前端自 2026-08-15 起在「選取列全非普桌」時把該選項停用、提示改印資料卡（兩張卡同尺寸易印錯的前端引導，
   非契約限制；混選仍可印），見 [business-rules-implicit §16.2](../../business-rules-implicit.md)
1b. **列印預覽頁下拉**——使用者明示選擇，不套上述停用規則，任何型別都可挑本報表
2. **新增報名表單存檔後的「列印資料卡」鈕**（2026-08-12）——**系統代選**：剛新增那筆 `SignupType = 4` 時打本 endpoint，其餘打 `GET /api/v1/reports/datacard`（見 [printing-reports.md §1 資料卡](../printing-reports.md)）；按鈕文字同步變「列印普桌資料卡」。判斷用存檔當下的類型快照而非下拉即時值。見 [business-rules-implicit §16.1](../../business-rules-implicit.md)、[signup-management.md](../signup-management.md)

## 批次

`POST /reports/batch` 的 `reportType` 白名單含 `worshipcard`；與 `worship` 完全一致**不做 SignupType 防呆**（ids 模式勾什麼印什麼、區間模式只跟隨呼叫端傳入的 `signupType` 篩選，見 `BatchReportHandler`「普桌不另限 SignupType」註解），見 [post-reports-batch.md](post-reports-batch.md)。（2026-08-12 修正：此處原記「ids 模式過濾非 type-4、區間模式強制 signupType=4」為 2026-07-18 解鎖前的殘句，與實作及本檔上方敘述矛盾。）

## 驗證

- 單元/煙霧測試：`RendererSmokeTests.WorshipCard_*`（6 變體渲染、姓名不被靜默丟字回歸鎖、電話/備註有渲染、6 情境 dump、**空內容仍印 template 回歸鎖**）
- 目視：`CEREMONY_PDF_DUMP=reference/output dotnet test --filter "FullyQualifiedName~WorshipCard"` → `reference/output/worshipcard_*_overlay.pdf`
- 回掃：生產字型渲染 → 200 DPI 點陣化 → 暗像素帶掃描 vs 樣板量測，逐項誤差 ≤0.013cm（2026-07-18）
- 實體：白紙直印 → 使用者確認（**尚待實體驗收**）
