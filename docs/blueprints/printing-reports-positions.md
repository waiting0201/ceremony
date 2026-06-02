---
title: RDLC 位置限制參考（19 個模板逐項座標）
purpose: 19 個 RDLC 報表的精確位置（cm）— 新版列印實作必須**嚴格 1:1 還原**這些座標，否則套印預印格式紙會錯位
status: CRITICAL — 列印實作的 single source of truth；任何偏差需 PR review 通過
applicable_when: 實作列印模板、驗證版面、調整 QuestPDF 座標、排查列印錯位問題、PR 涉及列印任何欄位
related_agents:
  - backend-engineer
  - visual-design-architect
  - qa-test-engineer
related_docs:
  - printing-reports.md
  - ../design/visual-design.md
  - ../workflows/qa-testing.md
keywords: [rdlc, 位置, position, layout, 座標, 列印, 套印, tablet, worship, datacard, receipt, text, 嚴格, strict, 1:1]
last_updated: 2026-06-02
---

## 📌 適用範圍（2026-05-27 補充）

本規範**雙路徑都適用**：
- **(A) RDLC 直接重用**（若 [printing-reports PoC](printing-reports.md) 通過）→ 對應舊 19 個 .rdlc XML，本檔的座標 / 字級 / 變體選擇邏輯是驗收 ground truth
- **(B) QuestPDF 重畫**（若 PoC 不通過 → fallback）→ 對應新 C# 流式 API 實作，本檔規格逐欄硬寫

兩條路徑的座標、字級、變體選擇必須**完全一致**。本檔不需依路徑改寫。

## ⚠️ 嚴格執行條款（CRITICAL）

**本檔為新版列印實作的唯一規格來源（single source of truth）**。違反以下任一條都會導致預印格式紙錯位、客戶投訴：

1. **零容忍偏差**：所有 `Top / Left / Width / Height` 在新版 PDF 必須與本檔列出的 cm 值一致，**最大容忍誤差 ±0.05cm**（不是 ±0.2cm；那只是「不算嚴重錯誤」的下限）
2. **不得改動座標**：即使視覺上「看起來更整齊」也不能微調。所有座標都是配合**實體預印紙的紋飾框/底圖**設定的，PDF 上看起來不齊不代表印在實體紙上不齊
3. **不得改字型**：必須用標楷體（`BiauKai / DFKai-SB / TW-Kai`）。微軟正黑體、宋體、新細明體一律不可
4. **不得改字色**：全 19 份 RDLC 都是 `#000000`。不可改成深灰或任何其他色
5. **不得自創新版面**：若實際需求需要新模板（例如 7 位陽上），**必須**先 PR 增訂本檔的新章節，才能實作對應 QuestPDF
6. **變體選擇邏輯**（見「模板選擇邏輯」章節）**完全照舊 code**：不可「優化」、不可「合併相似變體」
7. **驗收前必跑「對位驗收 checklist」**（見文末）：實體列印 + 與舊系統並排照相 + 量測誤差

> **歷史 incident**：v1.x 曾因把牌位字級從 0.8cm 改成 0.75cm「看起來更精緻」，結果 200 張牌位印在紋飾紙上字壓到底框，全部報廢重印。本檔每一個數值都有實體紙張對位的歷史原因，**不要動**。

## 為什麼位置很重要

法會列印的 5 種報表中，**牌位 / 普桌 / 文牒**會印在**預印格式紙**上（紙廠提供的紋飾紙、含框線/底圖），欄位位置偏差超過 ±0.2cm 就會印出框線、覆蓋紋飾、超出可印區。新系統 QuestPDF 必須照舊 RDLC 的座標 1:1 還原。

**為什麼資料卡 / 收據也要嚴格**：雖然印在白紙不會「卡到框線」，但承辦人員已習慣固定欄位位置做雙重檢核（眼睛掃過去 + 紙本歸檔位置），改了會降低稽核效率與引發人員抗拒。

座標系：左上角為 (0, 0)，往右 = Left+，往下 = Top+，單位 cm。

## 通用規格

| 屬性 | 值 | 來源 RDLC 屬性 |
|---|---|---|
| 字型 | **標楷體（BiauKai / DFKai-SB）** — 19 個 RDLC 100% 一致 | `<FontFamily>標楷體</FontFamily>` |
| 字色 | **黑色（#000000）** — 全部報表單一色 | `<Color>Black</Color>` |
| 邊界 | Top/Bottom/Left/Right = 0cm（**滿版列印**）— 除 tmpTabletOneOne 上下各 2cm | `<TopMargin>` etc. |
| 座標單位 | cm | XML 顯式標 `Xcm` |
| 字級單位 | cm（主流） / pt（Receipt 用 14pt、16pt） | `<FontSize>` |
| 字級範圍 | 0.6cm ~ 3cm（≈ 17pt ~ 85pt） | — |
| Bold | 僅用於 Number 欄位 + Receipt 編號 | `<FontWeight>Bold</FontWeight>` |
| Italic / Underline | **全無** | — |
| TextAlign 預設 | Left；Number 多為 Center；DataCard 簽名欄 Right | `<TextAlign>` |
| VerticalAlign 預設 | Top；**HallNameFirst/Second 為 Middle**（所有 Tablet/Text 變體）；DataCard 「確認無誤請簽名」為 Bottom | `<VerticalAlign>` |
| Padding | Receipt / Text 系列 2pt 全側；Tablet / Worship 多數無 padding | `<PaddingTop>2pt</PaddingTop>` |
| Border | 99% None；DataCard 含 Solid 與 Dashed 兩條 Line | `<Border>` |
| ConsumeContainerWhitespace | true（多數） | `<ConsumeContainerWhitespace>true</...>` |

> ⚠️ **跨平台字型**：QuestPDF 在 macOS/Linux 需要 bundle 標楷體字型檔。建議用 [TW-Kai 開源台灣標準楷書](https://github.com/g0v/twkai) 或購買 DynaFont DFKai-SB；fallback 鏈：`['BiauKai', 'DFKai-SB', 'TW-Kai', 'STKaiti', '標楷體', serif]`

## 紙張尺寸總覽（必查表）

實作 QuestPDF 前先用此表確認 page size，避免設成錯誤大小。**所有 9 個牌位變體都是 11.5 × 25.4cm 直式**（doc v1 曾誤標部分為橫向超寬版，已更正）：

| RDLC | Width (cm) | Height (cm) | 方向 | 備註 |
|---|---|---|---|---|
| tmpDataCard | 21 | 14.8 | A5 橫 | |
| tmpReceipt | 21 | 29.7 | A4 直 | Tablix 內容跨 59.4cm（雙聯 + 標籤） |
| **tmpTablet** | **11.5** | **25.4** | **直** | 牌位窄長 |
| **tmpTabletOne** | **11.5** | **25.4** | **直** | 同上（無 PhotoAddress） |
| **tmpTabletOneOne** | **11.5** | **25.4** | **直** | 同上 + Margin Top/Bottom 2cm |
| **tmpTabletOneTwo** | **11.5** | **25.4** | **直** | 同上 |
| **tmpTabletTwo** | **11.5** | **25.4** | **直** | 同上 |
| **tmpTabletTwoOne** | **11.5** | **25.4** | **直** | 同上 |
| **tmpTabletTwoTwo** | **11.5** | **25.4** | **直** | 同上 |
| **tmpTablet_One** | **11.5** | **25.4** | **直** | 同上（注意：底線版本，仍是窄長） |
| **tmpTablet_Two** | **11.5** | **25.4** | **直** | 同上 |
| tmpText | 36.5 | 26.2 | 橫向超寬 | 唯一橫向；含 PhotoAddress |
| tmpTextTwo | 36.5 | 26.2 | 橫向超寬 | 含 PhotoAddress |
| tmpWorship | 21 | 29.6 | A4 直 | 含 worship2 背景；6 位 LivingName 矩陣 |
| tmpWorshipOne | 21 | 29.6 | A4 直 | 1 位 LivingName |
| tmpWorshipTwo | 21 | 29.6 | A4 直 | 2 位 |
| tmpWorshipThree | 21 | 29.6 | A4 直 | 3 位 |
| tmpWorshipFour | 21 | 29.6 | A4 直 | 4 位 |
| tmpWorshipFive | 21 | 29.6 | A4 直 | 5 位 |

**結論：除 tmpText/tmpTextTwo 為橫向超寬（36.5×26.2cm）外，所有牌位變體都是 11.5×25.4cm 直式。**

## 1. tmpDataCard.rdlc（資料卡）

**頁面**：21cm × 14.8cm（A5 橫式）

| Name | 綁定 | Top | Left | Width | Height | FontSize |
|---|---|---|---|---|---|---|
| txtNumber | =Fields!Number.Value | 0.538 | 1.361 | 6.204 | 1.129 | 1.0cm |
| HallName | =Fields!HallName.Value | 0.776 | 7.961 | 3.241 | 0.891 | 0.6cm |
| txtPrepay | =Fields!Prepay.Value | 0.776 | 12.133 | 7.629 | 0.891 | 0.7cm |
| txtTitleDeadName | "亡者：" | 1.897 | 1.714 | 2.438 | 0.918 | 0.8cm |
| DeadNameOne | =Fields!DeadNameOne.Value | 1.897 | 4.328 | 7.236 | 0.918 | 0.8cm |
| DeadNameTwo | =Fields!DeadNameTwo.Value | 1.897 | 11.763 | 7.259 | 0.918 | 0.8cm |
| Textbox3 (DeadNameThree) | =Fields!DeadNameThree.Value | 2.814 | 4.328 | 7.236 | 0.918 | 0.8cm |
| Textbox4 (DeadNameFour) | =Fields!DeadNameFour.Value | 2.814 | 11.763 | 3.596 | 0.918 | 0.8cm |
| Textbox5 (DeadNameFive) | =Fields!DeadNameFive.Value | 2.814 | 15.535 | 3.638 | 0.918 | 0.8cm |
| Line2 | 虛線分隔 | 4.190 | 4.328 | 15.434 | 0 | Dashed |
| txtTitleLivingName | "陽上：" | 4.707 | 1.714 | 2.438 | 0.918 | 0.8cm |
| Textbox6 (LivingNameOne) | =Fields!LivingNameOne.Value | 4.707 | 4.328 | 7.236 | 0.918 | 0.8cm |
| Textbox7 (LivingNameTwo) | =Fields!LivingNameTwo.Value | 4.707 | 11.763 | 7.259 | 0.918 | 0.8cm |
| Textbox8 (LivingNameThree) | =Fields!LivingNameThree.Value | 5.660 | 4.328 | 7.236 | 0.918 | 0.8cm |
| Textbox9 (LivingNameFour) | =Fields!LivingNameFour.Value | 5.730 | 11.763 | 3.596 | 0.918 | 0.8cm |
| Textbox10 (LivingNameFive) | =Fields!LivingNameFive.Value | 5.730 | 15.535 | 3.638 | 0.918 | 0.8cm |
| txtTitleAddress | "地址：" | 6.753 | 1.714 | 2.438 | 0.918 | 0.8cm |
| txtAddress | =Fields!Address.Value | 6.753 | 4.328 | 15.434 | 1.870 | 0.8cm |
| txtTitlePhone | "電話：" | 8.799 | 1.714 | 2.438 | 0.626 | 0.6cm |
| txtPhone | =Fields!Phone.Value | 8.799 | 4.328 | 15.434 | 0.626 | 0.6cm |
| txtTitleRemark | "備註：" | 9.602 | 1.714 | 2.438 | 0.918 | 0.6cm |
| txtRemark | =Fields!Remark.Value | 9.602 | 4.328 | 15.434 | 3.421 | 0.6cm |
| Textbox13 | "確認無誤請簽名：" | 13.182 | 9.590 | 6.548 | 0.749 | 0.8cm |
| Line1 | 簽名底線 (實線) | 13.931 | 16.125 | 3.638 | 0 | Solid |

## 2. tmpReceipt.rdlc（收據，雙聯）

**頁面**：21cm × 29.7cm（A4），Tablix 高 59.4cm（兩聯堆疊）

雙聯收據：上聯 (Top 0~29.7) 為**收據聯**、下聯 (Top 29.7~59.4) 為**存根聯**，欄位 Top 值相差約 9.8~10cm。

### 上聯（顧客）

| Name | 綁定 | Top | Left | Width | Height | FontSize |
|---|---|---|---|---|---|---|
| Textbox16 | =Fields!Name.Value | 2.30 | 6.73 | 8.257 | 0.726 | 0.6cm |
| Textbox17 | =Fields!Fee.Value | 3.50 | 5.00 | 2.50 | 0.653 | 14pt |
| Textbox18 | =Fields!Number.Value | 3.50 | 15.00 | 2.50 | 0.653 | 14pt Bold |
| Prepay | =Fields!Prepay.Value | 4.70 | 11.50 | 6.00 | 0.653 | 14pt |
| Year | =Fields!Year.Value | 7.60 | 8.00 | 2.50 | 0.653 | 14pt |
| Month | =Fields!Month.Value | 7.60 | 11.50 | 2.50 | 0.653 | 14pt |
| Day | =Fields!Day.Value | 7.60 | 15.00 | 2.50 | 0.653 | 14pt |

### 下聯（存根）

| Name | 綁定 | Top | Left | Width | Height | FontSize |
|---|---|---|---|---|---|---|
| Textbox19 | =Fields!Name.Value | 12.10 | 6.73 | 8.257 | 0.753 | 0.6cm |
| Textbox20 | =Fields!Fee.Value | 13.60 | 5.00 | 2.50 | 0.653 | 14pt |
| Textbox21 | =Fields!Number.Value | 13.60 | 15.00 | 2.50 | 0.653 | 14pt Bold |
| Prepay1 | =Fields!Prepay.Value | 14.50 | 11.50 | 6.00 | 0.653 | 14pt |
| Year1 | =Fields!Year.Value | 17.50 | 8.00 | 2.50 | 0.653 | 14pt |
| Month1 | =Fields!Month.Value | 17.50 | 11.50 | 2.50 | 0.653 | 14pt |
| Day1 | =Fields!Day.Value | 17.50 | 15.00 | 2.50 | 0.653 | 14pt |

### 郵寄標籤區（第二頁/續頁）

| Name | 綁定 | Top | Left | Width | Height | FontSize |
|---|---|---|---|---|---|---|
| Textbox22 | =Fields!Zipcode.Value | 33.60 | 4.756 | 2.50 | 0.70 | 16pt |
| Textbox23 | =Fields!Address.Value | 34.371 | 4.756 | 10.676 | 0.70 | 16pt |
| Textbox24 | =Fields!Name.Value | 35.141 | 4.756 | 9.244 | 0.70 | 16pt |

## 3. tmpTablet.rdlc（薦牌 — 基本版）

**頁面**：11.5cm × 25.4cm（窄長牌位）；**ReportParameter**：`ParaFontSize` 預設 0.8cm（動態調整 DeadName 字級）

| Name | 綁定 | Top | Left | Width | Height | FontSize |
|---|---|---|---|---|---|---|
| HallNameSecond | =Fields!HallNameSecond.Value | 6.10 | 3.90 | 0.70 | 1.383 | 0.6cm |
| HallNameFirst | =Fields!HallNameFirst.Value | 6.10 | 5.90 | 0.70 | 1.383 | 0.6cm |
| Number | =Fields!Number.Value | (置中) | (置中) | 4.296 | 1.132 | 0.8cm Bold |
| DeadNameOne | =Fields!DeadNameOne.Value | 7.583 | 4.80 | 0.80 | 6.466 | ParaFontSize |
| LivingNameOne | =Fields!LivingNameOne.Value | 14.004 | 1.562 | 0.70 | 5.50 | 0.6cm |
| LivingNameTwo | =Fields!LivingNameTwo.Value | 14.004 | 0.835 | 0.70 | 1.261 | 0.6cm |
| LivingNameThree | =Fields!LivingNameThree.Value | 14.00 | 0.10 | 0.70 | 1.261 | 0.6cm |
| LivingNameFour | =Fields!LivingNameFour.Value | 15.442 | 0.835 | 0.70 | 1.261 | 0.6cm |
| LivingNameFive | =Fields!LivingNameFive.Value | 15.442 | 0.10 | 0.70 | 1.261 | 0.6cm |

## 4. tmpTabletOne.rdlc（薦牌 — 1 亡者 + 3-6 陽上）

**頁面**：11.5cm × 25.4cm（窄長牌位 — **與 v1 doc 記錄的「36.5×26.2 橫向」不符，已更正**）；**不含 PhotoAddress**（那是 tmpText/tmpTextTwo 才有）；**ReportParameter**：`ParaFontSize` 預設 0.8cm（依 DeadNameOne 長度動態調整為 0.6cm，見變體選擇邏輯）

| Name | 綁定 | Top | Left | Width | Height | FontSize |
|---|---|---|---|---|---|---|
| HallNameSecond | =Fields!HallNameSecond.Value | 6.10 | 3.90 | 0.70 | 1.383 | 0.6cm (VAlign=Middle) |
| HallNameFirst | =Fields!HallNameFirst.Value | 6.10 | 5.90 | 0.70 | 1.383 | 0.6cm (VAlign=Middle) |
| Number | =Fields!Number.Value | (置中) | (置中) | 4.296 | 1.132 | 0.8cm Bold Center |
| DeadNameOne | =Fields!DeadNameOne.Value | 7.583 | 4.80 | 0.80 | 6.466 | ParaFontSize |
| LivingNameOne~Five | =Fields!LivingNameN.Value | 14.004 / 15.442 | 0.10–1.562 | 0.70 | 1.261–5.50 | 0.6cm |

> 與 tmpTablet 主要差異：陽上欄位排版採 5 格直書（容納 3-6 位陽上）。實際使用情境：DeadName 只有 1 位、LivingName 為 3 位以上（含 6）時用此模板。

## 5. tmpTabletOneOne.rdlc（薦牌 1-1 — 邊距 2cm 簡化版）

**頁面**：11.5cm × 25.4cm；**Margins：Top=2cm, Bottom=2cm**（其他模板皆 0）；**ReportParameter**：`ParaFontSize` 預設 0.8cm

| Name | 綁定 | Top | Left | Width | Height | FontSize |
|---|---|---|---|---|---|---|
| HallNameSecond | =Fields!HallNameSecond.Value | 6.10 | 3.90 | 0.70 | 1.383 | 0.6cm |
| HallNameFirst | =Fields!HallNameFirst.Value | 6.10 | 5.90 | 0.70 | 1.383 | 0.6cm |
| Number | =Fields!Number.Value | (置中) | (置中) | 4.296 | 1.132 | 0.8cm Bold |
| DeadNameOne | =Fields!DeadNameOne.Value | 7.583 | 4.80 | 0.80 | 6.466 | ParaFontSize |
| LivingNameOne | =Fields!LivingNameOne.Value | 14.004 | 0.835 | 0.80 | 5.50 | 0.8cm |

> 與 tmpTablet 差異：移除 LivingNameTwo~Five；Top/Bottom margin 各 2cm。

## 6. tmpTabletOneTwo.rdlc（薦牌 1-2 — 雙亡者）

**頁面**：11.5cm × 25.4cm

| Name | 綁定 | Top | Left | Width | Height | FontSize |
|---|---|---|---|---|---|---|
| HallNameSecond | =Fields!HallNameSecond.Value | 6.10 | 3.90 | 0.70 | 1.383 | 0.6cm |
| HallNameFirst | =Fields!HallNameFirst.Value | 6.10 | 5.90 | 0.70 | 1.383 | 0.6cm |
| Number | =Fields!Number.Value | (置中) | (置中) | 4.296 | 1.132 | 0.8cm Bold |
| Textbox27 (DeadNameOne) | =Fields!DeadNameOne.Value | 7.583 | 5.30 | 0.80 | 6.31 | ParaFontSize |
| Textbox28 (DeadNameTwo) | =Fields!DeadNameTwo.Value | 7.583 | 4.20 | 0.80 | 6.31 | ParaFontSize |
| LivingNameOne | =Fields!LivingNameOne.Value | 14.004 | 1.526 | 0.70 | 5.50 | 0.6cm |
| LivingNameTwo | =Fields!LivingNameTwo.Value | 14.004 | 0.80 | 0.70 | 1.261 | 0.6cm |
| LivingNameThree | =Fields!LivingNameThree.Value | 14.00 | 0.10 | 0.70 | 1.261 | 0.6cm |
| LivingNameFour | =Fields!LivingNameFour.Value | 15.442 | 0.80 | 0.70 | 1.261 | 0.6cm |
| LivingNameFive | =Fields!LivingNameFive.Value | 15.442 | 0.10 | 0.70 | 1.261 | 0.6cm |

> 與 tmpTablet 差異：增加 DeadNameTwo（Left=4.2cm，與 DeadNameOne 錯開 1.1cm）

## 7. tmpTabletTwo.rdlc（薦牌 2 — 雙亡者單陽上）

**頁面**：11.5cm × 25.4cm

| Name | 綁定 | Top | Left | Width | Height | FontSize |
|---|---|---|---|---|---|---|
| HallNameSecond | =Fields!HallNameSecond.Value | 6.10 | 3.90 | 0.70 | 1.383 | 0.6cm |
| HallNameFirst | =Fields!HallNameFirst.Value | 6.10 | 5.90 | 0.70 | 1.383 | 0.6cm |
| Number | =Fields!Number.Value | (置中) | (置中) | 4.296 | 1.132 | 0.8cm Bold |
| Textbox27 (DeadNameOne) | =Fields!DeadNameOne.Value | 7.583 | 5.30 | 0.80 | 6.31 | ParaFontSize |
| Textbox28 (DeadNameTwo) | =Fields!DeadNameTwo.Value | 7.583 | 4.20 | 0.80 | 6.31 | ParaFontSize |
| LivingNameOne | =Fields!LivingNameOne.Value | 14.004 | 0.835 | 0.80 | 5.50 | 0.8cm |

> 與 OneTwo 差異：陽上僅一位

## 8. tmpTabletTwoOne.rdlc（薦牌 2-1 — 多亡者單陽上，巢狀 Rectangle）

**頁面**：11.5cm × 25.4cm

外層 TextBox：
| Name | 綁定 | Top | Left | Width | Height | FontSize |
|---|---|---|---|---|---|---|
| HallNameSecond | =Fields!HallNameSecond.Value | 6.10 | 3.90 | 0.70 | 1.383 | 0.6cm |
| HallNameFirst | =Fields!HallNameFirst.Value | 6.10 | 5.90 | 0.70 | 1.383 | 0.6cm |
| Number | =Fields!Number.Value | (置中) | (置中) | 4.296 | 1.132 | 0.8cm Bold |
| LivingNameOne | =Fields!LivingNameOne.Value | 14.004 | 0.835 | 0.80 | 5.50 | 0.8cm |

**Rectangle2**（Top=7.5825, Left=3.9, Width=2.7, Height=11.469）內含 5 位亡者：

| Name | 綁定 | Top（相對 Rect） | Left | Width | Height | FontSize |
|---|---|---|---|---|---|---|
| DeadNameOne | =Fields!DeadNameOne.Value | 0 | 1.00 | 0.60 | 11.033 | ParaFontSize |
| Textbox27 (DeadNameTwo) | =Fields!DeadNameTwo.Value | 0 | 1.90 | 0.60 | 1.588 | ParaFontSize |
| Textbox28 (DeadNameThree) | =Fields!DeadNameThree.Value | 0 | 0.10 | 0.60 | 1.588 | ParaFontSize |
| Textbox30 (DeadNameFour) | =Fields!DeadNameFour.Value | 1.864 | 1.90 | 0.60 | 5.530 | ParaFontSize |
| Textbox31 (DeadNameFive) | =Fields!DeadNameFive.Value | 1.864 | 0.10 | 0.60 | 5.530 | ParaFontSize |

## 9. tmpTabletTwoTwo.rdlc（薦牌 2-2 — 多亡者雙陽上）

**頁面**：11.5cm × 25.4cm

| Name | 綁定 | Top | Left | Width | Height | FontSize |
|---|---|---|---|---|---|---|
| HallNameSecond | =Fields!HallNameSecond.Value | 6.10 | 3.90 | 0.70 | 1.383 | 0.6cm |
| HallNameFirst | =Fields!HallNameFirst.Value | 6.10 | 5.90 | 0.70 | 1.383 | 0.6cm |
| Number | =Fields!Number.Value | (置中) | (置中) | 4.296 | 1.132 | 0.8cm Bold |
| LivingNameOne | =Fields!LivingNameOne.Value | 14.004 | 1.283 | 0.80 | 5.50 | 0.8cm |
| LivingNameTwo | =Fields!LivingNameTwo.Value | 14.004 | 0.306 | 0.80 | 5.50 | 0.8cm |

**Rectangle2 內亡者結構**同 TwoOne。

## 10. tmpTablet_One.rdlc（薦牌_1 — 3+ 亡者 + 1 陽上）

**頁面**：11.5cm × 25.4cm（窄長牌位 — **v1 doc 誤標為 36.5×26.2，已更正**）；**ReportParameter**：`ParaFontSize` 預設 0.6cm（注意：與其他 Tablet 變體 0.8cm 不同）

| Name | 綁定 | Top | Left | Width | Height | FontSize |
|---|---|---|---|---|---|---|
| HallNameSecond | =Fields!HallNameSecond.Value | 6.10 | 3.90 | 0.70 | 1.383 | 0.6cm (VAlign=Middle) |
| HallNameFirst | =Fields!HallNameFirst.Value | 6.10 | 5.90 | 0.70 | 1.383 | 0.6cm (VAlign=Middle) |
| Number | =Fields!Number.Value | (置中) | (置中) | 4.296 | 1.132 | 0.8cm Bold Center |
| LivingNameOne | =Fields!LivingNameOne.Value | 14.004 | 0.835 | 0.80 | 5.50 | 0.8cm |

**Rectangle2**（多亡者群組）內含 DeadNameOne~Five，巢狀結構同 [§ 8 tmpTabletTwoOne](#8-tmptablettwoonerdlc薦牌-2-1--多亡者單陽上巢狀-rectangle) 的 Rectangle，差異在 `ParaFontSize` 預設 0.6cm。

> 使用情境：DeadName 有 3 位以上 + LivingName 只有 1 位。code 觸發條件見「模板選擇邏輯」§ Tablet 系列 1[c]i 分支。

## 11. tmpTablet_Two.rdlc（薦牌_2 — 3+ 亡者 + 2 陽上）

**頁面**：11.5cm × 25.4cm（**v1 doc 誤標已更正**）；**ReportParameter**：`ParaFontSize` 預設 0.6cm

| Name | 綁定 | Top | Left | Width | Height | FontSize |
|---|---|---|---|---|---|---|
| HallNameSecond | =Fields!HallNameSecond.Value | 6.10 | 3.90 | 0.70 | 1.383 | 0.6cm (VAlign=Middle) |
| HallNameFirst | =Fields!HallNameFirst.Value | 6.10 | 5.90 | 0.70 | 1.383 | 0.6cm (VAlign=Middle) |
| Number | =Fields!Number.Value | (置中) | (置中) | 4.296 | 1.132 | 0.8cm Bold Center |
| LivingNameOne | =Fields!LivingNameOne.Value | 14.004 | 1.283 | 0.80 | 5.50 | 0.8cm |
| LivingNameTwo | =Fields!LivingNameTwo.Value | 14.004 | 0.306 | 0.80 | 5.50 | 0.8cm |

**Rectangle2**（多亡者群組）內含 DeadNameOne~Five，巢狀結構同 § 8。

> 使用情境：DeadName 有 3 位以上 + LivingName 只有 2 位（無 Three/Four/Five/Six）。實作時若 Rectangle 內 5 個 DeadName 都實際對位有 ±0.05cm 差異，需以舊系統實機列印作對照樣本。

## 12. tmpText.rdlc（文牒 — 含垂直地址圖）

**頁面**：36.5cm × 26.2cm

| Name | 綁定 | Top | Left | Width | Height | FontSize |
|---|---|---|---|---|---|---|
| Number | =Fields!Number.Value | 3.80 | 31.497 | 4.749 | 1.103 | 1cm Bold |
| HallNameSecond | =Fields!HallNameSecond.Value | 2.10 | 11.50 | 0.70 | 1.383 | 0.6cm |
| HallNameFirst | =Fields!HallNameFirst.Value | 2.10 | 13.538 | 0.70 | 1.383 | 0.6cm |
| LivingNameOne | =Fields!LivingNameOne.Value | 15.275 | 21.874 | 0.913 | 6.728 | 0.8cm |
| LivingNameTwo | =Fields!LivingNameTwo.Value | 15.275 | 20.961 | 0.913 | 1.583 | 0.8cm |
| LivingNameThree | =Fields!LivingNameThree.Value | 15.275 | 20.049 | 0.913 | 1.583 | 0.8cm |
| LivingNameFour | =Fields!LivingNameFour.Value | 17.259 | 20.961 | 0.913 | 1.583 | 0.8cm |
| LivingNameFive | =Fields!LivingNameFive.Value | 17.259 | 20.049 | 0.913 | 1.583 | 0.8cm |
| **PhotoAddress (Image)** | =Fields!PhotoAddress.Value | 4.10 | 25.40 | 0.66 | 16.80 | FitProportional |

> **陽上 (LivingName) 矩陣**：列距 = 17.25916 − 15.2748 = **1.98436cm**。`LivingNameOne` 為整欄高主欄（H 6.72806cm，Left 21.874）；`Two`/`Three` 上排（Top 15.275）、`Four`/`Five` 下排（Top 17.259），對應陣列 `lv[0..4]`。

### 文牒往生者 DeadName（Rectangle2，原點 Top 3.65889 / Left 11.50）

座標 = Rectangle2 原點 + RDLC 各 Textbox 相對位移；FontSize **0.8cm**；次要格 `CanGrow=true`。
**5 格矩陣：主欄整欄高 + 上排(Two/Three) → 下排(Four/Five)，列距 2.06375cm。**

| Slot | 陣列 idx | Top(cm) | Left(cm) | Width | 名目 Height | 角色 |
|---|---|---|---|---|---|---|
| DeadNameOne | d[0] | 3.65889 | 12.41251 | 0.91251 | 10.50374 | **主欄（整欄高）** |
| DeadNameTwo | d[1] | 3.65889 | 13.32502 | 0.91251 | 1.5825 | 上排右 |
| DeadNameThree | d[2] | 3.65889 | 11.50000 | 0.91251 | 1.5825 | 上排左 |
| DeadNameFour | d[3] | 5.72264 | 13.32502 | 0.91251 | 1.5825 | 下排右 |
| DeadNameFive | d[4] | 5.72264 | 11.50000 | 0.91251 | 1.5825 | 下排左 |

- **列距（上排→下排）= 5.72264 − 3.65889 = 2.06375cm**（= RDLC Four/Five 的 Top offset）。
- 次要格 RDLC 名目高僅 1.5825cm 但 `CanGrow=true`；直書可用高應取「到下一格的列距 2.06375cm」，且**只有當正下方格有名字時**才受此界限（對齊 `VerticalText.Avail`）。主欄一律整欄高 10.50374cm。
- 實作對應 [TextRenderer.cs](../../backend/src/Ceremony.Infrastructure/Reporting/TextRenderer.cs) `DrawDeadNames`（`TextTemplate.Base` 分支）與 Render 內陽上 `lv[]` 區塊。

> **PhotoAddress** 是 Library.DrawText 預產的 25×605px PNG（垂直地址），嵌入 **0.66×16.8cm** 窄帶。新版若改用 QuestPDF 直接畫垂直文字，須保留此區域。

## 13. tmpTextTwo.rdlc（文牒 2）

**頁面**：36.5cm × 26.2cm；陽上 (LivingName)、HallName、Number、PhotoAddress 座標與 § 12 tmpText **一致**（LivingName 同 § 12）。
**差異僅在往生 DeadName**：恰 2 亡，皆整欄高並排（無上下排矩陣）。

### 文牒 2 往生者 DeadName（Rectangle2，原點 Top 3.62361 / Left 11.50）

座標 = Rectangle2 原點 + RDLC 相對位移；FontSize **0.8cm**。

| Slot | 陣列 idx | Top(cm) | Left(cm) | Width | Height | 角色 |
|---|---|---|---|---|---|---|
| DeadNameOne | d[0] | 3.65889 | 13.01299 | 0.91251 | 10.50374 | 高欄（右） |
| DeadNameTwo | d[1] | 3.62361 | 11.85000 | 0.91251 | 10.50374 | 高欄（左） |

- 使用情境：**恰 2 位往生**（DeadNameTwo 有值且 Three~Six 皆空）— 對齊舊 `PrintText` 換 `tmpTextTwo.rdlc` 條件（[SignupForm.cs:1350](../../reference/old/Ceremony/SignupForm.cs)）。
- 實作對應 [TextRenderer.cs](../../backend/src/Ceremony.Infrastructure/Reporting/TextRenderer.cs) `DrawDeadNames`（`TextTemplate.Two` 分支）。

## 14. tmpWorship.rdlc（普桌 — 6+ 陽上 / fallback 版）

**頁面**：21cm × 29.6cm（A4）

**Background Image**（必須對齊背景紋飾；座標精確到小數第 5 位）：

| 屬性 | 值 |
|---|---|
| Name | Image1 |
| Source | Embedded（`worship2`） |
| Top | 0.26141cm |
| Left | 0.42cm |
| Width | 20.04729cm |
| Height | 28.88438cm |
| Sizing | FitProportional |

**TextBox**（**6 個 LivingName 排成 2 列 × 3 欄矩陣** — v1 doc 只列出 3 個，已補齊 Textbox11/12/13）：

| Name | 綁定 | Top | Left | Width | Height | FontSize |
|---|---|---|---|---|---|---|
| Number | =Fields!Number.Value | 4.47354 | 5.5875 | 8.90292 | 2.20583 | 2cm Bold Center |
| LivingNameOne | =Fields!LivingNameOne.Value | 7.31167 | 11.0925 | 2.2 | 10.21125 | 2cm |
| Textbox9 (LivingNameTwo) | =Fields!LivingNameTwo.Value | 7.31167 | 8.82834 | 2.2 | 10.21125 | 2cm |
| Textbox13 (LivingNameThree) | =Fields!LivingNameThree.Value | 7.31167 | 6.62834 | 2.2 | 10.21125 | 2cm |
| Textbox12 (LivingNameFour) | =Fields!LivingNameFour.Value | 17.69931 | 11.07715 | 2.2 | 10.21125 | 2cm |
| Textbox10 (LivingNameFive) | =Fields!LivingNameFive.Value | 17.69931 | 8.86362 | 2.2 | 10.21125 | 2cm |
| Textbox11 (LivingNameSix) | =Fields!LivingNameSix.Value | 17.69931 | 6.62834 | 2.2 | 10.21125 | 2cm |

**排版視覺**：

```
   col3 (L=6.63)   col2 (L=8.83)   col1 (L=11.09)
┌────────────────┬────────────────┬────────────────┐
│ Three (Tb13)   │ Two (Tb9)      │ One           │  row1 (T=7.31)
├────────────────┼────────────────┼────────────────┤
│ Six (Tb11)     │ Five (Tb10)    │ Four (Tb12)   │  row2 (T=17.70)
└────────────────┴────────────────┴────────────────┘
```

> ⚠️ **Textbox 名稱與 Field 綁定的順序不是直覺對應**：Textbox9 綁 LivingNameTwo、Textbox10 綁 LivingNameFive、Textbox11 綁 LivingNameSix、Textbox12 綁 LivingNameFour、Textbox13 綁 LivingNameThree。實作 QuestPDF 時依「Field 名稱」決定哪個值放哪個位置，不要照 Textbox 編號順序排。

## 15. tmpWorshipOne.rdlc（普桌 1 — 單陽上 3cm 字級）

**頁面**：21cm × 29.6cm；背景圖同 tmpWorship

| Name | 綁定 | Top | Left | Width | Height | FontSize |
|---|---|---|---|---|---|---|
| Number | =Fields!Number.Value | 4.474 | 5.588 | 8.903 | 2.206 | 2cm Bold Center |
| LivingNameOne | =Fields!LivingNameOne.Value | 7.312 | 8.550 | – | 18.65 | **3cm** |

## 16. tmpWorshipTwo.rdlc（普桌 2 — 雙陽上 3cm）

**頁面**：21cm × 29.6cm

| Name | 綁定 | Top | Left | Width | Height | FontSize |
|---|---|---|---|---|---|---|
| Number | =Fields!Number.Value | 4.474 | 5.588 | 8.903 | 2.206 | 2cm Bold Center |
| LivingNameOne | =Fields!LivingNameOne.Value | 7.312 | 10.349 | – | 17.567 | 3cm |
| LivingNameTwo | =Fields!LivingNameTwo.Value | 7.312 | 6.622 | – | 17.567 | 3cm |

## 17. tmpWorshipThree.rdlc（普桌 3 — 三陽上 2×2 矩陣 3cm）

| Name | 綁定 | Top | Left | Width | Height | FontSize |
|---|---|---|---|---|---|---|
| Number | =Fields!Number.Value | 4.474 | 5.588 | 8.903 | 2.206 | 2cm Bold Center |
| LivingNameOne | =Fields!LivingNameOne.Value | 7.312 | 8.550 | – | – | 3cm |
| LivingNameTwo | =Fields!LivingNameTwo.Value | 14.471 | 8.550 | – | – | 3cm |
| LivingNameThree | =Fields!LivingNameThree.Value | 14.471 | 5.008 | – | – | 3cm |

## 18. tmpWorshipFour.rdlc（普桌 4 — 四陽上 2cm）

| Name | 綁定 | Top | Left | Width | Height | FontSize |
|---|---|---|---|---|---|---|
| Number | =Fields!Number.Value | 4.474 | 5.588 | 8.903 | 2.206 | 2cm Bold Center |
| LivingNameOne | =Fields!LivingNameOne.Value | 7.312 | 10.458 | – | – | 2cm |
| LivingNameFour | =Fields!LivingNameFour.Value | 17.699 | 7.263 | – | – | 2cm |

## 19. tmpWorshipFive.rdlc（普桌 5 — 五陽上 2cm）

| Name | 綁定 | Top | Left | Width | Height | FontSize |
|---|---|---|---|---|---|---|
| Number | =Fields!Number.Value | 4.474 | 5.588 | 8.903 | 2.206 | 2cm Bold Center |
| LivingNameOne | =Fields!LivingNameOne.Value | 7.312 | 10.14 | – | – | 2cm |
| LivingNameTwo | =Fields!LivingNameTwo.Value | 7.312 | 7.585 | – | – | 2cm |
| LivingNameFour | =Fields!LivingNameFour.Value | 17.699 | 8.864 | – | – | 2cm |

## 模板選擇邏輯（**直接擷取自 [SignupForm.cs:1148-1228](../../reference/old/Ceremony/SignupForm.cs#L1148-L1228) / :1335-1357 / :1554-1593**）

**這不是推測 — 是 code ground truth**。新版實作必須完全複製此邏輯，不可優化、不可合併變體。

「N 位」判定條件：`name != null && name.Trim() != ""`。「無第 N+1 位」條件：`name == null || name.Trim() == ""`。

### Tablet 系列（PrintTablet）— 9 個變體選擇

```
依「亡者數 × 陽上數」二維選擇 + 動態 ParaFontSize

if (DeadName 只有 1 位) {
    if (LivingName 只有 1 位)        → tmpTabletOneOne.rdlc
    else if (LivingName 只有 2 位)   → tmpTabletOneTwo.rdlc
    else                              → tmpTabletOne.rdlc           // 1 亡 3-6 陽
    ParaFontSize = DeadNameOne.Length > 7 ? "0.6cm" : "0.8cm"
}
else if (DeadName 只有 2 位) {
    if (LivingName 只有 1 位)        → tmpTabletTwoOne.rdlc
    else if (LivingName 只有 2 位)   → tmpTabletTwoTwo.rdlc
    else                              → tmpTabletTwo.rdlc           // 2 亡 3-6 陽
    ParaFontSize = (DeadNameOne.Length > 7 || DeadNameTwo.Length > 7) ? "0.6cm" : "0.8cm"
}
else {  // DeadName 3 位以上（含 6 位）
    if (LivingName 只有 1 位)        → tmpTablet_One.rdlc           // 注意底線版本
    else if (LivingName 只有 2 位)   → tmpTablet_Two.rdlc
    else                              → tmpTablet.rdlc              // 3+ 亡 3-6 陽 (fallback)
    ParaFontSize = "0.6cm" (固定，無動態判斷)
}
```

**關鍵理解**：
- `tmpTablet.rdlc`（基本版檔名）= 3+ 亡 + 3+ 陽（**fallback**），不是「1 亡 1 陽」
- 底線版 `tmpTablet_One / _Two` 專供「3+ 亡者」場景，ParaFontSize 預設更小（0.6cm）以容納更多文字
- `ParaFontSize` 對 1-2 位亡者場景有動態邏輯：DeadName 字長 > 7 字 → 縮小到 0.6cm

### Text 系列（PrintText）— 2 個變體選擇

```
if (DeadName 只有 2 位)              → tmpTextTwo.rdlc
else                                  → tmpText.rdlc                // 1 亡 OR 3+ 亡 OR 6 亡
```

**關鍵理解**：tmpText 是 fallback；tmpTextTwo **只有「恰好 2 位亡者」**才會被選中。

### Worship 系列（PrintWorship）— 6 個變體選擇

```
// 依 LivingName 最高位數遞減判斷（從 Six 往 One 往下找）
if (LivingNameSix 有值)              → tmpWorship.rdlc              // 6 位（含 6 位陽上的 fallback）
else if (LivingNameFive 有值)        → tmpWorshipFive.rdlc          // 5 位
else if (LivingNameFour 有值)        → tmpWorshipFour.rdlc          // 4 位
else if (LivingNameThree 有值)       → tmpWorshipThree.rdlc         // 3 位
else if (LivingNameTwo 有值)         → tmpWorshipTwo.rdlc           // 2 位
else if (LivingNameOne 有值)         → tmpWorshipOne.rdlc           // 1 位
```

**關鍵理解**：
- 普桌**不考慮 DeadName**（WorshipDataSet 本來就沒 DeadName 欄位）
- `tmpWorship.rdlc`（基本版檔名）對應「**有第 6 位陽上**」，不是「無陽上的 fallback」
- 程式碼用 `if/else if` 而非 `else if (Six != null && Five != null...)`：只要 LivingNameSix 有值就走 tmpWorship；忽略中間是否有空格（「第 1、2、3、5、6 位有值，第 4 位空」仍走 tmpWorship）

### 跨報表選擇（哪些信眾要印哪些報表）

「列印薦牌」、「列印文牒」、「列印普桌」是**獨立 menu item**（不互斥），承辦人員依場合各自勾選。同一筆報名可同時印 3 種報表（資料卡 + 收據 + 薦牌 + 文牒 + 普桌），各自走上述變體選擇。

**普桌（PrintWorship）僅在 SignupType=4（普桌）時 enabled**，其餘類型 menu item 變灰；見 [SignupForm.cs:140-144](../../reference/old/Ceremony/SignupForm.cs#L140-L144)。

## 全局樣式總表

| 屬性 | 全 RDLC 統計 |
|---|---|
| 字體 | **標楷體 100%** |
| 字色 | **#000000 100%**（無紅/灰/其他色） |
| 字級 0.6cm | 多數 HallName / 小標 / 電話 / 備註 |
| 字級 0.8cm | DeadName / LivingName / 地址（最常用） |
| 字級 1cm | DataCard Number / Text Number |
| 字級 2cm | Worship Number / Worship Living（大字直印） |
| 字級 3cm | tmpWorshipOne/Two/Three 的 LivingName（極大字） |
| 字級 14pt | Receipt 主資訊 |
| 字級 16pt | Receipt 郵寄標籤區 |
| Bold | Number 欄位（資料卡 / 收據 / 牌位 / 普桌 / 文牒 5 種報表都是 Bold） |
| TextAlign Center | Number（普桌） / Prepay（資料卡） |
| TextAlign Right | DataCard「確認無誤請簽名」 |
| VerticalAlign Middle | HallNameFirst/Second（牌位/文牒） |
| Padding 2pt | Receipt 全欄、Text 部分欄 |
| Border None | 99% TextBox |
| Line Solid | 簽名底線（DataCard） |
| Line Dashed | DataCard 中段虛線分隔 |
| CanGrow | DeadNameTwo/Three（牌位/文牒） |
| CanShrink | DeadNameFour/Five（牌位） |

## ReportParameter（動態字級）

部分牌位 RDLC 用 `ParaFontSize` 參數控制 DeadName 字級（讓 DeadName 文字過長時可調小）：

| RDLC | ParaFontSize 預設 |
|---|---|
| tmpTablet | 0.8cm |
| tmpTabletOne / OneOne / OneTwo | 0.8cm |
| tmpTabletTwo / TwoOne / TwoTwo | 0.8cm |
| **tmpTablet_One / _Two** | **0.6cm**（兩例外） |

呼叫 LocalReport 時透過 `report.SetParameters(new ReportParameter("ParaFontSize", "0.8cm"))` 傳入。新版 QuestPDF 對應做法：依 DeadName 字數動態決定字級。

## EmbeddedImage（內嵌背景圖）— **更正前述推測**

實際 grep `<Image><Source>Embedded</Source><Value>...</Value>` 後確認：**6 個 tmpWorship*.rdlc 全部使用同一張 `worship2`**，不是 worship2/3/4/5 分別配對。

| RDLC | 引用 EmbeddedImage Name |
|---|---|
| tmpWorship | worship2 |
| tmpWorshipOne | worship2 |
| tmpWorshipTwo | worship2 |
| tmpWorshipThree | worship2 |
| tmpWorshipFour | worship2 |
| tmpWorshipFive | worship2 |

> 每份 RDLC 還內嵌 worship / worship1 兩張未引用的圖（設計者過程留下的死資源）。圖檔已抽出至 [reference/extracted-images/](../../reference/extracted-images/)：
> - **worship2.png**（63 KB）→ 新版要還原的背景圖
> - worship.png / worship1.jpg（死資源，保留備查）
> - tablet-tablet.jpg（tmpTablet 內嵌但未引用）

## DataSet / Fields 對映

| RDLC | DataSet | 主要 Fields |
|---|---|---|
| tmpDataCard | DataCardDataSet | SignupID, Number, Prepay, LivingName1-6, DeadName1-6, Address, Phone, Remark, HallName |
| tmpReceipt | ReceiptDataSet | SignupID, Name, Fee, Number, Zipcode, Address, Year, Month, Day, Prepay |
| tmpTablet* | TabletDataSet | SignupID, HallNameFirst, HallNameSecond, LivingName1-6, DeadName1-6, Number |
| tmpText / tmpTextTwo | TextDataSet | TabletDataSet 全部 + TextAddress + **PhotoAddress (byte[])** |
| tmpWorship* | WorshipDataSet | SignupID, Number, LivingName1-6（**無 DeadName，無 HallName**） |

## Image 元件（PhotoAddress）

僅 tmpText 與 tmpTextTwo 含此元件：

| 屬性 | 值 |
|---|---|
| Name | PhotoAddress |
| Source | Database（從 byte[] 動態載入） |
| Value | =Fields!PhotoAddress.Value |
| MimeType | image/png |
| Sizing | FitProportional（保持比例） |
| Top | 4.1cm |
| Left | 25.4cm |
| Width | 0.66cm |
| Height | 16.8cm |

舊系統由 `Library.DrawText()` 產 25×605 px PNG（垂直疏文地址），存入 TextViewModel.PhotoAddress。新版 QuestPDF 可直接 `column.Item().Rotate(90).Text(...)` 或畫 Canvas。

## QuestPDF 還原指引

1. **Page size + Margin**：用 `page.Size(w, h, Unit.Centimetre)` + `page.Margin(0)`（tmpTabletOneOne 用 `page.MarginTop(2, Unit.Centimetre).MarginBottom(2, Unit.Centimetre)`）
2. **絕對定位**：QuestPDF 用 `container.Translate(x, y).Element(...)` 或 `Canvas` 模擬絕對座標。**禁止使用 Row/Column 的自然流排版**，因為長字串會自動 wrap 或推擠下一個欄位
3. **字體註冊**：啟動時 `FontManager.RegisterFont(File.OpenRead("Fonts/biaukai.ttf"))`；DefaultTextStyle 設 BiauKai。**嚴禁** fallback 到微軟正黑體 — 即使部分罕用字 BiauKai 無法支援，仍應透過 font subset / glyph fallback 補字而非整體改字型
4. **動態字級（ParaFontSize 對應）**：完全照舊 code 的判斷（`DeadNameOne.Length > 7`，不是 `> 5`、不是 `> 10`）：
   ```csharp
   var paraFontSize = (deadOne?.Length ?? 0) > 7 ? 0.6f : 0.8f;
   // 2 位亡者情境另加 deadTwo 判斷
   ```
5. **垂直文字（牌位窄欄、普桌大字直書）**：QuestPDF 對中文直書支援不佳，建議用 `SkiaSharp` Canvas API 自繪：每個字 `canvas.DrawText(c, x, y)` 並沿 y 軸遞增字高
6. **PhotoAddress（文牒垂直地址）**：保留舊 `Library.DrawText()` 產 25×605 px PNG 的邏輯（避免 1:1 差異），新版仍存 byte[] 入 `TextViewModel.PhotoAddress`，PDF 端 `container.Image(bytes)`
7. **背景圖（普桌 worship2）**：`page.Background(...).Image("worship2.png")`；位置精確到 `(Top=0.26141cm, Left=0.42cm)` × `(Width=20.04729cm, Height=28.88438cm)` FitProportional。**不可四捨五入**到 0.26/0.42
8. **粗體**：`.Bold()` 在 Number 欄位（DataCard / Receipt 下聯 Number / Tablet Number / Worship Number / Text Number）
9. **置中**：`.AlignCenter()` 在 Number；對應原 TextAlign=Center
10. **邊距 padding**：Receipt 系列加 `.Padding(2, Unit.Point)`
11. **CanGrow / CanShrink**：QuestPDF 預設 auto-size；用 `MinHeight` / `MaxHeight` 約束。**牌位 / 普桌 / 文牒禁用 auto-size**（必須固定 Height，否則套印錯位）
12. **誤差容忍**：所有座標誤差 ≤ ±0.05cm（CI 自動量測）；實體列印 ≤ ±0.2cm（驗收標準）
13. **單位換算**：`0.6cm ≈ 17pt`；`0.8cm ≈ 22.7pt`；`1cm ≈ 28.35pt`；`2cm ≈ 56.7pt`；`3cm ≈ 85pt`

## 對位驗收 Checklist（**每個 RDLC 模板 PR 必跑**）

新版列印模板（QuestPDF 實作）的 PR 進入 review 前，**送 PR 的工程師必須完成以下檢查並把結果貼到 PR description**。QA 收到 PR 後 spot-check 任 2 項做 cross-check。

### A. 靜態檢查（程式內）

- [ ] 模板 page size 與本檔「紙張尺寸總覽」表完全一致
- [ ] 所有 `Top / Left / Width / Height` 數值直接 hardcode 自本檔對應章節（**不可從變數計算**）
- [ ] 字級用本檔列出的精確值（0.6 / 0.7 / 0.8 / 1.0 / 2.0 / 3.0 cm 或 14pt / 16pt），不可換算成接近值
- [ ] Number 欄位 `Bold = true`；Worship Number 額外 `TextAlign = Center`
- [ ] HallNameFirst/Second `VerticalAlign = Middle`
- [ ] 用 BiauKai 字型；無 fallback 到其他字型
- [ ] Margin 全 0（tmpTabletOneOne 例外：Top/Bottom 2cm）
- [ ] 變體選擇邏輯**逐字複製**自「模板選擇邏輯」章節，含 `if/else if` 順序與 `name?.Trim()` 判定

### B. 動態量測（產出 PDF）

對該模板的每個變體都要做：
- [ ] 用 6 種 fixture data 各產一份 PDF：
  - 全部欄位填滿（最大長度）
  - 全部欄位空白（最小，只剩骨架）
  - 只有 1 位亡 + 1 位陽
  - 邊界 case：DeadName 字長 = 7（不觸發 ParaFontSize 縮小）
  - 邊界 case：DeadName 字長 = 8（觸發縮小到 0.6cm）
  - 中文罕用字（如「龘」「靐」）測字型 fallback
- [ ] 開啟生成的 PDF，**量測**每個 TextBox 的 absolute position：
  ```bash
  # 使用 pdfinfo / pdftotext / mutool 抽座標
  mutool info -f new-output.pdf
  pdftotext -bbox-layout new-output.pdf - | grep -E '"[^"]+" .* x="[\d.]+" y="[\d.]+"'
  ```
- [ ] 用以下 Python 腳本對照舊系統 PDF（[scripts/diff-pdf-positions.py](../../scripts/diff-pdf-positions.py)，若不存在於新版實作期建立）：
  ```python
  # 量測舊版 vs 新版每個欄位的 x,y 差值
  # 任一欄位差異 > 0.05cm 即 fail
  ```

### C. 實體列印對位（QA 階段）

- [ ] 把新版 PDF 列印到**對應的預印格式紙**上（牌位紋飾紙 / 普桌紋飾紙 / 文牒紋飾紙）
- [ ] 把舊系統相同 input 列印到同款紙上
- [ ] 兩張紙**疊放透光檢查**：欄位邊緣差異 ≤ 0.2cm
- [ ] 拍照存證（含尺規對照）並附 PR description

### D. 9 個牌位變體 + 6 個普桌變體 + 2 個文牒變體的 fixture 覆蓋

| 模板 | fixture：DeadName 數 | fixture：LivingName 數 | 預期選中 |
|---|---|---|---|
| tmpTabletOneOne | 1 | 1 | ✓ |
| tmpTabletOneTwo | 1 | 2 | ✓ |
| tmpTabletOne | 1 | 3, 4, 5, 6（4 組） | ✓ |
| tmpTabletTwoOne | 2 | 1 | ✓ |
| tmpTabletTwoTwo | 2 | 2 | ✓ |
| tmpTabletTwo | 2 | 3, 4, 5, 6（4 組） | ✓ |
| tmpTablet_One | 3, 4, 5, 6 | 1（4 組） | ✓ |
| tmpTablet_Two | 3, 4, 5, 6 | 2（4 組） | ✓ |
| tmpTablet | 3, 4, 5, 6 | 3, 4, 5, 6（16 組） | ✓ |
| tmpTextTwo | 2（任意 LivingName）| 任意 | ✓ |
| tmpText | 1 / 3 / 4 / 5 / 6 | 任意 | ✓ |
| tmpWorshipOne | — | 1 | ✓ |
| tmpWorshipTwo | — | 2 | ✓ |
| tmpWorshipThree | — | 3 | ✓ |
| tmpWorshipFour | — | 4 | ✓ |
| tmpWorshipFive | — | 5 | ✓ |
| tmpWorship | — | 6 | ✓ |

完整 fixture matrix 共 **45 組牌位 + 6 組文牒 + 6 組普桌 = 57 組**測試案例。CI 自動跑「靜態檢查 + 動態量測」，QA 抽 10 組做實體列印驗證。

## 已知陷阱與邊界 case

實作時容易踩雷的點：

1. **`name == null` vs `name.Trim() == ""`**：舊 code 兩者都判斷，**新版必須沿用**：`name != null && name.Trim() != ""`。只判斷 null 會把空白字串視為「有值」，選錯變體
2. **Textbox 名稱 ≠ Field 名稱**：tmpWorship 內 `Textbox9` 綁 `LivingNameTwo`、`Textbox13` 綁 `LivingNameThree` 等。**依 Field 綁定決定值的位置**，別照 Textbox 編號排
3. **ParaFontSize 對 3+ 亡者場景無動態邏輯**：tmpTablet / tmpTablet_One / tmpTablet_Two 固定 0.6cm，不論 DeadName 長度
4. **tmpReceipt Tablix 跨頁**：ReportSize=29.7cm 但 Tablix 內容 59.4cm（雙聯 + 標籤），新版 QuestPDF 須產 2 頁 PDF。Tablix 內第一聯欄位 Top 在 2-7cm，第二聯在 12-17cm，標籤區 33-35cm（第 2 頁）
5. **tmpTabletOneOne 的 Margin 2cm 是真正的 RDLC Page Margin**，不是 Body Margin。QuestPDF 用 `page.MarginTop(2, Unit.Centimetre)`，不要用 `container.PaddingTop(2cm)`
6. **背景圖座標精度**：worship2.png 的座標精確到小數 5 位（0.26141cm），這是 designer 對著紋飾紙紋路逐次微調的結果。任何「四捨五入到 0.26cm」都會錯位
7. **避 4 顯示是 application 層處理**：DB 仍存 `Number=4`，但 ViewModel 的 `Number` 字串已是「3-1」。RDLC 看到的就是字串，不要再做轉換
8. **薦牌的「點」是排版佔位字符**：HallNameSecond / HallNameFirst 在無堂號時填「．」全形句點，**不是空字串**。新版若改成空字串，整段排版會位移
9. **CanGrow 在 RDLC 預設 true**：但因 Height 已給定，文字超出時會自動撐高 → 推擠下方欄位 → 套印錯位。**新版必須 disable CanGrow**，文字超出寧可截斷也不要撐版
10. **Fee 欄位數字格式**：舊版直接 `Fee.ToString()`（無千分位），新版若用 `Fee:N0`（`1,200`）會與舊版差一個逗號 — 兩版並排會被看出。**保留無千分位格式**
