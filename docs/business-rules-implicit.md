---
title: 隱含業務規則（舊系統未文件化）
purpose: 從舊原始碼反推但未在分析文件記錄的業務規則 — 重構時必須沿用
applicable_when: 寫新系統的業務邏輯、處理邊界 case、設計 API validation、寫驗收測試
related_agents:
  - backend-engineer
  - system-analyst
  - qa-test-engineer
related_docs:
  - blueprints/signup-management.md
  - blueprints/believer-management.md
  - blueprints/prepay-loading.md
  - blueprints/printing-reports.md
  - design/database-design.md
keywords: [business rules, 業務規則, 隱含, 不變式, 驗證, 編號, 月份, 季別, 春季, 中元, 秋季]
last_updated: 2026-06-29 (§16 列印普桌啟用條件改看選取列而非搜尋篩選)
---

> 本文收錄**舊系統 code 內隱含、但原分析文件未明寫**的業務規則。每條都附 source 引用。新系統實作時要逐條沿用，否則容易與舊行為偏離。

## 1. 編號生成與唯一性

### 1.1 編號生成純序列，不跳號
- `Library.GetSignupNumber(Year, CeremonyCategoryID, SignupType)` 取 MAX(Number)+1
- **不跳過 4**（避 4 只在顯示層）
- 無記錄則回 1

### 1.2 唯一性檢查的 key
- key 是 `(Year, CeremonyCategoryID, SignupType, Number)` — **不含 BelieverID**
- 編輯時排除自身 `a.SignupID != ParamSignupID`
- 重複訊息：`"{Year} {Ceremony} {Type} 編號重複，請重新確認！"`

### 1.3 Number 實質 NOT NULL
- DB schema 允許 nullable
- 應用層永遠寫入值（GetSignupNumber 或 cbKeepNumber 手動）
- 顯示時直接 `(int)cell.Value` 無 null check

### 1.4 同信眾同 (Year, Ceremony, SignupType) 可有多筆 Signup
- code **不檢查** BelieverID 重複；只檢查 Number 重複
- 允許情境：同信眾在中元同時報「一般」+「觀音會」（不同 SignupType，編號不衝突）

### 1.5 BelieverID 可為 null（寺方場景）
- LoadPrepay case 3（寺方）允許 null BelieverID
- 新增時若選「寺方」類型可不綁信眾
- 列印時若 Signup.Name 為 null 則從 Believer 取

---

## 2. 避 4 規則邊界（**完整定義**）

完整定義見 [printing-reports.md](blueprints/printing-reports.md) 或 [glossary.md](glossary.md) §「避 4」。重點：

- **只避個位 4**，十位/百位/千位的 4 **不避**
- DB 存實值，僅顯示轉換
- 矩陣：

| Number | 顯示 |
|---|---|
| 4 | `3-1` |
| 14 | `13-1` |
| 40 | `40`（不避） |
| 44 | `43-1` |
| 140 | `140`（不避） |
| 144 | `143-1` |
| 400 | `400`（不避） |
| 404 | `403-1` |

---

## 3. Believer 與 Signup 兩級欄位設計

### 3.1 編輯 Signup 時的 Believer 同步策略

> ⚠️ **新版刻意偏離 legacy（2026-06-29，方案 C）**：legacy `EditSignupForm.btnConfirm` 會把 HallName / EmployeeType / IsFixedNumber 回寫 Believers——但因這些是**信眾層級**屬性、清單堂號又靠 `SignupView` JOIN 帶出，導致「編輯一筆報名改堂號→同信眾全部報名連動」的缺陷。新版 `UpdateSignupHandler` **完全不回寫 Believer**，這三欄只在信眾維護頁修改。見 [signup-hallname-isolation.md](blueprints/signup-hallname-isolation.md)。

**legacy 會同步至 Believers（新版已停止）**：
- HallName（堂號）→ 新版報名表單改唯讀顯示，僅信眾維護頁可改
- EmployeeType（員工類型）→ 報名表單早已唯讀
- IsFixedNumber（固定編號）→ 報名表單早已唯讀

**legacy 與新版都不同步**：
- Name
- Phone

設計動機：
- Believer 是**人員主檔**（堂號/身分屬個人固有，跨報名一致）
- Signup 是**該次報名快照**（每年要寄哪、寫什麼名可能不同；姓名/電話/地址/名單為報名自有欄）
- 堂號既屬信眾固有 → 一處維護（信眾頁），報名頁不得改，避免連動污染

### 3.2 信眾資料帶入（NewSignup 選既有信眾時）優先順序

```
Name/Phone：
  1. Signup record（若有 SignupID）
  2. DataGridView 列的 ColName/ColPhone
  3. Believer.Name/Phone（fallback）

Address (Mail/Text)：
  1. Signup.Zipcodes
  2. Believer.Zipcodes
```

### 3.3 編輯 Signup 時 SignupLog 寫入的 Name 來源

⚠️ **不一致行為**（舊系統 quirk）：
- **新增**：寫 SignupLog 用 `txtName.Text`（編輯區當下值）
- **編輯**：寫 SignupLog 用 `believer.Name`（DB 內 Believer 主檔，**不是** Signup 級的 Name）

新系統應該統一（建議都用 Signup 級 Name 寫 log，避免資訊遺失）。

---

## 4. 年份限制

| 場景 | 規則 | 訊息 |
|---|---|---|
| 新增報名 Year | < 當年民國年 → 拒 | `請勿輸入今年以前的年份` |
| 預繳 PrepayYear | < 當年 → 拒；通常 ≥ 當年+1 | `預繳年份需大於{currentYear}，請重新確認！` |
| 編輯舊年（Year < 當年） | 預繳區塊整個 disabled | – |

---

## 5. 法會分類刪除限制（**雙重檢查**）

```csharp
if (!ceremonycategory.Signups.Any() && !ceremonycategory.CeremonyCategorys1.Any()) {
    // 可刪
} else {
    // "已有報名或還有下層法會，無法刪除"
}
```

- 檢查 1：該分類無 Signups
- 檢查 2：該分類無子分類（即使子分類本身也無 Signups）

---

## 6. 信眾刪除（**整批中止**）

`BelieverForm.tsmiDelete_Click`：

```csharp
foreach (DataGridViewRow dgvRow in dgvBelievers.SelectedRows) {
    Believers believer = believersService.GetByID(...);
    if (believer.Signups.Any()) {
        MessageBox.Show(believer.Name + " 已有報名資料，不能刪除！");
        return;  // **整批中止**
    }
    deletes.Add(BelieverID);
}
```

- 多選刪除時，**任一信眾有報名**即**整批中止**
- **不會**跳過該筆繼續刪其他

新系統可考慮改為「跳過該筆 + 顯示哪些被跳過」的 UX，但目前保留舊行為避免使用者驚訝。

---

## 7. 載入預繳（**無 idempotency**）

詳見 [prepay-loading.md](blueprints/prepay-loading.md)。重點：

- **無 idempotency 檢查** — 連按確認或重啟後再跑會產生**重複資料**
- 唯一防護：`btnConfirm.Enabled = false`（line 63）
- **無顯式 transaction**，EF SaveChanges 自帶
- 6 case 連續 Create 至 DbContext，最後 SaveChanges 一次

新系統必須加 idempotency 檢查（已在 prepay-loading blueprint 標註）。

---

## 8. 列印模板選擇（**3 系列 17 個變體的觸發條件**）

詳見 [printing-reports.md](blueprints/printing-reports.md)。重點：

- **薦牌 9 變體**：依 DeadName 深度（1 / 2 / 3+）× LivingName 深度（1 only / 2 only / 3+）3×3 矩陣
- **文牒 2 變體**：DeadNameTwo 有值 AND DeadName3..6 空 → `tmpTextTwo`，否則 `tmpText`
- **普桌 6 變體**：依 LivingName 最高有值位置 → tmpWorshipOne / Two / Three / Four / Five / tmpWorship
- **資料卡 / 收據**：固定，無變體

### 薦牌字級邏輯（ParaFontSize）

| DeadName 深度 | DeadName 字長 | ParaFontSize |
|---|---|---|
| 僅 DeadName1 | > 7 字 | 0.6cm |
| 僅 DeadName1 | ≤ 7 字 | 0.8cm |
| DeadName1+2 | 任一 > 7 字 | 0.6cm |
| DeadName1+2 | 都 ≤ 7 字 | 0.8cm |
| DeadName 3+ | 任意 | **固定 0.6cm** |

> **字長以「真實字數」計（排除半形/全形空格）。** 使用者會在姓名中間刻意輸入空格作排版間隙（直書渲染時保留為空白列），此間隙**不計入** > 7 字門檻。
> 實作：`PrintTemplateSelector.RealCharCount`（`char.IsWhiteSpace`，涵蓋 U+0020 與全形 U+3000）。
> **刻意偏離 legacy**：舊 `SignupForm.cs:1179/1203` 用 `Trim().Length`，會把中間空格計入而誤縮字級。詳見 [gotchas.md](gotchas.md)「姓名中間空格」條與 [legacy-coverage/signup-form.md](blueprints/legacy-coverage/signup-form.md)。

---

## 9. 寺方編號顯示特例

- SignupType=2（寺方）：顯示時**只顯示 NumberTitle「寺」，不附 Number**
- SignupForm line 302 的格式邏輯：`row["Display"] = (signupType == 2) ? numberTitle : numberTitle + GetNumberText(number)`

---

## 10. Phone 全/半形轉換

- 存入前用 `Microsoft.VisualBasic.Strings.StrConv(VbStrConv.Narrow)` 全形 → 半形
- 規則：信眾與報名儲存時都做
- Regex：`^0[0-9]*$`（必 0 開頭）

新系統用自製工具實現（不依賴 VB runtime）。

---

## 11. 表單驗證 regex 一覽

| 欄位 | Regex | 允許空 |
|---|---|---|
| 民國年 | `^1[0-9]{2}$` | 否 |
| 電話 | `^0[0-9]*$` | 是 |
| 編號 | `^[1-9][0-9]*$` | 視情境 |
| 費用 | `^[0-9]*$` | **是**（空字串視為 0 或不填） |

---

## 12. 「同寄件地址」勾選邏輯

- 勾選：複製 Mail 至 Text；mail 為空時阻止勾選並彈 `"請先輸入寄件地址"`
- 取消：清空 Text 區（City/Zone/Address 全還原為 placeholder）

實作細節：用 `SelectedIndex` 複製（兩個 City list 順序相同因 query 一樣，但脆弱）。

---

## 13. 編輯舊年報名的限制

- 若 `signup.Year < currentTaiwanYear`，UI 上：
  - `txtPrepayYear.Enabled = false`
  - `dlPrepayCeremony.Enabled = false`
- 業務意義：舊年資料不允許再加/改預繳（已成定局）

---

## 14. PredicateBuilder 搜尋默認

- 全空條件 → AND predicate = true、OR predicate 不套用 → **回傳全部資料**
- 結果為空 → 顯示「無資料，請重新搜尋！」
- 主搜尋面板 OR 條件依**任一姓名/陽上/往生/電話**checkbox 啟用

---

## 15. 新增 vs 編輯的 Number 行為

| 場景 | Number 處理 |
|---|---|
| 新增、`cbKeepNumber` 未勾 | `Library.GetSignupNumber()` 自動產 |
| 新增、`cbKeepNumber` 勾 + 空 | 拒：`請輸入編號` |
| 新增、`cbKeepNumber` 勾 + 重複 | 拒：`{Year} {Ceremony} {Type} 編號重複，請重新確認！` |
| 編輯、修改 Number | 檢查重複（排除自身 SignupID），訊息：`編號重複，請重新確認！` |

---

## 16. 「列印普桌」啟用條件（2026-06-29 改：改看選取列，不看搜尋篩選）

- SignupForm 右鍵 menu「列印普桌」：**只要選取的每一列 `signupType == 4`（普桌）即 enabled，與搜尋篩選 `signupType` 無關**
- 選取若夾雜任一筆非普桌資料 → grey out + tooltip「選取含 N 筆非普桌資料，僅普桌(類型 4)可列印」
- 其他四種列印選項恆可用

> **舊規則（已淘汰）**：原本「僅當搜尋篩選 `signupType == 4` 才 enabled」。改為驗證實際選取列，讓使用者在「全部」篩選下也能直接挑普桌資料列印，不必先切篩選。
>
> **為何安全（無 bug）**：啟用條件放寬只是 UX 層；真正的防呆在後端且未動 —
> - 單筆列印走 `GET /reports/worship?signupId=`，by-id 驗證 `SignupType != 4 → 422 WORSHIP_ONLY_TYPE_4`（[GenerateReportHandlers.cs:121](../backend/src/Ceremony.Application/Reports/GenerateReportHandlers.cs)）
> - 批次列印走編號區間，`BatchReportHandler` **強制 `SignupType=4`**（[BatchReportHandler.cs:25-26](../backend/src/Ceremony.Application/Reports/BatchReportHandler.cs)），區間內非普桌列一律被過濾，不會套錯版型
> - 前端只放行「選取全為普桌」，混選直接擋下，所以送到後端的一定是合法集合
>
> 批次走編號區間的既有不精確性（區間內未選的普桌列也會印）維持不變，仍由既有 confirmation dialog 提示。

---

## 17. 月份 → 季別法會對照（**新版加值規則，非舊系統反推**）

> ⚠ 此規則為新系統新增（舊 NewSignupForm 無自動判斷，季別永遠人工選），2026-06-23 由業主定案。同時釐清 [pending-business-input.md](pending-business-input.md) B3 的「月份範圍」部分。

- 報名表單**新增模式**依當前月份自動帶出對應季別 root 法會（可編輯的預設，非鎖定）：

  | 月份 | 季別 | Root GUID |
  |---|---|---|
  | 1–4 月 | 春季 | `18927907-dcad-42b2-8f2a-635c2e0fa98d` |
  | 5–8 月 | 中元 | `0c478f0e-787c-448e-ba7b-b1579f3f1fce` |
  | 9–12 月 | 秋季 | `3864e4dc-24db-4544-acb3-3351592f6dab` |

- 只帶**季別 root**；子法會（梁皇寶懺、盂蘭盆…）仍由使用者人工挑選，逐年不同。
- 月份取自系統當下日期（公曆月份；民國年僅換算年份不影響月份）。
- 邊界：4/5 月之交切春季↔中元、8/9 月之交切中元↔秋季。
- 實作：`frontend/src/app/shared/util/ceremony-season.ts`（`seasonForMonth` / `currentSeason` / `resolveSeasonRootId`，GUID 優先、title 退場）；表單 `applySeasonDefault()` 僅在 create 模式且欄位尚未有值時帶入，編輯模式不覆蓋。
