---
title: 報名管理（搜尋、新增、編輯、歷程）
purpose: 報名核心業務 — 對應舊 SignupForm / NewSignupForm / EditSignupForm / SignupLogForm
status: draft
applicable_when: 要修改報名欄位、調整搜尋邏輯、調整編號生成、修改變更歷程
related_agents:
  - software-architect-blueprint
  - backend-engineer
  - frontend-architect
related_docs:
  - ../design/database-design.md
  - ../design/api-design.md
  - ../design/backend-design.md
  - ../design/visual-design.md
  - ../design/performance.md
  - prepay-loading.md
  - printing-reports.md
keywords: [signup, 報名, 報名維護, 編號, NumberTitle, 避4, PredicateBuilder, SignupForm, context-menu, 右鍵, 多選, 批次列印, 勾選列印, signupIds]
last_updated: 2026-07-31 (新增報名表單客訴五項〔第四輪〕：①地址欄位標題全拿掉、寄件/文牒**各一個 fieldset 外框**、「同寄件地址」勾選移進文牒框內〔**部分反轉 07-29 的「全部拿掉外框」**〕②往生/陽上名單各自框起來，解掉「填字後失去標示」取捨 ③基本資料改序 堂號→姓名→電話→員工類型→固定編號、前三欄與地址框逐像素等寬 ④**勾了「指定編號」不再被改選信眾清掉**〔勾選與數字都保留；與費用同一取捨，也回到舊 BelieverSelected 未碰 cbKeepNumber 的行為，resetBelow 仍清〕⑤「同寄件地址」放寬為「城市/區域/地址三者全空才擋」〔刻意偏離舊系統，提示改「請先填寫寄件地址（城市／區域或地址）」〕；信眾表單同步 ①⑤。ng build 0 warning、ng test 31 綠〔新增 3 案回歸鎖〕，版面待實機複驗。先前 2026-07-29 (報名維護清單客訴三項：法會欄預設寬度 100→64px〔＝三個字〕；「顯示完整表格」取消 localStorage 記憶、每次開頁一律不勾；「全部」語意收斂為**只解除年份/法會/類型**〔＋年份修飾條件「範圍」〕，關鍵字/範圍 5 項/編號/固定編號在全部模式下仍可改、仍可按搜尋，取消勾選回到原本三個條件。先前 2026-07-28 搜尋面板「姓名/陽上/往生/電話/備註」5 個 checkbox 預設改全勾（關鍵字欄一開始就可輸入；刻意偏離舊系統全不勾）；同日先前新增/編輯報名 overlay 改「只有 × 或取消才關」——form-overlay 新增 dismissible input，報名維護給 false 停用 backdrop click 與 Esc；同日先前新增報名草稿改「一律快照」：離開時無條件存當下畫面、不再有清除時機——拿掉 saveDraft 的 form.dirty 門檻、submit 成功與 resetBelow 不再 clear()（SignupDraftState.clear 移除），草稿加存 lastCreatedSignupId 與 dirty 旗標，補上原本兩個破口〔存檔成功後／按取消後畫面有東西但表單 pristine，切走再回來會不見〕；同日先前按「取消」（清成新的一筆）不再清空「費用」——同場法會連續輸入金額多半固定，與「改選信眾不清費用」同一取捨；resetBelow 的 patchValue 拿掉 fee: null，回歸鎖 1 案；同日先前新增報名 Enter 不送出（移除隱藏 submit 鈕＋(ngSubmit)，另加 form 層 Enter 攔截、textarea 放行；回歸鎖 1 案）＋成功提示 dialog 內文放大到 --font-size-md（＝側欄選單字級，改共用 .confirm-body）；同日先前版面客訴第二輪四項：「同寄件地址」改夾在寄件段與文牒段中間（自成一列、靠右對齊）、文牒郵遞區號回到文牒區域右邊、文牒地址加寬佔滿三欄、「列印資料卡/取消/確認」移到備註下方〔表單新增 form-actions 投影 slot，overlay 端 [showActions]=false 收掉 footer，路由頁 .form-actions 移除〕；同日先前四項：信眾搜尋框字級對齊地址輸入框〔.search-input 不在 .field 內、只繼承 font-family，字級仍是 UA 預設 13.33px〕、往生/陽上名單由右欄移到左欄「地址」下方、「同寄件地址」由文牒地址同列移到文牒**郵遞區號正上方**〔第一列第三欄，郵遞區號下移到文牒地址右側，不多佔列高〕、備註 textarea rows=1→4〔約再加兩行〕；先前 2026-07-27 清單列選取補齊 shift 範圍選取：一般點擊 toggle 並設錨點、shift 點擊選錨點~本列整段，錨點在 shift 期間不動且以「錨點當下選取」為基準重算故範圍可縮小、更早的選取不被吃掉；列首 checkbox 從 (change) 改綁 (click) 才拿得到 shiftKey〔change 事件無此旗標，原本從 checkbox 點永遠吃不到 shift〕、mousedown preventDefault 擋掉 shift 文字反白；新增 signup-list-page.spec.ts 7 案回歸鎖；同日先前搜尋面板新增「全部」checkbox〔置於「範圍」上方〕：勾選＝忽略所有條件顯示全部報名供比對、條件值保留但停用變灰，取消勾選即以原條件重查還原；匯出 Excel 同步跟著模式走；isAll + searchedBeforeAll 併入 SignupSearchState 跨路由保存——取代舊系統「另開一個 SignupForm 視窗並排比對」的做法；同日先前補回「列印資料卡」按鈕（存檔前 disabled、新增成功後啟用，印剛新增那筆，對齊舊 btnPrintDataCard）＋報名維護 overlay 固定 1100px＝對齊新增報名頁 .page max-width（form-overlay 新增 width input；19 欄結果表原本把 panel 撐到 92vw）；同日版面續調三項：重複報名警示移到左欄法會資料下方、編號 disabled 補灰底（全域 .field 補 :disabled 樣式）、信眾結果高度改表頭+4 列；同日先前新增報名版面三項：法會資料改放左側直立窄欄（回舊 plStep1）、信眾搜尋結果高度縮為表頭+3 列、編號欄恆顯示改以 disabled/enabled 切換（回舊 cbKeepNumber_CheckedChanged）；同日往生/陽上名單 input 文字色/底色對齊地址欄（.field 外吃不到全域 color，UA fieldtext 純黑），報名+信眾兩表單同步；同日改選信眾不再清空「費用」（使用者輸入的金額保留，對齊舊 BelieverSelected 未碰 txtFee）；同日新增成功後不清表單/不關閉、跳單按鈕結果 dialog「編號X，新增報名成功」、草稿記憶清掉——saved output 改帶 SignupSavedEvent{keepOpen}，對齊舊 NewSignupForm:355-361；同日先前改選信眾 in-flight race guard〔pickToken/isStale，慢回應不再蓋掉後選的、地址區域下拉不再錯亂〕＋選信眾即標髒〔沒打字也會存草稿〕＋改選時清錯誤訊息；同日新增報名跨路由草稿：填到一半切到其他功能頁再回來資料保留——root singleton SignupDraftState，僅純新增模式、僅存記憶體、靜默還原，儲存成功/按取消才作廢，純新增模式關閉 overlay 不再跳「未儲存的變更」確認；同日先前信眾搜尋補回舊 BelieverView 語意：併查 GET /believers?searchKey=（既有端點新加 14 欄 OR 參數）補「從未報名過的信眾」列，排在報名列後、獨立額度 50 列；同日先前「信眾搜尋選定回填」客訴修正：清單一列＝一筆報名，點列改帶「該筆報名」的姓名/電話/寄件+文牒地址/往生+陽上名單/備註，信眾主檔 `GET /believers/{id}` 降為欄位為空時的 fallback 與摘要卡資料源，對齊舊 BelieverSelected:991-1101；年份/法會/類型/編號/費用仍不帶、費用清空、預繳仍走 prefillPrepayHistory 條件回填；同日先前 2026-07-21 報名維護 UI 客訴四項：編號＋批次列印起迄加 .num-stepper ▲▼ ±1（對齊舊 NumericUpDown）、編輯表單「預繳民國年」移到下一行且置於「預繳法會」之前、搜尋/列印按鈕補 align-self:stretch 撐滿列高（對齊舊 btnSearch 75×99 / btnPrint 75×63）、清單垂直捲軸右鍵子選單〔捲動到這裡/頂端/底部/上一頁/下一頁/向上/向下捲動〕對齊舊 WinForms 原生捲軸選單；同日先前：員工類型/固定編號/堂號改 per-signup 可編輯（方案 A）：Signups 加自有欄＋DbUp 回填＋SignupView COALESCE，報名表單三欄可編輯只改這筆、不回寫信眾、預繳保號仍讀信眾；同日新增報名頁客訴六項：取消＝清成新的一筆不跳頁、改選信眾殘留欄位修復、搜尋結果與名單文字大小對齊地址、勾指定編號後編號欄移至勾選文字右邊、地址非必填（前後端同步放寬）；先前 2026-07-18 右鍵「列印普桌／普桌資料卡」解鎖：前端不再檢查選取列型別、恆啟用，防呆交後端過濾/驗證；2026-07-17 新增報名表單對齊舊系統四項：信眾搜尋改常駐 in-form 結果列表、地址寄件上/文牒下、名單往生上/陽上下且無底色、未選信眾自動先建新信眾（前端 POST /believers orchestration）))
---

## 背景與動機

報名是系統核心，舊 SignupForm 超過 1900 行；含 40 欄 grid、AND/OR PredicateBuilder 搜尋、多選列印、批次範圍列印、Excel 匯出、新增/編輯/歷程子頁面。新版必須完整重現業務邏輯與介面編排，並修正三段 SaveChanges 非原子、編號 race condition 等問題。

## 範圍

### 做什麼
- 報名搜尋（年份 / 法會 / 類型 / 編號 / 關鍵字 + 5 範圍 checkbox〔姓名/陽上/往生/電話/備註〕+ 固定編號）
- 報名 CRUD：新增（兩步驟）/ 編輯 / 刪除（多選） / 歷程
- 編號生成（GetSignupNumber）+ 避 4 顯示（GetNumberText）
- NumberTitle 由 SignupType 自動推導（不可手動）
- 信眾資料帶入優先順序（Signup → grid row → Believer）
- 預繳資料自動帶入（「今年以前最新報名」）
- 地址 fallback（疏文 ← 寄件）
- Excel 匯出（32 欄）
- 批次列印（編號範圍 + 5 種報表）

### 不做什麼
- 報名線上付款（業務未要求）
- 報名通知信 / 簡訊（未來）
- 信眾自助報名（內部使用）

## 使用者流程

### 搜尋（SignupForm 對應）

```
1. 進入 /signups
2. 篩選區：年份 / 範圍勾選 / 法會 / 類型 / 編號 / 關鍵字
   勾選任一搜尋欄位（姓名/陽上/往生/電話/備註）才能填關鍵字——這 5 項**預設全勾**（見下方段落）
3. 「搜尋」→ 後端套 PredicateBuilder AND/OR
3b.「全部」勾選 → 解除年份/法會/類型限制（其餘條件仍生效、仍可按搜尋）；取消勾選還原原本三個條件
4. DataGrid 顯示 40 欄（預設 32 顯示）
5. 「顯示完整表格」勾選顯示額外 5 欄
6. 列首 checkbox 多選（點列或點 checkbox 皆可）；一般點擊＝toggle 並設錨點，shift-click＝選取「錨點 ~ 本列」整段
7. 右鍵（或長按 / 列尾「…」按鈕）開 context menu — **9 項，依選取狀態與 SignupType filter 啟用 / 停用**
8. 上方批次列印面板：起~迄編號 + reportType → 「列印」（呼叫 POST /reports/batch）
9. 「匯出Excel」→ 下載 yyyyMMddHHmmss.xlsx
```

#### 關鍵字範圍 5 項預設全勾（2026-07-28 使用者指定，**刻意不同於舊系統**）

- 姓名 / 陽上 / 往生 / 電話 / 備註 五個 checkbox 的初值由全不勾改為**全勾**（`signup-list-page.ts` form 預設值 + `resetForm()` 的 reset 值兩處都要改），關鍵字欄因此**一開始就可以輸入**（`searchKey` 不再以 `disabled` 起手、`keyEnabled` 初值改 `true`）。
- 舊系統 [SignupForm.Designer.cs](../../reference/old/Ceremony/SignupForm.Designer.cs) 這 5 個 `cbSearch*` 皆未設 `Checked`（＝預設全不勾，必須先勾欄位才能打關鍵字）。這裡刻意偏離：實務上使用者九成是「打個名字/電話找人」，先勾欄位是多一道手續；全勾＝打什麼都找得到，要縮小範圍再自己取消。
- 連動規則不變：勾選歸零時關鍵字欄仍會停用並清空（`bindScopeKeyToggle`）；「全部」模式下這 5 項**仍可用**（2026-07-29 起，見下段）。跨路由還原（`SignupSearchState`）讀的是快照值，不受預設值影響。

#### 「全部」checkbox（新版增強，2026-07-27，無舊對應）

- **需求**：使用者要「額外顯示全部的資料，提供比對用」——手上留著一組搜尋條件，想暫時看全部資料對照，再切回原條件。舊系統的做法是從主選單再開一個 `SignupForm` 視窗（modeless `Show()`，見 [MainForm.cs:60-70](../../reference/old/Ceremony/MainForm.cs#L60-L70)）並排比對；新版是 SPA 單一 `/signups` 路由，改用同頁的模式切換達成同一目的。
- **位置**：搜尋面板左側 checkbox 欄最上方（`全部` / `範圍` / `顯示完整表格` 由上而下），沿用既有三列 grid、**不增加面板高度**。
- **行為**（2026-07-29 客訴修正後：全部＝**只解除三個範圍限制**，不是「忽略所有條件」）：
  - 勾選 → 立即重查，查詢中把 `year` / `ceremonyCategoryId` / `signupType` 放掉（送 null / -1），**其餘條件照送**；被停用的控制項只有這三個＋`isScope`（`isScope` 是年份的修飾條件 `Year >=`，年份鎖住時無作用故一併停用），值一律**保留、不清空**（原生變灰＋對應 label 淡化 0.5）
  - 全部模式下**仍可搜尋**：關鍵字、範圍 5 項、編號、固定編號都還能改、按「搜尋」即以「全年份/全法會/全類型 + 這些條件」重查（例：想跨年份找某個人名）
  - 取消勾選 → 立即以保留下來的年份/法會/類型重查還原；若進入全部模式前根本沒搜尋過，則回到「請設定搜尋條件後點『搜尋』」的空狀態，不憑空跑一次無年份/法會/類型的查詢
  - 「匯出 Excel」跟著模式走（與搜尋共用 `buildQuery()`，兩條路徑一致）
  - `顯示完整表格` 不受影響（欄位顯隱與搜尋條件正交，全部模式下仍可自由切換）
- **狀態保存**：`isAll` 併入 `SignupSearchFormSnapshot`，另存 `searchedBeforeAll` 於 `SignupSearchState` → 進 edit / logs 頁再返回時模式與條件一併還原；stale 重查也會沿用全部模式。
- **取捨**：條件選擇「停用」而非「清空」，是需求明寫的「原本搜尋條件不能清掉」；停用（而非放著可編輯）則是為了讓「條件還在、但此刻不生效」一眼可辨，避免使用者以為改了條件卻沒反應。停用範圍 2026-07-29 由「全部條件」收斂為「年份/法會/類型（＋範圍）」——原設計把全部當成「純比對快照」，實際使用是「拿掉年份/法會/類型的框，再在全部資料裡找人」，條件全鎖等於逼使用者取消勾選才能搜、搜完又得重勾。
- ⚠ **無筆數上限**：全部模式（且未給其他條件時）會拉回整張 `SignupView`（`SearchAsync` 沒有 TOP，與舊系統一致）。前端靠 `cdk-virtual-scroll-viewport` 撐住渲染，但傳輸量隨資料成長。若日後實測過慢，處理方向見 [performance.md](../design/performance.md) §2（server-side 分頁），**不要**改成靜默截斷。

#### Grid Context Menu（cmsSignups 等價，**新版重現**）

舊 [SignupForm.Designer.cs:236-313](../../reference/old/Ceremony/SignupForm.Designer.cs#L236-L313) 9 個 `ToolStripMenuItem`，由 [SignupForm.cs:121-149](../../reference/old/Ceremony/SignupForm.cs#L121-L149) `dgvSignups_RowHeaderMouseClick` 觸發（`MouseButtons.Right` + 選中該列）。

| # | 舊 label | 對應 handler / endpoint | 選列規則 | SignupType 規則 | 備註 |
|---|---|---|---|---|---|
| 1 | 代入新增 | navigate `/signups/new?fromSignupId=:id`（前端帶 query 預填信眾） | **單選**（==1 才 enable） | 全部 | 對應 `tsmiAdd_Click` (cs:151)：把選列的 SignupID + Name 帶入 NewSignupForm |
| 1b | **在此前插入**（新版增強，無舊對應） | overlay signup-edit-form 插入模式 → `POST /api/v1/signups/insert-shift` | 觸發列（右鍵那列）有編號才 enable | 全部 | 於觸發列編號位置插入一筆新報名，該群組內 Number ≥ 此編號的既有報名 +1 順移。主要用於**預繳載入後補插**。見下方「插入並順移」段 |
| 2 | 修改資料 | navigate `/signups/:id/edit` | **單選** | 全部 | 對應 `tsmiEdit_Click` (cs:168)：未選列 verbatim「尚未選擇報名資料」 |
| 3 | 列印資料卡 | `GET /api/v1/reports/datacard?signupId=` ×N（或前端拼批次） | 單 / 多選皆可 | 全部 | 對應 `tsmiPrintDataCard_Click` (cs:188) |
| 4 | 列印收據 | `GET /api/v1/reports/receipt?signupId=` | 單 / 多選 | 全部 | `tsmiPrintReceipt_Click` (cs:242) |
| 5 | 列印薦牌 | `GET /api/v1/reports/tablet?signupId=` | 單 / 多選 | 全部 | `tsmiPrintTablet_Click` (cs:273)；含 HallName 拆字邏輯（前端不重作，後端已處理） |
| 6 | 列印文牒 | `GET /api/v1/reports/text?signupId=` | 單 / 多選 | 全部 | `tsmiPrintText_Click` (cs:323) |
| 7 | 列印普桌 / 普桌資料卡 | `GET /api/v1/reports/worship?signupId=` | 單 / 多選 | **完全不鎖型別**（2026-07-18 客訴改）：前端恆啟用、後端選什麼印什麼 | `tsmiPrintWorship_Click` (cs:380) 本就無型別檢查；原新系統「限 type-4」自加防呆（422 `WORSHIP_ONLY_TYPE_4`／批次過濾）已全數撤回 |
| 8 | 刪除資料 | `DELETE /api/v1/signups/:id` ×N | 單 / 多選 | 全部 | `tsmiDelete_Click` (cs:405)；多選逐筆呼叫；需二次確認 dialog |
| 9 | 瀏覽歷程 | navigate `/signups/:id/logs` | **單選** | 全部 | `tsmiLog_Click` (cs:428) |

**選列邏輯**：
- 未選 (count == 0)：除 3–7（多選列印）走「印出當前篩選結果」可選擇 disable 全部以維持舊行為；採後者，**未選列右鍵不開選單**或開但全部 disable（取後者，使用者察覺自己沒選到）
- 單選 (count == 1)：全部 enable（含 1, 2, 9）；列印走單筆 endpoint
- 多選 (count > 1)：1, 2, 9 disable；3–7 enable（多筆呼叫 batch endpoint 或前端逐筆）；8 enable

**普桌／普桌資料卡啟用條件（2026-07-18 改：完全解鎖）**：
- 與其他列印選項完全一致：**有選取列即 enable、選什麼印什麼**，前後端都不檢查 `signupType`（客訴：右鍵選項常被鎖、單選非普桌 422）
- 考據：舊系統 `tsmiPrintWorship_Click` 本就無型別檢查；原「限 type-4」是新系統自加的嚴格化，已全數撤回（詳見 [business-rules-implicit.md §16](../business-rules-implicit.md)）

**多筆列印實作策略（2026-07-03 更新，取代原 v1 限制）**：
- 選 1 筆 → 呼叫單筆 endpoint（`GET /reports/{type}?signupId=`）
- 選 > 1 筆 → 呼叫 `POST /api/v1/reports/batch` 帶 `signupIds: [勾選的 id...]`（見 [post-reports-batch.md](api-endpoints/post-reports-batch.md)），後端依 `SignupID IN (...)` 精準只印勾選的那幾筆，**不論編號是否連續**，不再需要湊區間或多印非選取列
- **已撤回**：原 v1「不連續時退化成 `numberStart=min, numberEnd=max` 編號區間 + 跳確認對話框告知會多印非選取筆數」的近似做法——已被上述精準模式取代，前端 `signup-list-page.ts` 的 `actionPrint` 不再跳該確認框
- 觸發方式：右鍵 menu / 列尾 kebab menu / 鍵盤 `Menu` 鍵 / 長按（touch）

**插入並順移（新版增強，2026-07-04）**：
- 需求：預繳載入後常需在既有連號序列中間補插一筆（漏報/臨時加報），並讓插入點其後的既有報名編號自動 +1 順移。舊系統無此能力（只能自動 MAX+1 或指定空號）。
- 入口：報名維護列表**右鍵某列 →「在此前插入」**（`signup-list-page.ts` `actionInsertBefore`，icon `insert-above`）。開 `signup-edit-form` 插入模式：帶入該列的年/法會/類型（**鎖定唯讀**）+ `keepNumber=true` + `customNumber=該列編號`（預填、可改）；信眾/名單/地址留空給使用者填。overlay title「插入報名（後續順移）」。
- 後端：`POST /api/v1/signups/insert-shift`（`InsertShiftSignupHandler` → `SignupRepository.InsertWithShiftAsync`）。單一交易內 `sp_getapplock`（`signup-number:` resource，**與預繳載入共用**）→ `UPDATE Signups SET Number=Number+1 WHERE ... AND Number >= @N`（set-based，`(Year,Cat,Type,Number)` 無 unique index 故無中間衝突）→ 插入新筆 + SignupLog。**刻意不做編號重複檢查**（插入位置本就佔用）。順移的既有列只 UPDATE Number、不 append SignupLog。詳見 [post-signups-insert-shift.md](api-endpoints/post-signups-insert-shift.md)。
- 範圍：僅 create 情境；編輯改編號仍走 `PUT /signups`（`SIGNUP_NUMBER_CONFLICT` 擋重複、不順移）。

#### 批次列印面板（btnPrint_Click 等價）

舊 [SignupForm.cs:447](../../reference/old/Ceremony/SignupForm.cs#L447) `btnPrint_Click` 從上方 nudStart / nudEnd / dlSearchSignupType 取編號區間 + 類型，**獨立於 grid 選取**。

新版獨立 panel（在 filter 區右側）：

```
┌─ 批次列印 ────────────────────┐
│ 起編號 [   ] ~ 迄編號 [   ]    │
│ 報表類型 [資料卡 ▼]            │
│ [列印批次]                     │
└────────────────────────────────┘
```

- 帶入年份 / 法會 / 類型 / yearGte 等其他 filter（沿用搜尋區當前值，避免使用者重設）
- 呼叫 `POST /api/v1/reports/batch` body `{ reportType, numberStart, numberEnd, year?, ceremonyCategoryId?, signupType? }`
- 回 PDF blob → 開新分頁 / iframe 預覽（同 reports preview page 機制）
- 普桌（worship）強制 SignupType=4；其他 reportType 跟隨當前 signupType filter

### 新增（NewSignupForm → 新版單頁，2026-05-29 欄位編排對齊舊版）

**結構決策**：舊 NewSignupForm 為兩步驟（先選年份/法會/類型 → 再選信眾並填詳細）；新版維持 mockup v4 的**單頁表單**（不重做兩步驟，無「下一步」），但**欄位編排對齊舊 NewSignupForm.cs**，單頁由上到下：

```
法會資料   民國年(預設 TaiwanCalendar.GetYear) / 法會分類 / 報名類型
           （2026-07-27 使用者指定改放**表單左側直立窄欄**，回到舊 plStep1 版面；
            取代 2026-07-17 的「提到表單最上方全寬」。以下欄位皆在右側主體區）
信眾       常駐搜尋列 + 結果列表直接顯示（2026-07-17 改，對齊舊常駐 dgvBelievers；
           選定後列表保留、可隨時點別筆改選覆蓋欄位；未選信眾也可送出 → 自動建新信眾）
           （搜尋框字級 2026-07-28 對齊地址輸入框 --font-size-base）
基本資料   堂號(可編輯 input) / 姓名 / 聯絡電話 ‖ 員工類型(可編輯 select) / 固定編號(可編輯 checkbox)
           （三欄 2026-07-21 改 per-signup 可編輯；2026-07-31 使用者指定改為此順序，
            且「堂號/姓名/電話」三欄的總寬度＝下方地址框寬度——左半走與 `.form-cols` 相同的巢狀
            達成逐像素等寬，不寫死欄寬；員工類型與固定編號落在右半）
地址       ┌ 寄件地址 ┐ 城市→區域(連動下拉)→郵遞區號(唯讀) / 寄件地址
           ┌ 文牒地址 ┐ ☑ 同寄件地址（框內最上方、**靠右對齊**）
                        城市→區域→郵遞區號 / 文牒地址（佔滿三欄）
           （2026-07-31 使用者指定：寄件/文牒**各一個 fieldset 外框**，框內**欄位標題全部拿掉**
            改 placeholder〔分段靠 legend 就夠，每欄再標一次「寄件/文牒」是噪音〕；
            勾選框由「兩段中間」移進**文牒框內**——它決定的是文牒段的內容）
           勾選＝複製 mail→text；**城市/區域/地址三者全空**才擋下並提示
             「請先填寫寄件地址（城市／區域或地址）」（2026-07-31 放寬，見下方客訴第四輪）
           （2026-07-17 改回上下堆疊，對齊舊 Designer 寄件 Y≈222 / 文牒 Y≈311）
名單       ┌ 往生名單 ┐×6 在上、┌ 陽上名單 ┐×6 在下（2026-07-17 對齊舊 Designer 往生 Y≈401 / 陽上 Y≈517）；
           往生輸入框不加底色（舊系統兩組皆無 BackColor，使用者指定）
           （2026-07-28 使用者指定：由右欄移到**左欄地址下方**，填地址與填名單是同一段連續動作）
           （2026-07-31 使用者指定：兩組各自有 fieldset 外框與 legend，填字後仍看得出哪組是往生）
編號/費用  ☑ 指定編號 + 編號 / 費用
備註/預繳  備註（2026-07-28 高度加兩行，rows=4）/ 預繳民國年 / 預繳法會
按鈕列     列印資料卡 / 取消 / 確認（**只能用滑鼠點，Enter 不送出**，2026-07-28 使用者指定；
           移到**備註下方**，
           不再是 overlay panel 底部 footer 或路由頁最下方；按鈕仍由 host 提供，
           投影進表單的 `form-actions` slot，overlay 端另給 `[showActions]="false"` 收掉 footer）
```

- **Enter 不送出（2026-07-28 使用者指定，與舊系統 AcceptButton 行為相反）**：表單內移除 `<button type="submit" hidden>` 與 `(ngSubmit)`（那顆隱藏鈕正是 HTML 隱含送出的來源），另在 `<form>` 上加 `onFormKeydown` 攔 Enter 並 `preventDefault()`——按鈕列已投影進 `<form>` 內，焦點停在「確認」上按 Enter 也會啟動，故需第二道。**例外：備註 `textarea` 放行**（要能換行）；信眾搜尋框的 Enter＝觸發搜尋（自己在 target 階段處理完），不受影響。回歸鎖 `signup-edit-form.component.spec.ts`「按 Enter 不送出表單」。**其他表單（信眾/管理員/法會分類）維持隱含送出，未一併改**
- **成功提示字級（2026-07-28 使用者指定）**：「編號X，新增報名成功」的 dialog 內文放大到 `--font-size-md`＝側欄選單 `.nav-label` 同級；改的是共用 `.confirm-body`，全站 confirm/alert dialog 一起放大
- **法會分類依當月自動帶季別（新版加值，2026-06-23）**：新增模式下載完分類樹後，依當前月份自動把「法會分類」預設為對應季別 root（1-4月→春季 / 5-8月→中元 / 9-12月→秋季，見 [business-rules-implicit.md](../business-rules-implicit.md) §月→季）。為**可編輯的預設**：使用者仍可改選任何季別或子法會（子法會仍人工挑選，月份只決定季別）。僅在 create 模式且使用者尚未選值時帶入；編輯模式不覆蓋既有 ceremony。實作：`util/ceremony-season.ts`（`currentSeason` / `resolveSeasonRootId`，GUID 優先、title 退場）+ `signup-edit-form` `applySeasonDefault()`
- 城市/區域連動下拉資料源：`GET /zipcodes/cities`、`GET /zipcodes?city=`（見 [get-zipcodes.md](api-endpoints/get-zipcodes.md)）；對齊舊 `LoadCity` / `dlMailCity_SelectedIndexChanged`
- **員工類型 / 固定編號 / 堂號改 per-signup 可編輯（2026-07-21，方案 A，反轉先前唯讀決策）**：這三欄由唯讀改為可編輯（員工類型 select、固定編號 checkbox、堂號 input），**只改這筆報名、不回寫 Believer**。後端 `Signups` 加自有 `HallName/EmployeeType/IsFixedNumber` 欄（DbUp 0001 加欄 / 0002 回填 / 0003 `SignupView` 改 `COALESCE(Signups.X, Believers.X)` + 新增數值 `EmployeeType`）；`CreateSignupRequest` 加 `employeeType/isFixedNumber`（`hallName` 已有），三個 handler 寫入 Signups 自有欄。前端 form group 加三 control，選信眾帶入現值當預設、submit 由表單值送出、未選信眾自動建立時用表單值建新信眾。**預繳保號仍讀 `Believers.IsFixedNumber`**（per-signup 覆寫不影響預繳保號，使用者指定）。見 [signup-hallname-isolation.md](signup-hallname-isolation.md)、[business-rules-implicit §3.1](../business-rules-implicit.md)
- **未選信眾 → 自動建立新信眾（2026-07-17 補齊，對齊舊 `btnConfirm_Click:186-223`）**：舊系統 `dgvBelievers.SelectedRows.Count == 0` 時當場 `Guid.NewGuid()` INSERT Believers 再建報名；新版 API 層維持不做 inline 建立（`CreateSignupRequest.BelieverId` 必填），由**前端 orchestration**：`submit()` 發現 create 模式且無 believerId → 先 `POST /believers`（employeeType=1 非員工、isFixedNumber=false，同舊表單下拉/checkbox 預設；姓名/電話/兩組地址/陽上/往生取自表單）→ 拿到 id 綁回表單再 `POST /signups`。信眾建立成功但報名失敗時 believerId 已綁回表單，重送不會重複建信眾。此前前端漏做這條路（believerId 掛 required），導致「沒選信眾就完全無法新增」——已修
- **選信眾自動帶入預繳歷史**：`pickBeliever` 呼叫 `GET /prepay?believerId&year`，最新報名有預繳則帶入預繳年/法會（對齊舊 `BelieverSelected:1102-1115`；見 [get-prepay-believer-latest.md](api-endpoints/get-prepay-believer-latest.md)）
- **新增報名頁客訴六項（2026-07-21）**：
  1. **「取消」＝清成新的一筆、不關閉/跳頁**：新增模式的取消鈕改呼叫 `SignupEditFormComponent.resetBelow()`——保留最上方法會資料（年/法會/類型）作為連續輸入的固定情境，清除已選信眾＋搜尋框＋搜尋結果與信眾以下欄位（基本資料/地址/名單/編號/備註/預繳），回到全新新增狀態。**例外：費用不清（2026-07-28 使用者指定）**——同一場法會連續輸入時金額多半固定，每按一次取消就要重打很煩；與下方客訴 2「改選信眾不清費用」同一取捨（費用唯一來源是使用者手打，舊系統也沒有任何路徑會清 `txtFee`）。回歸鎖：[signup-edit-form.component.spec.ts](../../frontend/src/app/features/signups/signup-edit-form.component.spec.ts)「按『取消』保留法會資料與費用」。**兩個宿主都要改**：(a) overlay（主要 UX）`signup-list-page.onOverlayCancel()` 依 `editOverlay().signupId` 分流，新增時 resetBelow 且**不關閉 overlay**（要關閉走標題列 ×；backdrop / Esc 自 2026-07-28 起已停用，見下方「overlay 只能用 × 關」）；(b) 路由頁 `/signups/new`（deep-link）`signup-edit-page.onCancel()` 原本 `<a routerLink="/signups">` 直接跳頁 → 改成新增模式 resetBelow、不跳頁（同時移除未使用的 `RouterLink` import）。編輯模式（含 `/signups/:id/edit`）「取消」維持關閉/返回列表
  2. **改選信眾殘留欄位修復**：`pickBeliever` 覆蓋整張表單前先清掉上一筆殘留的預繳（預繳年/預繳法會）；預繳先歸零再由 `prefillPrepayHistory` 只在確有紀錄時回填，避免新信眾查無預繳卻沿用前一筆。**編號欄（keepNumber/customNumber）2026-07-31 起改為不清**（見下方客訴第四輪第 4 項）。**備註 2026-07-27 起改為帶入該筆報名的 `remark`（無則空字串）**，不再一律清空（見下方「選定回填」）。**費用 2026-07-27 起改為不清（使用者指定，取代原本一併清空的作法）**：費用不會從結果列帶入，唯一來源是使用者自己輸入，改選時清掉等於吃掉已打好的金額；舊 `BelieverSelected` 也完全沒碰 `txtFee`（只在 `btnConfirm_Click:236` 讀值 + `txtFee_Validating` 數字驗證），故**不清才是對齊舊系統**
  3. **搜尋結果 list 文字大小＝地址**：`.believer-results table.data-table` 覆寫 `font-size: var(--font-size-base)`（原 `.data-table` 為 `--font-size-sm`），對齊地址輸入框
  4. **往生/陽上名單文字大小＝地址**：`.names .name-grid input` 明訂 `font-size: var(--font-size-base)`（原未設、繼承 body 雖同值，改為顯式對齊）。**2026-07-27 客訴續修「名單的字比較黑」**：同一區塊再補 `color: var(--c-text-primary)` + `background: var(--c-surface)`——這些 input 不在 `.field` 內，吃不到全域 `.field input` 的 color/background，會落回 UA 樣式表的 `color: fieldtext`（純黑 #000，直接設在元素上、不是繼承）。**新增信眾表單 `believer-edit-form` 同步補**（兩份表單自 2026-07-18 起刻意同步）
  5. **編號欄顯示在勾選文字右邊**：新增模式用 `.keep-number-row`（flex，`align-items:flex-end`）把 checkbox 與編號欄放同一列、編號在右；非另起一列。編輯模式編號仍為獨立 `.field`。**2026-07-27 使用者指定改回舊行為（取代原本「勾了才出現」）**：編號欄**恆顯示**，未勾＝`disabled`（看得到、不能改），勾「指定編號」才 `enable`——對齊舊 `cbKeepNumber_CheckedChanged:139-149`（`txtNumber.Enabled = cbKeepNumber.Checked`）。實作 `syncCustomNumberEnabled()`（`ngOnInit` + `keepNumber.valueChanges` 兩處觸發）；**編輯模式編號恆可改**、不受勾選影響；插入模式 `keepNumber` 被鎖為 true 故編號可編輯。disabled control 不進 `form.value`，但 submit 走 `getRawValue()` 故送出不受影響
  6. **地址非必填**：前端 `mailAddress` 移除 `Validators.required`（僅留 maxLength 200）＋拿掉「寄件地址 *」星號；**後端同步放寬**（見 [post-signups.md](api-endpoints/post-signups.md) 與 §地址非必填業務規則）。「同寄件地址」勾選當時仍要求先有寄件地址文字；**2026-07-31 一併放寬**為「城市/區域/地址三者全空才擋」（見下方客訴第四輪第 5 項）
- **新增報名表單客訴五項（2026-07-31，第四輪）**：前三項是版面（細節與理由見 [frontend-design.md「報名表單版面（2026-07-31 客訴第四輪）」](../design/frontend-design.md)），後兩項是行為。
  1. **地址：欄位標題全拿掉、寄件/文牒各一個外框**（「同寄件地址」勾選移進文牒框內）——上一輪把外框全拿掉之後，使用者要的其實是「框回來、欄位標題拿掉」
  2. **往生/陽上名單各自框起來**，解掉 07-29 記錄的「填字後失去往生/陽上標示」取捨
  3. **基本資料改序 堂號→姓名→電話→員工類型→固定編號**，前三欄與地址框逐像素等寬
  4. **勾了「指定編號」不再被改選信眾清掉**（勾選與已輸入的數字都保留）：`pickBeliever` 移除 `patchValue({ keepNumber: false, customNumber: null })`。與費用同一取捨，**也回到舊系統行為**——舊 `BelieverSelected` 完全沒碰 `cbKeepNumber`/`txtNumber`，只有 `PanelFormEmpty()`（＝我們的 `resetBelow`，按「取消」時）才清（`NewSignupForm.cs:853-854`），故 `resetBelow()` **維持清空**。順帶確認「重新查詢信眾」本來就不動任何表單欄位（`triggerBelieverSearch` 只清搜尋結果 signal）
  5. **「同寄件地址」放寬觸發條件**：由「寄件地址文字欄非空」改為「寄件的城市/區域/地址**三者全空**才擋」，提示改「請先填寫寄件地址（城市／區域或地址）」。**刻意偏離舊系統**（舊 `NewSignupForm.cs:477-502` 硬性要求 `txtMailAddress.Text.Trim() != ""`）——地址自 2026-07-21 起已非必填，只選了城市與區域是合法狀態，這時同步城市/區域一樣有意義
  - **信眾表單同步**（使用者指定）：第 1、5 項一併套到 `believer-edit-form`（往生/陽上本來就有外框；第 3、4 項是報名表單獨有）。⚠ 該表單的 `mailAddress` 仍掛 `Validators.required`，與報名表單/後端 2026-07-21 的放寬不一致，本次未動，列入 [pending-business-input.md](../pending-business-input.md) 待確認
  - **回歸鎖**（`signup-edit-form.component.spec.ts`，`ng test` 31 passed）：改選信眾不清編號勾選/數字且 `resetBelow` 仍清、只選城市+區域可同步文牒、三者全空才彈回勾選。`ng build` 0 warning；**版面待實機複驗**
- **overlay 只能用 × 關（2026-07-28 客訴）**：新增報名／編輯報名的彈出視窗，使用者指定「要點 X 或是取消才會關起來」——`<app-form-overlay>` 新增 `dismissible` input，報名維護給 `[dismissible]="false"`，backdrop click 與 `Esc` 兩個關閉入口 early-return，× 與表單自己的按鈕不受影響（dirty 確認邏輯完全不動）。理由：這張表單很長（法會資料＋信眾搜尋＋地址＋名單＋預繳），輸入途中手滑點到 panel 外或按 Esc（想取消輸入法組字）整張就收掉。**只改報名維護**，信眾／分類／管理者三個 overlay 維持可點外面關（表單短、誤關成本低）。注意「取消」在**新增模式仍是清成新的一筆、不關閉**（見上方客訴 1，2026-07-21 使用者指定）；編輯模式的「取消」才是關閉
- **填到一半切走不能被清掉＝跨路由草稿（2026-07-27 客訴）**：新增報名是 overlay（backdrop `position:fixed; inset:0` 蓋滿全螢幕），要點側欄切到其他功能頁**一定得先關掉 overlay** → 表單元件被銷毀 → 填到一半的內容全沒。修法：新增 root singleton [`SignupDraftState`](../../frontend/src/app/features/signups/signup-draft-state.ts)（比照既有 `SignupSearchState`），元件銷毀時存、下次開啟時還原。**使用者定案的三項行為**：
  1. **只存記憶體、不落地**：等同舊 WinForm「Form 還開著」的語意；系統關閉重開後草稿消失，避免隔天開機跑出前一天的舊資料。取捨：畫面重新整理／Electron 重啟會掉草稿（實務上罕見），換掉「舊草稿陰魂不散」與「要另做清除機制」的成本
  2. **靜默還原**：重開新增報名時直接把欄位填回去，不顯示「已帶回上次未完成資料」提示列，也不另設「清除草稿」鈕——要清空走既有的「取消」鈕（＝清成新的一筆，見上方客訴 1）
  3. **純新增模式關閉 overlay 不再跳「未儲存的變更」確認**：資料既然會保留，攔人反而與實際行為矛盾。`signup-list-page` 的 `[dirty]` 改綁 `editFormDirty() && overlayGuardsDirty()`；**編輯 / 代入新增（fromSignupId）／插入（insertAt）三種模式不做草稿、維持原確認**——這三種各有自己的資料來源（既有報名 / 來源報名 / 插入群組），還原草稿只會互相打架
  - **草稿內容**：整份 `form.getRawValue()`（含法會資料/信眾/地址/名單/編號/費用/備註/預繳）＋ 已選信眾摘要、常駐搜尋列表的關鍵字/結果/高亮列、`lastCreatedSignupId`（＝「列印資料卡」鈕的啟用狀態）、`dirty` 旗標。**不入草稿**：城市→區域下拉的選項（還原時重跑 `applyAddress` 連動重新載入）與重複報名警示（`valueChanges` 會自動重查）
  - **快照時機：離開時一律存，沒有任何清除時機（2026-07-28 使用者定案，取代 07-27 的規則）**——使用者要的是「回到新增表單時資料要是離開之前的狀態」，所以 `saveDraft()` 拿掉 `form.dirty` 門檻、`submit()` 成功與 `resetBelow()` 也不再 `clear()`（`SignupDraftState.clear()` 一併移除）。原規則有兩個破口：**存檔成功後**（畫面刻意留著那筆、表單 pristine）與**按「取消」後**（畫面留著法會資料＋費用、表單 pristine）都會被門檻擋掉 → 「畫面上看得到、切走再回來卻不見」。唯一的清除時機是系統關閉／重載（草稿只存記憶體）
  - ⚠ **連帶取捨**：存檔成功那筆現在會跟著帶回來，且「確認」鈕仍可按 → 重按會再新增一筆。這與下方「存完不關閉、資料留著」本來就有的風險是同一個（舊系統亦然），要開乾淨的下一筆請按「取消」
  - **順帶修掉的兩個殘留（2026-07-27 同日稽核 `pickBeliever` 逐欄對照後發現）**：
    1. **改選信眾的 in-flight race**：`pickBeliever` 有多個 `await`（信眾主檔 / 區域清單 / 預繳歷史），但**沒有** `triggerBelieverSearch` 那樣的 token guard。使用者在回應到齊前再點別列時，先點那筆的慢回應會蓋掉後點的、或兩筆混在一起（最明顯是地址：`mailAreas` 選項清單與 `mailZipcodeId` 對不起來，區域下拉變空白）。修法：`pickToken`＋`isStale()`，每個 await 後判斷，非最新的一律放棄寫入；`applyAddress` / `prefillPrepayHistory` 加選用的 `isStale` 參數（其他呼叫端不受影響）；`resetBelow()` 也 `pickToken++`，避免按完取消後在途回應把欄位填回來。順帶把 `errorMessage` 在改選時清掉（上一次操作的錯誤不該掛在新選的信眾上）
    2. **選了信眾但一個字都沒改就切走，草稿不會存**：`patchValue` 不會標髒，而草稿條件是 `form.dirty` → 這條路徑回來仍是空白（正是客訴情境之一）。修法：`pickBeliever` 結尾 `form.markAsDirty()` + `dirtyChange.emit(true)`（選信眾＝實質輸入）
  - 回歸測試：[signup-edit-form.component.spec.ts](../../frontend/src/app/features/signups/signup-edit-form.component.spec.ts)（離開再回來帶回、開了沒改就離開內容不變、**取消後切走再回來＝取消後的畫面**、**存檔成功後切走再回來仍是同一份且列印資料卡維持可按**、編輯模式不存、**只選信眾沒打字也帶回**、**連續改選兩位信眾先選的慢回應不會蓋掉後選的**；race case 已用「暫時移除 guard → 該 case 轉紅」確認抓得到）
- **信眾搜尋 1:1 對齊舊 `dgvBelievers`（2026-07-02 決策，取代先前簡化卡片式設計；2026-07-17 由 modal picker 改為常駐 in-form 列表，完全回到舊系統型態）**：
  - **常駐列表（2026-07-17）**：搜尋框/搜尋鈕/結果表格直接放在表單頂部「信眾」fieldset（全寬），非彈窗——對齊舊 `plStep2` 上常駐的 `txtQ + dgvBelievers`。點列選定後**列表保留**、可隨時再點別筆（每次改選重新覆蓋整份表單欄位，同舊 `dgvBelievers_CellClick`）；選定列高亮 + 「已選信眾」摘要（**僅選定後顯示**；未選時不顯示任何提示文字，「符合 N 筆僅顯示前 200」截斷提示也不顯示——2026-07-17 使用者指定拿掉，截斷本身保留）。結果表格高度＝**表頭 + 4 列**（2026-07-27 使用者指定；`max-height: 25px × 5`，並把 cell `line-height` 寫死成 `25px - padding×2` 讓「剛好 3 列」算得準——原本 140px 靠字型 line-height 推估約 5 列），第 4 列起內部捲動（舊 dgv 高 117px 同精神），**靜默截斷最多 render 前 200 列**（模糊字如「陳」可命中 2 萬+ 列，全塞 DOM 會卡死；舊 WinForms grid 有虛擬化沒此問題）。**無 row hover 變色**（對齊 vgrid，見配色規範）。**配色/列高對齊報名維護 grid（2026-07-17 使用者指定）**：走全站唯一權威 [visual-design.md「清單/資料格配色規範」](../design/visual-design.md)——`.data-table.dense` 已補直向格線/表頭底線/往生欄右框線與 vgrid 一一對應（Playwright computed-style 9 項比對全同值），cell padding 收為 2px 6px（列高 25px ≈ 報名維護 26px）。編輯模式不顯示搜尋區（不換信眾），僅顯示信眾摘要卡
  - **搜尋**：單一輸入框 + 「搜尋」按鈕觸發（**2026-07-02 改**：原本 `(input)` 即時查詢，即使加 debounce 仍是「打字就打 API」；改回對齊舊 `btnBelieverSearch_Click` 的按鈕觸發語意——文字先落地在框內，按鈕或 Enter 才真正查詢），OR 比對 Name/Phone/6組陽上/6組往生共 14 欄（對齊舊 `NewSignupForm.cs:715-722` `txtQ`/`LoadBelievers`）。**兩路併查、不新增 endpoint**（2026-07-27 補齊舊 `BelieverView` 語意）——同一把關鍵字並行打：(a) `GET /api/v1/signups`（`SignupApi.search`）帶 `searchKey` + `scopeName/scopePhone/scopeLivingName/scopeDeadName=true`（`scopeRemark` 不開，舊系統不搜備註）出**報名紀錄列**；(b) `GET /api/v1/believers?searchKey=`（**既有端點新加 `searchKey` 參數**，同樣 14 欄 OR，見 [get-believers.md](api-endpoints/get-believers.md)）出**信眾主檔**，前端濾掉已在 (a) 出現的 `believerId` 後，把剩下的「**從未報名過的信眾**」包成報名欄位留空的列。舊系統 `LoadBelievers` 的資料源正是 `BelieverView`（Believers LEFT JOIN Signups），舊 code 註解「如果沒有報名過就查不到」就是當初從 `SignupView` 換過去的原因；此前新版只查 `/signups`，等於退回舊系統修掉的那個 bug——沒報名過的信眾只能靠「不選信眾直接送出自動建新信眾」繞過（且會建出重複信眾）
  - **清單粒度/欄位**：每筆「報名紀錄」一列（非每信眾一列，同信眾過去報過多次會重複出現多列），16 欄：堂號/姓名/聯絡電話/編號標題/編號/年份/法會/往生1~6/陽上1~6（欄位順序對齊 `NewSignupForm.Designer.cs:1017-1355`）；資料直接來自 `SignupListItem`（`/signups` 既有回應，本已含全部所需欄位，未新增後端 DTO）。**未報名過的信眾補列（2026-07-27）**：由 `makeSignupRowFromBeliever` 把 `BelieverListItem` 包成同型別列，年份/法會/編號標題/編號留空（`year: 0` 於 template 以 `row.year || ''` 顯示空白），`id` 加 `believer:` 前綴避免與 SignupID 撞號；**排在報名列之後**（對齊舊 BelieverView 依 `Year desc` 排序、null 墊底），並有**獨立額度 50 列**（`MAX_BELIEVER_ONLY_ROWS`），不與報名列的 200 列額度互相排擠——這批正是最需要被找到的（要幫他報名）
  - **選定回填＝帶「點到的那筆報名」（2026-07-27 客訴修正，取代先前「一律帶信眾主檔」）**：清單一列＝一筆報名，故點列後帶入的是**該筆報名自身的快照**——姓名/電話/寄件+文牒地址/往生+陽上名單/備註（`SignupListItem` 既有欄位，不再另外查表）；`GET /believers/{id}`（`BelieverApi.getById`）仍呼叫，但只當**該欄為空時的 fallback**與「已選信眾」摘要卡資料源（對齊舊 `BelieverSelected:991-1101` 的 `signup != null ? signup.X : believer.X` 分支）。地址判斷：該筆報名有 city 或 address 就用該筆（只存 city/area 字串、無 zipcodeId → 以區域名稱比對 areas），整段皆空才退回信眾主檔（含 zipcodeId）。員工類型/固定編號/堂號取該筆值（`SignupView` 已 `COALESCE` 回退信眾，見 §per-signup 可編輯）。**年份/法會/報名類型/編號/費用不帶**（新的一筆自己決定）；**費用刻意不清**（2026-07-27 使用者指定，見上方客訴 2——費用唯一來源是使用者輸入，改選時清掉等於吃掉已打好的金額，舊 `BelieverSelected` 亦未碰 `txtFee`），預繳先歸零再由 `prefillPrepayHistory` 條件回填。
    - 修正前行為：一律以 `Believers` 主檔覆蓋整張表單，導致同一信眾點哪一列都拿到同一份（多半是最早建檔時的）名單/地址，看不出各筆報名差異——舊系統從不因報名回寫 Believers（`btnConfirm_Click` 只在「未選信眾」時 INSERT 一次），主檔會與歷次報名脫節
  - **已知落差**：`/signups` 現有排序為 `Year, CeremonySort, NumberTitle, Number`（ascending，服務主列表用途），未暴露 `CeremonySort` 供前端精確重現舊排序（`Year desc, CeremonySort desc, NumberTitle asc, Number desc`）；前端改用整體反轉近似「新的在前」，未新增後端排序參數
  - 實作：[signup-edit-form.component](../../frontend/src/app/features/signups/signup-edit-form.component.ts) `triggerBelieverSearch` / `runBelieverSearch` / `pickBeliever`
- **重複報名警示（新版加值，2026-06-30）**：選定信眾後（或改年份/法會時）即時查該信眾在同一 `(Year, CeremonyCategoryID)`（**忽略報名類型**）是否已有報名；有則跳 `.alert-warn` 警示並逐筆列「編號 · 報名類型」。**位置 2026-07-27 使用者指定移到左欄「法會資料」下方的空白處**（取代原本的信眾區塊下方）——它講的正是「這位信眾在這個年份/法會曾經報過什麼」，貼著法會資料看最直覺，也順便把左欄下方空白用起來、不再把右側主體往下推；左欄僅 176px 故文案會折行（行高收緊、清單縮排收小）。**僅提醒、不阻擋**，使用者仍可照常儲存。判定走唯讀 `GET /signups/duplicates`（`SignupApi.checkDuplicates`），前端以 `combineLatest`（year/ceremony/believer 三 control，debounce 300ms）觸發。編輯模式帶 `excludeSignupId` 排除自身。詳見 [get-signup-duplicates.md](api-endpoints/get-signup-duplicates.md)、[business-rules-implicit.md](../business-rules-implicit.md) §1.4
- 「確認」→ `POST /api/v1/signups`（`CreateSignupHandler`，atomic：Insert Signups 自動 Number/NumberTitle + Insert SignupLogs 快照）
- **新增成功後不清表單、不關閉，只跳成功訊息（2026-07-27 使用者指定，對齊舊 `NewSignupForm.btnConfirm_Click:355-361`）**：舊系統成功後先 `signupForm.LoadSearchSignups()` 重查列表，再跳 `CustomMessageForm`「編號X，新增報名成功」，**表單原樣留著**、按鈕重新啟用（可接著按「列印資料卡」）。新版同序：`saved` output 改帶 `SignupSavedEvent { keepOpen }`——
  - `keepOpen = !編輯 && !插入`：**新增類存完不關閉、欄位資料全部留著**；host（overlay 與 `/signups/new` 路由頁）收到一律重查列表，只有 `keepOpen=false`（編輯／插入）才關閉/返回
  - 成功訊息走全站既有的單按鈕結果 dialog（`ConfirmDialogService.ask({ hideCancel: true })`，非瀏覽器原生 `alert`），文案 `編號{number}，新增報名成功`（`number` 取自 `POST /signups` 回應的 `SignupListItem`）
  - `form.markAsPristine()` + `dirtyChange.emit(false)`：畫面上的資料是「剛存好那筆的殘影」，留著方便接續操作。**草稿 2026-07-28 起不在此清掉**（原本會 `clear()`）——畫面既然留著，切走再回來就該看到同一份，改由離開時的 `saveDraft()` 一律快照（見上方草稿段）
  - ⚠ **已知取捨（同舊系統）**：資料留著且「確認」鈕仍可按，重按會**再新增一筆**（舊 `btnConfirm.Enabled = true` 亦同）。要開下一筆請按「取消」（＝清成新的一筆、保留法會資料）
  - 編輯模式維持原本「存檔即關閉/返回列表」，未加成功訊息（舊 `EditSignupForm.cs:367` 有「修改報名成功」，尚未要求）
- **「列印資料卡」（2026-07-27 補回，對齊舊 `btnPrintDataCard`）**：新增類在 **overlay 底部 actions 列、「取消」按鈕左邊**（同日使用者指定位置；`/signups/new` 路由頁的按鈕列同樣處理），**存檔前 disabled**（無額外提示文字）、**新增成功後啟用**——完全對齊舊 `NewSignupForm`（進 Step2 `btnPrintDataCard.Enabled = false`:95 → `btnConfirm` 成功 `= true`:361）。點擊後以「剛新增那一筆」的 id（＝舊 `CurrentSignupID`，存於表單的 **public** `lastCreatedSignupId` signal，供 host 判斷 disabled；host 端用 `viewChild()` signal query 取表單，`@ViewChild` 非 reactive 在 OnPush + zoneless 下不會即時更新）打既有 `GET /reports/datacard?signupId=`，PDF 開新分頁預覽（共用 `shared/util/pdf.ts` 的 `openPdfInNewTab`，自 signup-list-page 抽出）。按「取消」（＝清成新的一筆）會把它重新 disable。**編輯模式不顯示**——既有報名要列印走報名維護的右鍵選單
- **overlay 彈跳視窗寬度固定 900px（2026-07-27 客訴「報名維護的彈跳視窗太寬」）**：panel 預設 content-adaptive（`max-width: 92vw`），而信眾搜尋結果表有 19 欄，其 max-content 會把整個視窗撐滿。修法是**限制 panel**——`<app-form-overlay>` 新增 `width` input（見 [frontend-design](../design/frontend-design.md)），報名維護傳 `width="1100px"`——**與新增報名頁 `/signups/new` 的 `.page { max-width: 1100px }` 對齊**（2026-07-27 使用者指定兩處寬度一致；兩邊各留互指的註解，改一邊要改另一邊）；全域 `max-width: 92vw` 仍在，小視窗自動縮。結果表在自己的 `overflow: auto` 容器內橫向捲動。**⚠ 別改在表單的 `:host`**：曾這樣做（`width: min(920px, 88vw)`），panel 仍被表格撐寬 → 表單縮了、panel 沒縮，底部「取消／確認」落單在右下角（同日客訴，已改）
- 實作：[signup-edit-form.component](../../frontend/src/app/features/signups/signup-edit-form.component.ts)（create/edit 共用）

### 編輯（EditSignupForm 對應）

```
1. 從 SignupForm 右鍵「修改資料」進入
2. 編輯區帶入既有資料
3. dlBeliever 下拉（AutoComplete）切換信眾：
   自動 sync HallName / EmployeeType / IsFixedNumber
   不自動 sync Name / Phone（保留 Signup 級獨立）
4. 修改 → 「確認」→ atomic transaction：
   1. Update Believers（HallName/EmployeeType/IsFixedNumber）
   2. Update Signups（全欄位含 Name/Phone）
   3. Insert SignupLogs（action=2=Update，完整快照）
   - 編號重複檢查排除自身 SignupID
5. 「修改報名成功」
```

- **重複報名警示同樣適用編輯**（共用 `signup-edit-form`）：若把年份/法會改成與該信眾另一筆相同，跳警示但**排除自己這筆**（`excludeSignupId=signupId`）；僅提醒、不阻擋。見上方「新增」段與 [get-signup-duplicates.md](api-endpoints/get-signup-duplicates.md)。

### 歷程（SignupLogForm 對應）

```
1. 從 SignupForm 右鍵「瀏覽歷程」進入
2. 唯讀 grid，Createdate DESC
3. 顯示 signup_logs 完整快照（含 action 標示新增/編輯/刪除）
```

## 設計決策

### 關鍵選擇

- **MediatR Command + TransactionBehavior** 包覆三段 SaveChanges
  - 舊：3 段 SaveChanges 非原子 → 中斷則資料不一致
  - 新：一個 EF transaction，任一失敗整個 rollback
- **編號生成改用樂觀鎖 + retry**
  - 舊：`MAX(number)+1` 兩人同時做 → race
  - 新：`INSERT ... OUTPUT inserted.number; IF duplicate THEN retry`，或用 sequence
- **NumberTitle 不可手動覆寫**
  - 由 `SignupType` 推導：1→No、2→寺、3→觀、4→普、5→郵
  - 在 Domain Service `NumberTitleResolver` 集中
- **避 4 顯示與資料分離**
  - DB 存實值（含 4）
  - 顯示層 `AvoidFourFormatter`：個位 4 → "3-1"
  - 例：104 → "103-1"
- **PredicateBuilder 改用 EF Core Expression composition**
  - 保留 AND/OR 語意，移除 LinqKit
- **資料帶入優先順序明確化**
  - Name/Phone：Signup → GridRow → Believer
  - Address：Signup → Believer
- **編輯時 Name/Phone 仍允許 Signup 級獨立**
  - 業務需要：報名快照可不同於 Believer 主檔
- **SignupLogs 現況無 `action` 欄位**（沿用既有 schema）
  - 沿用舊行為：同 SignupID 的第一筆 = 新增，後續 = 編輯
  - 刪除：可選擇是否寫 log；若寫，應用層在備註欄補「[Deleted]」標記
  - 若業務需求強烈，可走 DbUp migration 加 `action` 欄位（DB 已解除凍結，待評估）

### 取捨

- 取了：資料完整性、可審計、可平行操作
- 捨了：舊版簡單但有 race / 不原子的「直接寫」便利

## 跨層影響

| 層級 | 是否影響 | 變動摘要 |
|---|---|---|
| 視覺 | 是 | SignupForm / NewSignup / EditSignup / SignupLog 對應頁；訊息 verbatim |
| 前端 | 是 | signups feature 全部；data-grid context menu；wizard 元件；Signal-first store |
| 後端 | 是 | Signup Repository（Dapper）+ 6 個 Handler（Create/Update/Delete/Search/Logs/PrintPrepare） |
| API | 是 | `/signups/*` 完整 |
| 資料庫 | 現況否 | 本功能用既有 schema；DB 已可變更，加 action 欄位/索引為待評估 migration |
| 效能 | **是** | server-side 分頁、Dapper 直接 SQL、UPDLOCK 編號生成、in-memory cache；詳見 [performance.md](../design/performance.md) |
| 安全 | 部分 | 寫操作 Serilog file log（非 DB audit） |

## 業務規則摘要

| 規則 | 描述 |
|---|---|
| NumberTitle 推導 | 1=No, 2=寺, 3=觀, 4=普, 5=郵 |
| 寺方(2) 顯示 | 只顯示 NumberTitle，不加 Number |
| 避 4 顯示 | 個位 4 → "3-1"（DB 存實值） |
| 編號生成 | `MAX(year, ceremonyId, signupType, number)+1`；無記錄則 1 |
| 編號重複檢查 | (year, ceremonyId, signupType, number) 不可重複；編輯時排除自身 |
| 地址 fallback | 疏文空 → 用寄件 |
| 信眾資料帶入 | Name/Phone：Signup > GridRow > Believer；Address：Signup > Believer |
| 預繳自動帶入 | 該信眾「今年以前」最新報名的預繳資訊 |
| 編輯時 Believer 同步 | HallName / EmployeeType / IsFixedNumber；**不**同步 Name / Phone |
| 變更歷程 | 每次 Create/Update/Delete 插入 signup_logs 快照 |

## 驗收標準

- [ ] SignupForm 四面板版型與舊系統一致
- [ ] 40 欄 grid 預設 32 顯示；cbShowAll 控制 5 欄；10 內部欄永遠隱藏；cbShowAll **每次開頁一律不勾**（不記憶）
- [ ] 「全部」勾選＝只解除年份/法會/類型（值保留且變灰），其餘條件仍可改、仍可按搜尋；取消勾選還原原本三個條件；匯出 Excel 跟著模式走
- [ ] DeadName 1..5 欄背景 `#FFE0C0`
- [ ] AND/OR 搜尋邏輯與舊系統一致（全空 → 全部、勾選任一才能填 key）
- [ ] 「列印普桌」「列印普桌資料卡」有選取列即啟用，任何型別皆可印（前後端均不限 type）
- [ ] 編號重複（含編輯排除自身）回 409 + 訊息 verbatim
- [ ] 編號 4 顯示為「3-1」
- [ ] NumberTitle 無法手動覆寫（API 無此參數）
- [ ] 新增/編輯為單一 transaction，中斷 rollback
- [ ] 多用戶同時新增同年同法會同類型，編號不衝突
- [ ] 歷程頁面 Createdate DESC（依舊 schema，無 action 欄位）
- [ ] **Grid 右鍵 context menu 9 項齊備**（代入新增 / 修改資料 / 列印 5 種 / 刪除 / 瀏覽歷程）
- [ ] **右鍵啟用規則對齊舊系統**：代入新增 / 修改資料 / 瀏覽歷程 → 單選 only；列印 / 刪除 → 單 + 多選
- [ ] **「列印普桌／普桌資料卡」不 grey out**：有選取即 enable；選什麼印什麼（對齊舊系統）
- [ ] **多選 checkbox** + shift 範圍選取（點列與點 checkbox 都吃得到 shift；錨點不動故範圍可縮小；shift 不觸發文字反白）
- [ ] **批次列印面板**（起編號 / 迄編號 / reportType）獨立於 grid 選取，呼叫 `POST /reports/batch`
- [ ] 列印結果走新分頁 / iframe 預覽（不再有「PDF / 預覽列印」對話）
- [ ] 通過 [code-review](../workflows/code-review.md) 與 [qa-testing](../workflows/qa-testing.md)

## 效能要點（**資料越來越多會吃這裡；以應用層手段為主，索引可走 migration 補強**）

| 場景 | 措施 | 依據 |
|---|---|---|
| 搜尋大表 | server-side 分頁強制（pageSize ≤ 200）+ UI 必填年份+法會限縮範圍 | [performance.md](../design/performance.md) §2 |
| 編號生成 | `UPDLOCK + HOLDLOCK` 序列化 + 5 次 retry | §6 |
| 批次列印 100 筆 PDF | QuestPDF stream 到 HTTP response，不寫暫存 | §8 |
| Excel 匯出 5k 列 | ClosedXML；> 10k 用 OpenXmlWriter streaming | §9 |
| Grid 顯示大量 | `cdk-virtual-scroll-viewport` + server-side paging | §「DataGrid 虛擬滾動」 |
| AutoComplete 信眾 | 改 typeahead（debounce 300ms，後端模糊查詢回前 20 筆） | §「Search debounce」 |
| 編輯歷程列表 | 只取最近 100 筆 + Createdate DESC | §2 |
| 預繳查詢 | UI 必填來源年+法會限縮範圍 | §2 |
| 靜態資料（法會分類、Zipcodes） | IMemoryCache | §4 |

## 風險與未解問題

- 編號樂觀鎖 retry 上限 — 5 次後仍失敗回 503，前端引導重試
- AutoComplete 載入 50k+ 信眾 — **必須**改為遠端 typeahead，不可一次撈全
- 大表 search export 100k+ 列 → stream + 分批寫 Excel
- SignupLogs 預期成長最快（每次 edit 一筆）— 5 年後評估歸檔策略

## 參考資料

- [scratch/03-signup-main.md](../../.scratch/explore/03-signup-main.md)：SignupForm 四面板、40 欄、AND/OR、批次列印、Excel
- [scratch/04-signup-create-edit-prepay-category.md](../../.scratch/explore/04-signup-create-edit-prepay-category.md) §A/B：NewSignupForm 兩步驟、EditSignupForm Believer sync
- 舊原始碼：[SignupForm.cs](../../reference/old/Ceremony/SignupForm.cs)、[NewSignupForm.cs](../../reference/old/Ceremony/NewSignupForm.cs)、[EditSignupForm.cs](../../reference/old/Ceremony/EditSignupForm.cs)、[SignupLogForm.cs](../../reference/old/Ceremony/SignupLogForm.cs)
- [Library.cs](../../reference/old/Ceremony/Commons/Library.cs)：GetSignupNumber / GetNumberText
