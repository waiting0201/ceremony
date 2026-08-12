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
keywords: [business rules, 業務規則, 隱含, 不變式, 驗證, 編號, 月份, 季別, 春季, 中元, 秋季, 預繳, 普桌]
last_updated: 2026-08-12 (新增 §16.1「新增報名『列印資料卡』依報名類型自動挑卡」：普桌〔SignupType=4〕改印普桌資料卡 worshipcard、其餘印一般資料卡 datacard，按鈕文字同步變「列印普桌資料卡」；判斷來源是**存檔當下的類型快照**而非下拉即時值〔存完表單不關閉，改了下拉沒再存檔會印出與該筆記錄不符的版面〕，快照隨跨路由草稿保存、按「取消」歸零。同時在 §16 補適用範圍：§16「不檢查 SignupType」只管**使用者明示選擇**的入口〔右鍵 menu／預覽頁下拉〕，新增表單這顆鈕沒得選＝**系統代選**，兩者不衝突。先前 2026-08-06 (§10 Phone 全/半形轉換擴寫：使用者指定「電話不管全形半形輸入，結果都是半形」，把原本只在存檔時做的 `ToNarrow` 往兩端延伸——前端電話欄新增 `appNarrowInput` directive 於輸入/組字結束當下即轉、信眾搜尋的 Phone 條件在 `SearchBelieversHandler` 也轉半形〔否則寫入端存半形、查詢帶全形永遠撈不到〕；同時寫明兩條邊界：**只轉電話**〔姓名/地址的全形空格是直書排版資料，不可轉〕、**載入時不轉顯示值**〔會讓 DOM 與 control 不一致，而在 writeValue 內回呼 onChange 會把表單誤標 dirty〕。先前 2026-08-05 (新增 §20「代入新增的選列規則與來源列 pin」：工具列「新增報名」補回代入邏輯，但**恰好選 1 筆才代入**〔偏離舊 `selectedcount > 0 → SelectedRows[0]`，因新系統多選是常態〕；代入後自動搜尋並選中來源列時，來源列若被 200 列 DOM 上限切掉會 pin 到最前〔舊 WinForms grid 無列數上限〕；連帶取捨：該情境不還原跨路由草稿。同時 §19 補記漏網路徑——代入新增先前沒帶預繳，現與 `pickBeliever` 共用 `applyBelieverRow` 已一致。先前 2026-07-31 (新增 §12.1「地址／堂號清空必須清得掉」：移除信眾表單 mailAddress 的 required、移除文牒段抄寄件段的隱性 fallback、報名堂號清空改存空字串〔避開 SignupView 的 COALESCE 回退〕；同日先前新增 §19「預繳依單筆報名隔離 — 法會預繳 ≠ 普桌預繳」：客訴「同一次搜尋先點有預繳的法會列、再點沒預繳的普桌列，普桌沿用法會預繳」；真因是舊 BelieverSelected:1102-1115 的跨類型「最新一筆」反查完全不分 SignupType，新版改為選信眾時預繳取該列自身值、不再呼叫 GET /prepay?believerId&year〔endpoint 保留備用〕，標為刻意偏離 legacy。同日先前 §12「同寄件地址」勾選門檻放寬為「城市/區域/地址三者全空才擋」〔取代 07-21 記錄的「仍要求先有寄件地址」〕，提示改「請先填寫寄件地址（城市／區域或地址）」，兩張表單同步；另記 believer-edit-form 的 mailAddress 仍 required、與 07-21 放寬不一致，已列 pending。先前 2026-07-21 (§3.1 反轉為方案 A：堂號/員工類型/固定編號改 per-signup 報名自有欄、可編輯只改這筆、不回寫信眾，view COALESCE 回退，預繳保號仍讀信眾；同日 §12 地址改非必填（前後端同步放寬）；先前 2026-07-18 §16 改版：右鍵普桌/普桌資料卡前端不再鎖、恆啟用，防呆交後端；2026-06-30 §1.4 補新版重複報名警示；§18 薦牌/文牒第 6 位往生/陽上已實作＋回歸測試＋影像驗證)))))
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
- **新版增強（不改上述允許規則）**：新增/編輯報名時，若選定信眾在同一 `(Year, CeremonyCategoryID)`（**忽略 SignupType**）已有報名，前端即時跳**警示**——但**仍可儲存**（提醒而非禁止）。判定走唯讀 endpoint `GET /signups/duplicates`，比對精確的 CeremonyCategoryID（同 §1.2 粒度），編輯排除自身。詳見 [api-endpoints/get-signup-duplicates.md](blueprints/api-endpoints/get-signup-duplicates.md)。

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

> ⚠️ **新版刻意偏離 legacy（2026-07-21 反轉為方案 A：三欄 per-signup 化）**：legacy `EditSignupForm.btnConfirm` 會把 HallName / EmployeeType / IsFixedNumber 回寫 Believers → 造成「改一筆報名堂號→同信眾全部報名連動」缺陷。**新版一律不回寫 Believer**；並讓這三欄成為**報名自有欄**（Signups 加 `HallName/EmployeeType/IsFixedNumber`，`SignupView` 以 `COALESCE(Signups.X, Believers.X)` 回退）。報名表單三欄**可編輯、只改這一筆**；信眾主檔於信眾維護頁維護。見 [signup-hallname-isolation.md](blueprints/signup-hallname-isolation.md)。
>
> （沿革：2026-06-29 曾採方案 C——三欄唯讀、只在信眾維護頁改；2026-07-21 使用者要求「同信眾不同報名可掛不同值」→ 改採方案 A。）

**這三欄現為報名自有覆寫欄（per-signup，2026-07-21）**：
- HallName（堂號）/ EmployeeType（員工類型）/ IsFixedNumber（固定編號）→ 報名表單**可編輯**，寫入 `Signups` 自有欄；未覆寫（null）由 view COALESCE 回退信眾值。**不回寫 Believer**。
- **例外**：預繳載入保號仍讀 `Believers.IsFixedNumber`（per-signup 覆寫不影響預繳保號，2026-07-21 使用者指定）。

**Name / Phone**：legacy 與新版都不回寫 Believer（早已是報名自有欄，同款 per-signup 快照）。

設計動機：
- Believer 是**人員主檔**；Signup 是**該次報名快照**（姓名/電話/地址/名單早已報名自有）。
- 使用者確認堂號/員工類型/固定編號可因報名而異 → 這三欄比照 Name/Phone 成為報名自有欄，改一筆不連動他筆、不污染主檔。

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

新系統用自製工具實現（不依賴 VB runtime）：`ToNarrow`（U+FF01–U+FF5E → ASCII、U+3000 → 半形空白），
實作於 `BelieverWriteValidator` / `CreateSignupHandler` / `UpdateSignupHandler` / `InsertShiftSignupHandler`。

**2026-08-06 追加（使用者指定「電話不管全形半形輸入，結果都是半形」）**——把同一規則往前後兩端延伸：

- **輸入當下就轉**：電話欄套 `appNarrowInput` directive（`shared/directives/narrow-input.directive.ts`），
  IME 組字結束（`compositionend`）或直接輸入時即轉半形，使用者當場看得到結果，不必等存檔。
  套用處：信眾表單電話、報名表單電話、信眾搜尋電話。
  游標位置在轉換後原樣還原（全/半形 1:1 對應、長度不變）。
- **搜尋條件也轉**：`SearchBelieversHandler` 對 `Phone` 條件做 `ToNarrow`。
  理由——寫入端一律存半形，條件若留全形（或舊 client 直接打 API）永遠撈不到資料。
- **只轉電話**：姓名／堂號／地址／名單**不轉**。全形空格是資料本身（使用者用開頭全形空格把名字往下推做直書排版，
  見 §「姓名中間空格」與 [gotchas.md](gotchas.md)），轉了會破壞套印版面。
- **載入既有資料不轉顯示值**：directive 的 `writeValue` 原樣顯示。轉了會讓 DOM 與 control 值不一致，
  而在 `writeValue` 內回呼 `onChange` 會把剛載入的表單標成 dirty（觸發「未儲存」提示）。
  舊資料若殘留全形，使用者一動就轉，沒動也有後端 `ToNarrow` 兜底。

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

**舊系統行為**（`NewSignupForm.cs:477-502`、`EditSignupForm.cs:159-184`、`BelieverForm.cs:294-318`）：

- 勾選：複製 Mail 至 Text；**`txtMailAddress` 為空**時阻止勾選並彈 `"請先輸入寄件地址"`
- 取消：清空 Text 區（City/Zone/Address 全還原為 placeholder）

實作細節：用 `SelectedIndex` 複製（兩個 City list 順序相同因 query 一樣，但脆弱）。

> **新版偏離（2026-07-21 客訴）：地址改非必填**。舊系統寄件地址為必填（空白擋下）；新版依使用者指定改為**非必填**——前端 `mailAddress` 移除 `Validators.required`，後端 `CreateSignupHandler` / `UpdateSignupHandler` / `InsertShiftSignupHandler` 與 `BelieverWriteValidator` 皆不再擋空，空白 normalize 為空字串照常寫入（含「未選信眾自動建立」路徑）。

> **新版偏離（2026-07-31 客訴）：勾選門檻放寬為「城市/區域/地址三者全空才擋」**。取代 2026-07-21 記錄的「同寄件地址仍要求先有寄件地址」——地址既然非必填，「只選了城市與區域、地址欄留空」就是合法狀態，這時同步城市/區域一樣有意義，硬擋反而讓使用者得先亂打一個字。提示文字同步改為 `"請先填寫寄件地址（城市／區域或地址）"`（舊字串 `"請先輸入寄件地址"` 不再使用）。複製內容不變（城市 + 郵遞區號 FK + 地址一起帶，地址為空就帶空字串）。兩張表單（`signup-edit-form` / `believer-edit-form`）同步。
>
> ✅ **已補齊（2026-07-31）**：`believer-edit-form` 的 `mailAddress` 曾遺留 `Validators.required`（2026-07-21
> 的放寬只做了報名表單與後端），使用者無法刪掉信眾既有的寄件地址；該 required 已移除，placeholder 的
> `*` 一併拿掉，兩張表單與後端至此一致。

---

## 12.1 地址／堂號「清空」必須清得掉（2026-07-31 客訴，**新版刻意偏離 legacy**）

使用者要求：新增或修改時，**既有的寄件地址與文牒地址要能整段刪掉**（含城市與郵遞區號，可不留地址），
**堂號同理**。三個各自獨立的障礙，全部移除：

| 障礙 | 舊/原行為 | 新版 |
|---|---|---|
| 信眾表單寄件地址必填 | 前端 `Validators.required` 擋下送出（後端 2026-07-21 起已允許空） | 移除 required |
| 文牒地址／區號的隱性 fallback | `NewSignupForm.cs:244-251`、`EditSignupForm.cs:255-267`：文牒欄為空就抄寄件段 → 清空存檔後又長回來 | **移除**：空就是空（存 `null`） |
| 報名堂號存 `null` 被回退 | `SignupView.HallName = COALESCE(S.HallName, B.HallName)`，清空存 null → 顯示信眾堂號 | 清空存**空字串**（`SignupHallName.Normalize`；前端一律送 `""`） |

- 「印疏文用同寄件地址」的需求改由**「同寄件地址」checkbox** 承擔（見 §12）：勾選時把值**實際填進**
  文牒欄，所見即所存；比隱性 fallback 明確，也才可能「不勾＝真的沒有文牒地址」。
- 城市／區域不是獨立欄位，是 `MailZipcodeID`/`TextZipcodeID` join `Zipcodes` 得來——把區域下拉選回
  「請選擇區域」（FK → `null`）就整段消失，郵遞區號文字快照也跟著清掉。
- 堂號的空字串哨兵**不影響並行期舊系統**：舊系統寫入的列 `S.HallName` 仍為 `null` → 照舊回退信眾堂號；
  它讀 view 拿到空字串也就是顯示空白。
- 信眾主檔堂號清空**不連動既有報名**（報名自持快照，見 §3.1）。
- 回歸鎖：`SignupsEndpointsTests.Clearing_hallName_and_addresses_actually_persists`、
  `BelieversEndpointsTests.PUT_can_clear_existing_addresses_and_hallName`、
  `UpdateSignupHandlerTests.Edit_cleared_*`、`CreateSignupHandlerTests.Empty_text_address_stays_empty_*`。

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

## 16. 「列印普桌 / 列印普桌資料卡」完全解鎖（2026-07-18：前端恆啟用＋後端不限型別）

- SignupForm 右鍵 menu「列印普桌」「列印普桌資料卡」：**與其他列印選項完全一致——有選取列即 enabled、選什麼印什麼**，前後端都不檢查 `SignupType`
- 考據（本次解鎖的依據）：舊系統 `tsmiPrintWorship_Click`（SignupForm.cs:380-403）**沒有任何型別檢查**，批次 case 5 也只跟隨搜尋篩選；原新系統的「限 type-4」防呆（單筆 422 `WORSHIP_ONLY_TYPE_4`、批次強制/過濾 type=4）是「比舊系統嚴格」的自加限制，實務上造成客訴（右鍵被鎖、單選非普桌 422），2026-07-18 全數撤回
- 現行為：
  - 單選走 `GET /reports/worship(card)?signupId=`：任何型別都回 200 PDF
  - 多選走 `POST /reports/batch`（signupIds 模式）：勾什麼印什麼，不過濾
  - 編號區間批次面板：只跟隨呼叫端傳入的 `signupType` 篩選（＝畫面上的搜尋篩選），與舊系統 case 5 相同
- 非普桌資料印普桌的版面後果由使用者自行判斷（同舊系統）：陽上名單為空時印出只有編號的牌位

> **適用範圍限「使用者明示選擇」的入口（2026-08-12 釐清）**：本條管的是列表右鍵 menu 與列印預覽頁下拉——
> 使用者自己指定了要哪一種報表，系統就照印、不得代為否決。
> **新增報名表單的「列印資料卡」鈕不在此列**：它沒有給使用者選卡別，屬**系統代選**，
> 故依報名類型自動分流（普桌→普桌資料卡，其餘→一般資料卡），見下方 §16.1。
> 兩者不衝突：一個是「別擋使用者的明示選擇」，一個是「沒得選時要選對」。

> **演進史**：
> 1. 最初「僅當搜尋篩選 `signupType == 4` 才 enabled」（舊系統原始行為，SignupForm.cs:138-145）
> 2. 2026-06-29 前端改看實際選取列：`selected.every(signupType === 4)` 才 enable，混選 grey out + tooltip
> 3. 2026-07-18 客訴「常被鎖」→ 前端恆啟用＋後端撤回全部 type-4 限制，回歸舊系統「選什麼印什麼」

### 16.1 新增報名「列印資料卡」依報名類型自動挑卡（2026-08-12 使用者定案，**新版加值規則**）

- 新增報名表單存檔後的「列印資料卡」鈕，依**剛新增那一筆**的 `SignupType` 決定送印哪一種：

  | SignupType | 送印報表 | 按鈕文字 |
  |---|---|---|
  | 4（普桌） | `worshipcard` 普桌資料卡 | 列印普桌資料卡 |
  | 1 / 2 / 3 / 5 | `datacard` 一般資料卡 | 列印資料卡 |

- **判斷來源＝存檔當下的類型快照，不是下拉的即時值**：存檔後表單不關閉、資料仍留在畫面上
  （見 [signup-management.md](blueprints/signup-management.md)「新增成功後不清表單」），使用者可能改了「報名類型」
  卻沒再按確認；這顆鈕印的是**已存的那一筆**，用即時值會印出與該筆記錄不符的版面。
  快照存於 `lastCreatedSignupType`，並隨跨路由草稿一起保存（切走再回來按鈕仍是普桌資料卡）；
  按「取消」（清成新的一筆）一併歸零。
- **按鈕文字跟著卡別走**：使用者按下去之前就知道會印哪一張（兩張紙同為 21×14.8cm，印錯不易當場察覺）。
- **舊系統無此規則**：舊 `btnPrintDataCard_Click`（NewSignupForm.cs:371-398）永遠 `tmpDataCard.rdlc`，
  因為舊系統根本沒有「普桌資料卡」這張報表（worshipcard 為 2026-07-04 新增的複合報表，`legacy_form: N/A`）。
  故此為新版加值、非反推規則。
- ⚠ 兩張卡紙張同為 21×14.8cm 但**驅動 form 名不同**（「資料卡」／「普桌資料卡」），
  自動選紙不可互相替代（見 `PrinterFormMatcher.cs` 與 [printing-reports.md](blueprints/printing-reports.md) §6）。

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

---

## 18. 薦牌／文牒列印「第 6 位往生/陽上」必印滿（**新版修正 legacy 缺陷**，2026-06-30 定案）

> ⚠ 此為新系統**刻意偏離 legacy**：舊系統第 6 位往生/陽上**可登錄、可存 DB、可搜尋，但列印時被默默丟掉**。新版要求**印滿 6 個**。

**Legacy 行為（缺陷）：**
- DB（`Signups.DeadNameSix` / `LivingNameSix`）、輸入表單（`txtDeadNameSix` 等，**未** disable）、列印 ViewModel（[SignupForm.cs:316](../reference/old/Ceremony/SignupForm.cs#L316) / [372](../reference/old/Ceremony/SignupForm.cs#L372)）**都有**第 6 欄。
- 但 RDLC 報表版面只宣告 dataset `<Field>`、**沒有第 6 格 textbox** → `=Fields!DeadNameSix.Value` 從未渲染。薦牌（`tmpTablet*`）、文牒（`tmpText*`）**全部 11 個變體皆只印 1–5**。唯一印第 6 的是普桌 `tmpWorship`（`LivingNameSix`）。
- 屬隱性缺陷（無提示），非刻意 disable。

**新版決議：薦牌、文牒列印一律印滿 6 位往生 + 6 位陽上。**

**狀態：✅ 已實作（2026-06-30）。** `TabletRenderer`（往生 default case + 陽上 Two/One/Base 三變體）與 `TextRenderer`（往生 tmpText + 陽上 inline）共 4 組補上 `d[5]`/`l[5]` 繪製：座標補矩陣空位、納入 `GroupFontPt` 統一字級分組（主名 `Avail` 改看第 6 位 → 有第 6 位才縮、無則行為與舊版完全相同＝向後相容）。回歸鎖：`RendererSmokeTests` 以「只填第 6 位 vs 全空」比 PDF 大小，隔離 `[5]` 是否真渲染（legacy 靜默丟字時兩者相等 → 失敗）。pdftotext + pdftoppm 影像驗證薦牌/文牒往生+陽上各 6 位全印、第 6 位落矩陣空位。**仍待**：實機預印紙對位驗收（±0.2cm）。

**原始缺陷（背景）：** 新 QuestPDF renderer 原從 legacy RDLC 1:1 抽座標，**沿用了同一缺陷** — [TabletRenderer.cs:100-104](../backend/src/Ceremony.Infrastructure/Reporting/TabletRenderer.cs#L100-L104)（往生）/ [110-188](../backend/src/Ceremony.Infrastructure/Reporting/TabletRenderer.cs#L110-L188)（陽上）與 [TextRenderer.cs:54-58](../backend/src/Ceremony.Infrastructure/Reporting/TextRenderer.cs#L54-L58)（陽上）/ [101-105](../backend/src/Ceremony.Infrastructure/Reporting/TextRenderer.cs#L101-L105)（往生）只畫 `d[0..4]` / `l[0..4]`，`d[5]`/`l[5]` 永不繪製（data 陣列已是 6 元素，只差座標與分組）。**薦牌、文牒各有往生 + 陽上兩組，共 4 組要補。**

**第 6 格座標（已確認，2026-06-30 Tim 拍板）：** legacy 無任何來源（已查證：薦牌 9 + 文牒 2 共 11 個變體，`Fields!DeadNameSix.Value` / `LivingNameSix.Value` 皆未綁定 textbox，最大渲染數恆為 5；`...Six` 只在 DataSet `<Fields>` 宣告，是孤兒欄位）。各組現為 2×3 缺一角，第 6 格**補進該空位使矩陣對稱**，座標如下（cm，直書，origin = 左上）：

| 報表 | 區 | Top | Left | 格寬 | 對齊依據 |
|---|---|---|---|---|---|
| 薦牌 tmpTablet | 往生6 | **9.4464** | **4.9** | 0.6 | Top 對齊往生下排 d4/d5、Left 對齊主名 d1 |
| 薦牌 tmpTablet | 陽上6 | **15.44174** | **1.56167** | 0.7 | Top 對齊陽上下排 l4/l5、Left 對齊主名 l1 |
| 文牒 tmpText | 往生6 | **5.72264** | **12.41251** | 0.91251 | 同上邏輯 |
| 文牒 tmpText | 陽上6 | **17.25916** | **21.87382** | 0.91251 | 同上邏輯 |

> 視覺確認圖（按真實 cm 比例，紅色虛線為第 6 格）：[reference/diagrams/tablet-text-sixth-name-position.png](../reference/diagrams/tablet-text-sixth-name-position.png)（向量原圖 `.svg` + 產圖腳本 `.draw.js` 同目錄，座標若調整重跑即可）。

> **僅基本變體出現第 6 位**：其他變體（`tmpTabletOne/Two/_One…`、`tmpTextTwo`）本就是 1–2 位往生的少格版，不會有第 6 位，只需改 `tmpTablet`(基本) 與 `tmpText`(基本)。**實機對位仍建議印一張驗收**（預印紙 ±0.2cm；見 [printing-reports.md](blueprints/printing-reports.md) 預印對位段）。第 6 位字級／列距比照同排次要格(4/5)納入 `GroupFontPt` 分組。

---

## 19. 預繳依「單筆報名」隔離 — 法會預繳 ≠ 普桌預繳（**新版修正 legacy 缺陷**，2026-07-31 客訴）

> ⚠ 此為新系統**刻意偏離 legacy**：舊 `BelieverSelected` 的預繳反查完全不分報名類型，會把法會的預繳帶到普桌。

**資料模型前提：** 預繳**沒有獨立資料表、也沒有金額欄位**，只有掛在單筆 `Signups` 上的兩欄 `PrepayYear` + `PrepayCeremonyCategoryID`（見 [glossary.md §預繳](glossary.md)）。也就是說**預繳是「某一筆報名」的快照屬性，不是信眾層級的餘額**。

**業務前提（2026-07-31 使用者確認）：** 法會（`SignupType` 1 一般／2 寺方／3 觀音會／5 郵寄）與**普桌（`SignupType` 4）是分開報名的兩件事**，同一位信眾的法會預繳**不等於**普桌預繳。

**Legacy 行為（缺陷）：** [NewSignupForm.cs:1102-1115](../reference/old/Ceremony/NewSignupForm.cs#L1102-L1115) 選信眾後查「該信眾今年(含)以前最新一筆報名」的預繳並帶入：

```csharp
.Where(a => a.BelieverID == BelieverID && a.Year <= Y)
.OrderByDescending(o => o.Year).ThenByDescending(o => o.CeremonyCategorys.Sort)
```

**沒有任何 `SignupType` 條件** → 同一信眾同時有法會與普桌報名時，點哪一列都拿到同一份（＝最新那筆，通常是法會），普桌明明沒預繳卻顯示有預繳。

**新版決議：選信眾（`pickBeliever`）時，預繳一律取「點到的那一列自身」的 `prepayYear` / `prepayCeremonyCategoryId`，該列沒預繳就是空白。**

- 不再呼叫 `GET /api/v1/prepay?believerId&year`（該 endpoint **保留備用但已無呼叫端**，見 [get-prepay-believer-latest.md](blueprints/api-endpoints/get-prepay-believer-latest.md)）。
- 這也讓 `pickBeliever` 內部一致：姓名／電話／地址／名單／備註／堂號／員工類型自 2026-07-27 起全都是「帶該筆報名自身快照」，預繳原是搜尋列表還只列信眾主檔時代的遺留。
- **不受影響**：編輯既有報名（`applyItem`）本就取該筆自身值；`resetBelow`（按「取消」）仍清空預繳；批次「載入預繳」（`POST /api/v1/prepay/load`）本來就有 `SignupType` 過濾（且 6 個分組不含普桌，見 [prepay-loading.md](blueprints/prepay-loading.md)）。

**狀態：✅ 已實作（2026-07-31）。** [signup-edit-form.component.ts](../frontend/src/app/features/signups/signup-edit-form.component.ts) `pickBeliever` 改帶 `row.prepayYear` / `row.prepayCeremonyCategoryId ?? ''`，刪除 `prefillPrepayHistory`。回歸鎖：`signup-edit-form.component.spec.ts`「改選：法會列的預繳不會殘留到普桌列」（先點法會列 prepayYear=121 → 再點普桌列 prepayYear=null → 斷言清空且無 `/prepay` 請求）；已用「暫時改回 `prepayYear: null` → 轉紅」驗證有效。

> **2026-08-05 補齊漏網路徑**：代入新增（`prefillFromSignup`）當時仍走自己那份 `patchValue`，**完全沒帶預繳**——同一條「從一筆報名帶資料」的規則在兩條路徑上不一致。現已讓代入新增改走與 `pickBeliever` 共用的 `applyBelieverRow`，找不到來源列時的 fallback（`applyPrefillItem`）也補上這兩欄。回歸鎖：「來源報名的預繳要跟著帶」。

## 20. 「代入新增」的選列規則與來源列 pin（2026-08-05，**新版刻意偏離 legacy**）

**Legacy 行為：** [SignupForm.cs:76-90](../reference/old/Ceremony/SignupForm.cs#L76-L90) `btnNew_Click` 與 [SignupForm.cs:151-166](../reference/old/Ceremony/SignupForm.cs#L151-L166) `tsmiAdd_Click` 是逐行重複的同一段——`selectedcount > 0` 就取 `SelectedRows[0]` 的 `SignupID` + `Name` 帶進 `NewSignupForm`（＝代入新增）。

**新版兩處偏離：**

1. **恰好選 1 筆才代入**（而非 `> 0` 取第一列）。舊系統的清單以單選為主，新系統多選是常態——批次列印動輒選數百列，此時沿用「取第一列」等於隨機拿一筆代入，使用者無從預期。故工具列鈕與右鍵「代入新增」統一為 `selectedRows.length === 1`；沒選或選多筆＝空白新增。
2. **來源列 pin 到搜尋結果最前**。代入後會自動以來源姓名跑一次信眾搜尋並選中來源列（對齊舊 `btnNextStep_Click:97-111`），但新版結果列表有 `MAX_BELIEVER_RESULT_ROWS`(200) 的 DOM 保護上限（舊 WinForms grid 無此限制），同名者多時來源列可能被切掉 → 沒有列可高亮。故 `runBelieverSearch` 收 `pinSignupId`，來源列不在前 200 內時把它 `unshift` 到最前（破序）。

**連帶取捨：** 工具列「選 1 筆按新增」自此屬代入模式 → 該次**不還原跨路由草稿**（草稿不會被覆寫，清掉選取再按新增即可拿回）。見 [signup-management.md](blueprints/signup-management.md)。
