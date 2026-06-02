---
title: 信眾維護
purpose: 信眾資料的搜尋、新增、修改、刪除；對應舊 BelieverForm
status: draft
applicable_when: 要修改信眾欄位、調整搜尋邏輯、修改地址下拉、改變刪除限制
related_agents:
  - software-architect-blueprint
  - backend-engineer
  - frontend-architect
related_docs:
  - ../design/database-design.md
  - ../design/api-design.md
  - ../design/visual-design.md
keywords: [believer, 信眾, 信眾維護, BelieverForm, 地址, 陽上, 往生]
last_updated: 2026-05-29
---

## 背景與動機

信眾是核心主檔：所有報名都連結到信眾。舊 BelieverForm 為「左 grid + 右編輯區」兩欄式，含 5 個搜尋欄位、26 欄 DataGrid、30+ 編輯欄位。新版必須完整保留版面與行為，同時修正電話全形/半形、雙 Zipcode FK 命名混淆等 quirks。

## 範圍

### 做什麼
- 信眾搜尋（姓名 / 電話 / 堂號 / 陽上 / 往生 — 至少填一）
- 信眾 CRUD（含地址下拉、雙地址、6 組陽上、6 組往生）
- 員工類型管理（1=非員工 / 2=大殿 / 3=地藏殿）
- 固定編號旗標（IsFixedNumber）
- 刪除限制：有報名紀錄則不可刪
- 「同寄件地址」一鍵複製

### 不做什麼
- 信眾批次匯入（暫由 DataTrans 處理）
- 信眾合併（重複信眾合一） — 未來功能
- 信眾照片 / 附件

## 使用者流程

```
1. 進入 /believers
2. 填寫至少一個搜尋條件 → 「搜尋」
   若全空 → 訊息「請輸入搜尋條件」
3. DataGrid 顯示結果（無結果訊息「無資料，請重新搜尋！」）
4. 點選列 → 右側編輯區帶入該信眾資料
5. 修改 → 「確認」→ 成功「修改信眾成功！」
6. 新增（清空編輯區）→ 填寫姓名（必填）+ 寄件地址（必填）+ 其他 →「確認」→ 成功「新增信眾成功！」
7. 右鍵列 →「刪除」→ 若有報名則顯示「{name} 已有報名資料，不能刪除！」否則「確認刪除嗎？」→「刪除成功！」
```

## 設計決策

### 關鍵選擇

- **保留 6 欄陽上 / 6 欄往生扁平結構**（不正規化為 child table）
  - 理由：列印 RDLC 模板依固定欄位綁定；業務上 6 個固定欄位夠用
  - 取捨：放棄彈性，換取與舊資料 / 列印的相容
- **欄位 header 顯示「陽上3-1」「往生3-1」**（避 4）
  - 理由：宗教場域避諱
- **電話自動全形→半形**
  - 舊：`Microsoft.VisualBasic.Strings.StrConv(VbStrConv.Narrow)`
  - 新：自製 `toHalfWidthDigits()` 工具，前端 + 後端雙重轉換
- **雙地址 FK 改名**：`Zipcodes` / `Zipcodes1` → `mail_zipcode` / `text_zipcode`
- **「同寄件地址」勾選邏輯保留**：mail 空時拒絕勾選，訊息「請先輸入寄件地址」
- **刪除限制走 explicit count query**（非 nav lazy-load）
  - 理由：舊系統用 `believer.Signups.Any()` 觸發 N+1；新版用 `EXISTS (SELECT 1 FROM signups WHERE believer_id = @id)`

### 取捨

- 取了：型別安全、效能可預測
- 捨了：EDMX nav property 的「方便」

## 跨層影響

| 層級 | 是否影響 | 變動摘要 |
|---|---|---|
| 視覺 | 是 | BelieverForm 對應頁面；訊息文字 verbatim |
| 前端 | 是 | believers feature + address-picker + name-list-input 共用元件 |
| 後端 | 是 | BelieverService + Search use case + 電話 normalizer |
| API | 是 | `/believers/*`、`/zipcodes/*` |
| 資料庫 | 是 | believers 表欄位調整；移除舊 `MailZipcode` / `TextZipcode` 純字串冗欄 |
| 基礎建設 | 否 | – |
| 安全 | 部分 | PII（姓名、電話、地址）log mask |

## 業務規則

### 搜尋（對齊舊 BelieverForm:353-409）

- 每欄獨立 AND
- 陽上 / 往生 各為 6 欄 OR-chain（任一 Contains 即命中）
- 無結果 → 訊息「無資料，請重新搜尋！」

### 必填驗證

- 姓名 → 「請輸入姓名」
- 寄件地址 → 「請輸入寄件地址」
- 電話 → 若填，必須 regex `^0[0-9]*$`，否則「聯絡電話格式錯誤，請重新確認！」

### 刪除規則

- 多選刪除：任一筆有報名 → 整批中止 + 顯示該筆訊息
- 全通過 → 確認 → 逐筆軟刪 → 「刪除成功！」

## 驗收標準

- [x] 版面（**2026-05-28 決策改 single-column + form-overlay，非三區**）：搜尋列 + 全欄位 vgrid + overlay 編輯，對齊舊欄位語意
- [x] DataGrid 完整重現（**22 顯示 + 4 隱藏 = 26 欄**；舊「12/14」為筆誤）：[believer-columns.ts](../../frontend/src/app/features/believers/believer-columns.ts) 1:1 對齊 dgvBelievers header/width/順序；往生欄底色；右鍵 + ⋮ context menu「編輯/刪除」
- [ ] 陽上/往生 6 欄輸入元件 label 顯示「3-1」非「4」（清單表頭已顯示「3-1」；編輯 form label 待核）
- [ ] 所有訊息 verbatim
- [ ] 同寄件地址勾選邏輯完整
- [ ] 電話 regex 與全/半形轉換
- [x] 有報名的信眾無法刪除（backend `DeleteBelieverHandler` 回 409 + verbatim；前端 `actionDelete` 顯示後端訊息）
- [x] 至少一個搜尋條件才可送出（前端 + `SearchBelieversHandler`）
- [ ] 通過 [code-review](../workflows/code-review.md) 與 [qa-testing](../workflows/qa-testing.md)

## 風險與未解問題

- 舊 `MailZipcode` / `TextZipcode` 純字串欄位是否還有其他來源依賴？— 確認無下游後 drop
- 大批信眾（50k+）搜尋效能 — 加 `idx_believers_name`、考慮全文索引

## 參考資料

- [scratch/02-believer.md](../../.scratch/explore/02-believer.md)：BelieverForm 控件清單、搜尋 LINQ、地址邏輯、所有訊息
- 舊原始碼：[reference/old/Ceremony/BelieverForm.cs](../../reference/old/Ceremony/BelieverForm.cs)
