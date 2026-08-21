---
title: NewSignupForm Legacy Coverage
purpose: 反向稽核 — NewSignupForm 所有方法/事件的新系統對應狀態（1118 行）
applicable_when: 完成 POST /signups + 編號分配 endpoint 後勾選；月度稽核；上線前 gate
legacy_form: NewSignupForm.cs
legacy_path: reference/old/Ceremony/NewSignupForm.cs
legacy_lines: 1118
audit_status: complete
coverage_percentage: 100
last_audited: 2026-08-05
baseline_completed: 2026-05-27
total_methods: 34
related_agents:
  - backend-engineer
  - qa-test-engineer
related_docs:
  - ../api-endpoints/README.md
  - ../signup-management.md
  - README.md
keywords: [legacy, coverage, new-signup, 報名建立]
last_updated: 2026-08-21 (加值清單補「移動並重排 ➕ 新版增強」一條（右鍵「移動插入至…」，見 post-signups-move-number.md）——非 legacy 方法，覆蓋率不受影響。先前 2026-08-05 (row 2 `btnNextStep_Click` 由「❌ 故意捨棄」改「✅ 已實作（部分）」：被捨棄的只有兩步驟切換，97-111「//代入新增」段〔姓名填進搜尋框→跑搜尋→選中來源列〕已補回；總覽 ✅ 28→29、❌ 6→5，覆蓋率仍 100%。代入新增改走 `applyBelieverRow`＝與「點結果列」同一段邏輯，順帶修掉 row 34 記載的三處落差之一「預繳未帶」〔`prefillFromSignup` 先前無 prepayYear/prepayCeremonyCategoryId，`pickBeliever` 有〕，以及地址整段為空時未退回信眾主檔；代入後表單維持 pristine。連帶工具列「新增報名」恰選 1 筆時也走代入〔見 signup-form.md row 3〕。先前 2026-07-31 (row 6 記入「TextAddress fallback 移除」與「堂號清空存空字串」兩項刻意偏離〔舊 244-251 文牒欄留空就抄寄件段，導致取消不掉文牒地址；SignupView 的 COALESCE 會把 null 堂號回退成信眾值〕；同日先前 row 34 預繳歷史改標**刻意偏離**：舊 1102-1115 的預繳反查不分 SignupType，會把法會預繳帶到普桌〔type 4〕；新版改取點到那一列自身的預繳，GET /prepay?believerId&year 保留但無呼叫端。同日先前 row 13「同寄件地址」改標**刻意偏離**：觸發門檻放寬為「城市/區域/地址三者全空才擋」、提示改「請先填寫寄件地址（城市／區域或地址）」；rows 28/34 記明編號欄（cbKeepNumber/txtNumber）只有 PanelFormEmpty 才清、BelieverSelected 不清——新版 2026-07-31 移除改選信眾時的清除後與舊系統 1:1，費用則維持刻意保留的偏離。先前 2026-07-29 (row 6 補回郵遞區號文字快照〔舊 248-250 兩欄都寫，新版原本只寫 FK ID，導致新增的報名郵遞區號空白〕；先前 2026-07-27 row 8 補回表單內「列印資料卡」按鈕（存檔前 disabled、成功後啟用，印剛新增那筆）；同日 row 5 改回 1:1 對齊：編號欄恆顯示、依 keepNumber 切 disabled/enable（先前「勾了才顯示」是自行簡化）；同日 row 6 補齊 btnConfirm 成功後行為 355-361：重查列表→「編號X，新增報名成功」單按鈕 dialog→表單資料留著不關閉；同日先前客訴：選定信眾回填來源改為「點到的那筆報名」快照；同日補回舊 BelieverView 語意——信眾搜尋併查 GET /believers?searchKey=（新參數，14 欄 OR）讓「從未報名過的信眾」也搜得到，rows 3/24 更新、信眾主檔降為 fallback，1:1 對齊舊 BelieverSelected:991-1101 分支語意——rows 4/34 更新；先前 2026-07-17 信眾搜尋改常駐 in-form 列表對齊舊 dgvBelievers；未選信眾自動建信眾由前端 orchestration 補齊——rows 3/4/6/24 與頂部註記更新))))
---

> ✅ **完成 (2026-06-02 交叉稽核)**：核心 POST /signups + 表單編排對齊舊版 + 地址 city/area 連動下拉皆 shipped；剩餘 WinForms 列印內部事件（PrintDocument/EMF/列印對話）統一 ❌ 故意捨棄（改 server-side QuestPDF→PDF + 瀏覽器預覽，與 SignupForm rows 34-37 一致，不受列印 PoC 影響）。
> ⚠️ 已知關鍵段落：
> - 表單驗證 + 編號分配 ✅ 已實作（UPDLOCK + HOLDLOCK 取代舊系統 race window）
> - 避 4 規則 ✅ 已實作（`Domain.Services.AvoidFourFormatter` 純函式）
> - 兩步驟流程（基本資料 → 名單）對應**新版單頁表單**（mockup v4 已決議）；2026-05-29 單頁**欄位編排對齊舊版**（法會→信眾→基本→地址→名單→編號/費用→備註/預繳）
> - **代入新增自動搜尋 + 選中來源列 ✅ 補回（2026-08-05）**：舊 97-111 的「//代入新增」段先前跟著 `btnNextStep_Click` 一起被歸為「故意捨棄」，但該捨棄的只有兩步驟切換本身。現已補回（row 2），並讓代入新增改走 `applyBelieverRow`＝與「使用者點結果列」同一段邏輯，順帶修掉三處落差：預繳未帶、地址整段為空時未退回信眾主檔、`selectedBeliever` 只拿 stub。代入後表單維持 **pristine**（內容全來自系統，關 overlay 不跳未儲存確認）。連帶：工具列「新增報名」在恰好選 1 筆時也走代入（見 [signup-form.md](signup-form.md) row 3），該情境不還原跨路由草稿
> - 地址 city→area 連動下拉 ✅（新增 `GET /zipcodes/cities` + `GET /zipcodes?city=`）+ 同寄件地址 checkbox
> - inline 新建 Believer ✅ 已實作（2026-07-17 補齊）——API 層維持不做（`CreateSignupRequest.BelieverId` 必填），由前端 orchestration：未選信眾送出時 `submit()` 先 `POST /believers`（employeeType=1/isFixedNumber=false 同舊預設）再 `POST /signups`。此前前端漏做（believerId 掛 required），「沒選信眾就無法新增」與舊 `btnConfirm_Click:186-223` 自動建信眾行為不符——已修。inline **編輯**既有信眾屬性仍 ❌ 故意捨棄（於信眾維護調整）；員工類型 + 固定編號於報名表單唯讀顯示
> - 選信眾自動帶入「預繳歷史」+ 固定編號顯示已補（2026-06-02，`BelieverListItem.IsFixedNumber` + `GET /prepay?believerId`）
> - **重複報名警示 ➕ 新版增強（2026-06-30，無對應 legacy 行）**：舊 NewSignupForm 不檢查信眾重複報名；新版選信眾後即時查 `(Year, CeremonyCategoryID)` 同信眾既有報名（忽略 SignupType）跳警示但不阻擋。見 [get-signup-duplicates.md](../api-endpoints/get-signup-duplicates.md)。不影響本表覆蓋率（非 legacy 方法）。
> - **插入並順移 ➕ 新版增強（2026-07-04，無對應 legacy 行）**：舊系統只能自動 `MAX+1` 或手動指定空號（指定已佔用號被擋）；新版於報名維護列表右鍵「在此前插入」，可插入到已佔用編號位並讓其後 `Number ≥ N` 的既有報名 +1 順移（主要用於預繳載入後補插）。見 [post-signups-insert-shift.md](../api-endpoints/post-signups-insert-shift.md)。不影響本表覆蓋率（非 legacy 方法）。
> - **移動並重排 ➕ 新版增強（2026-08-21，無對應 legacy 行）**：舊系統改編號遇已佔用號一律被 `編號重複` 擋下，沒有「移位並讓中間遞補」的路徑；現場的變通是「用『在此前插入』插一筆新的再刪掉原本那筆」，中間必留一個空號。新版於報名維護列表右鍵「移動插入至…」，輸入目標編號即把該筆移過去、中間區段 ±1 自動遞補（總筆數不變）。見 [post-signups-move-number.md](../api-endpoints/post-signups-move-number.md)。不影響本表覆蓋率（非 legacy 方法）。
> - **地址／堂號可整段清空 ⚠️ 刻意偏離（2026-07-31 客訴）**：舊 244-251 的「文牒欄留空就抄寄件段」已移除（空就是空），報名堂號清空改存空字串以避開 `SignupView` 的 COALESCE 回退。詳見 [business-rules-implicit §12.1](../../business-rules-implicit.md)。不影響本表覆蓋率（行為語意變更，行仍有對應）。
> - **地址非必填 ⚠️ 刻意偏離（2026-07-21 客訴）**：舊 `btnConfirm_Click` 驗證寄件地址為必填（空白擋下）；新版依使用者指定改為**非必填**，前端移除 required、後端 `CreateSignupHandler` 與自動建信眾走的 `BelieverWriteValidator` 皆放寬，空白存空字串。詳見 [business-rules-implicit §12](../../business-rules-implicit.md) 與 [signup-management §新增報名頁客訴六項](../signup-management.md)。不影響本表覆蓋率（僅驗證強度變更）。

## 稽核總覽

| 項目 | 值 |
|---|---|
| 總方法 / 事件 / 邏輯區塊數 | 34 |
| ✅ 已實作 | 29 |
| ❌ 故意捨棄 | 5 (WinForms 列印內部事件改 server-side PDF) |
| ⏳ 缺口待補 | 0 |
| 🤔 待確認 | 0 |
| 覆蓋率 | 100% |

## 方法 / 事件 / 邏輯區塊清單

| # | 舊方法/事件 | 舊 code line | 行為摘要 | 新系統狀態 | 對應 endpoint | 備註 |
|---|---|---|---|---|---|---|
| 1 | `NewSignupForm()` constructor | 58-83 | 初始化表單、服務、設定面板 | ✅ 已實作 | 前端 form init | `signup-edit-form.component` constructor：`loadCategories()` + `loadCities()` + create/edit mode effect |
| 2 | `btnNextStep_Click` | 85-112 | 下一步：載入城市、員工類型，代入邏輯 | ✅ 已實作（部分） | 前端 form init + `GET /api/v1/signups`（searchKey） | **兩步驟切換捨棄**（85-96 的 `PanelFilterSwitch`/`PanelFormSwitch`/`PanelFormEmpty`/`LoadCity`/`LoadEmployeeType`）——新版單頁表單（mockup v4）無「下一步」流程，這些在 constructor 就做完。**97-111「//代入新增」段 2026-08-05 補回**：`prefillFromSignup` 取得來源報名後，把姓名填進搜尋框（舊 `txtQ.Text = ParamName`）→ 跑一次信眾搜尋（舊 `LoadBelievers()`）→ 在結果中找 `r.id === fromSignupId` 的列（舊走訪 `ColSignupID == ParamSignupID`）→ `applyBelieverRow(source, { markDirty: false })`（舊 `dgvRow.Selected = true` + `BelieverSelected`）。此前只做 patchValue，搜尋框空白、結果表不渲染、無高亮列，使用者無法就地改選同信眾的別筆報名。姓名為空則完全不發搜尋（同舊 `if(ParamName != string.Empty)`），改走 fallback `applyPrefillItem`。**加值偏離**：來源列若被 200 列上限切掉會被 pin 到結果最前（舊系統無列數上限故無此問題）|
| 3 | `btnBelieverSearch_Click` | 114-124 | 驗證搜尋條件後查詢信眾 | ✅ 已實作 | `GET /api/v1/signups`（`searchKey`+4 scope flags） | signup-edit-form 信眾**常駐 in-form 搜尋**（2026-07-17 改：搜尋列+結果列表直接常駐於表單頂部，對齊舊 txtQ+dgvBelievers 常駐面板型態，取代 modal picker；最多 render 前 200 列+總數提示防 DOM 卡頓）。搜尋語意沿用 2026-07-02：單一輸入框 OR 比對 Name/Phone/6組陽上/6組往生共 14 欄，按鈕/Enter 觸發。2026-07-27：同一把關鍵字**並行**再查 `GET /api/v1/believers?searchKey=`，補回舊 BelieverView 才有的「未報名過的信眾」（見 row 24） |
| 4 | `dgvBelievers_CellClick` | 126-137 | 選擇信眾行並加載其資料 | ✅ 已實作 | 前端 row select + `GET /api/v1/believers/{id}` | `pickBeliever` 選定後預填表單（基本資料 + 地址 city/area + 陽上/往生名單 + 備註）；2026-07-17：選定後**列表保留**、選定列高亮，可隨時再點別筆改選覆蓋（同舊 CellClick 重跑 BelieverSelected）；**2026-07-27 客訴修正**：帶入來源改為**該列自身的報名快照**（`SignupListItem`），`/believers/{id}` 降為欄位為空時的 fallback（同舊 BelieverSelected 分支語意，見 row 34） |
| 5 | `cbKeepNumber_CheckedChanged` | 139-149 | 切換編號手動輸入啟用狀態 | ✅ 已實作 | 前端 form logic | **2026-07-27 起 1:1 對齊**：編號欄恆顯示，`keepNumber` 未勾＝`disabled`、勾選＝`enable`（`syncCustomNumberEnabled()`）；先前新版做成「勾了才顯示欄位」屬自行簡化，已改回 |
| 6 | `btnConfirm_Click` | 151-362 | **複合邏輯：表單驗證 + 編號分配 + 新增報名**（211 行核心方法；186-223 未選信眾時自動 INSERT Believers） | ✅ 已實作 | `POST /api/v1/signups`（未選信眾時前端先 `POST /believers`） | `CreateSignupHandler` + `SignupRepository.InsertWithLogAsync` 含 UPDLOCK + HOLDLOCK + transaction + 同步寫 SignupLog；行為改善（舊系統無 lock，有 race window）。自動建信眾分支＝前端 orchestration（2026-07-17 補齊，見頂部註記）。**成功後行為 2026-07-27 補齊對齊 355-361**：先重查列表（舊 `signupForm.LoadSearchSignups()`）→ 跳單按鈕結果 dialog「編號{number}，新增報名成功」（舊 `CustomMessageForm`）→ **表單資料原樣留著、不關閉**（舊 `btnConfirm/btnPrintDataCard.Enabled = true`）；新版另清掉跨路由草稿記憶。**2026-07-29 補回郵遞區號文字快照**：舊 248-250 除了 `MailZipcodeID` 還寫 `signup.MailZipcode/TextZipcode` 文字（`SignupView` 曝的郵遞區號讀的正是這兩欄），新版原本只寫 FK → 新增的報名郵遞區號一律空白；現於 INSERT 由 FK 現查補上。**2026-07-31 移除 TextAddress fallback ⚠️ 刻意偏離**：舊 244-251 在文牒欄為空時把寄件地址／區號抄過去，導致「取消文牒地址」永遠取消不掉（客訴）；新版空就是空，改由 row 13 的「同寄件地址」checkbox 實際填值。同次修正堂號清空改送空字串（`SignupView.HallName` 的 COALESCE 會把 null 回退成信眾堂號）。見 [business-rules-implicit §12.1](../../business-rules-implicit.md) |
| 7 | `btnCancel_Click` | 364-369 | 返回第一步並清空表單 | ✅ 已實作 | 前端 form reset | overlay 關閉 / `form.reset` + dirty 確認（form-overlay） |
| 8 | `btnPrintDataCard_Click` | 371-404 | 列印剛新增報名的資料卡 | ✅ 已實作 | `GET /api/v1/reports/datacard` | **2026-07-27 補回表單內按鈕**：新增模式表單底部常駐「列印資料卡」，存檔前 disabled、新增成功後啟用，印的是剛新增那筆（`lastCreatedSignupId` ＝舊 `CurrentSignupID`），PDF 開新分頁預覽——對齊舊 Enabled 切換（:95 false → :361 true）。仍**不 auto-print**（舊系統也是按鈕觸發）；既有報名的列印走報名維護右鍵選單 |
| 9 | `dlMailCity_SelectedIndexChanged` | 406-424 | 更新郵寄區域下拉清單 | ✅ 已實作 | `GET /api/v1/zipcodes?city=` | `onCityChange('mail')` 載入該城市區域（區域 option value=ZipcodeID） |
| 10 | `dlMailZone_SelectedIndexChanged` | 426-439 | 填入郵寄郵遞區號 | ✅ 已實作 | 前端 cascading | `onAreaChange('mail')` 顯示選定區域的郵遞區號（read-only） |
| 11 | `dlTextCity_SelectedIndexChanged` | 441-460 | 更新簽署區域下拉清單 | ✅ 已實作 | `GET /api/v1/zipcodes?city=` | `onCityChange('text')` |
| 12 | `dlTextZone_SelectedIndexChanged` | 462-475 | 填入簽署郵遞區號 | ✅ 已實作 | 前端 cascading | `onAreaChange('text')` |
| 13 | `cbSameMailAddress_CheckedChanged` | 477-502 | 複製郵寄地址到簽署地址或清空 | ⚠️ 刻意偏離 | 前端 form logic | `onSameMailAddressChange`：勾選複製 mail→text；取消勾選清空文牒（同舊）。**觸發門檻 2026-07-31 依使用者指定放寬**：舊版要求 `txtMailAddress` 非空，新版改為**城市/區域/地址三者全空**才擋、提示改「請先填寫寄件地址（城市／區域或地址）」（舊 verbatim「請先輸入寄件地址」不再使用）。理由：地址自 2026-07-21 起非必填，只選城市/區域是合法狀態。見 [business-rules-implicit §12](../../business-rules-implicit.md) |
| 14 | `txtYear_Validating` | 504-518 | 驗證年份格式及範圍 (須 ≥ 當年) | ✅ 已實作 (部分) | `POST /api/v1/signups` | API 收 int；regex/notInPast guard 留前端 |
| 15 | `txtPhone_Validating` | 520-551 | 驗證電話格式 (0 開頭) | ✅ 已實作 (部分) | `POST /api/v1/signups` | 全→半形轉換在 handler；regex 留前端 |
| 16 | `txtNumber_Validating` | 553-574 | 驗證編號格式及重複性 | ✅ 已實作 | `POST /api/v1/signups` | `NumberExistsAsync` 重複檢查 + verbatim「編號重複，請重新確認！」訊息 |
| 17 | `txtFee_Validating` | 576-584 | 驗證費用格式 (數字) | ✅ 已實作 (部分) | `POST /api/v1/signups` | API 收 int?；前端 input mask |
| 18 | `txtPrepayYear_Validating` | 586-614 | 驗證預繳年份格式並載入法會 | ✅ 已實作 (部分) | validator + cascading | 同 rows 14/15/17：預繳年份/法會欄存在；regex/notInPast guard 留前端 |
| 19 | `LoadCeremony1()` helper | 616-624 | 載入主法會下拉清單 | ✅ 已實作 | `GET /api/v1/categories` | 法會分類下拉（`flatCategories`） |
| 20 | `LoadSignupType()` helper | 626-660 | 載入報名類型清單 (5 類) | ✅ 已實作 | enum（前端 `SIGNUP_TYPES`） | 報名類型下拉 |
| 21 | `LoadCity()` helper | 662-677 | 載入城市下拉清單 | ✅ 已實作 | `GET /api/v1/zipcodes/cities` | `ZipcodeRepository.GetCitiesAsync`（GROUP BY City ORDER BY City，對齊舊；未過濾 IsDisplay） |
| 22 | `LoadEmployeeType()` helper | 679-703 | 載入員工類型清單 (3 類) | ✅ 已實作 | `GET /api/v1/believers` | 員工類型 + **固定編號**於 signup 表單**唯讀顯示**（`employeeTypeTitle` / `isFixedNumber`）；inline 編輯信眾屬性故意捨棄（新流程不於報名改信眾主檔，於信眾維護調整）。`BelieverListItem` 已補 `IsFixedNumber`（2026-06-02）|
| 23 | `LoadPrepayCeremony()` helper | 705-713 | 載入預繳法會下拉清單 | ✅ 已實作 | `GET /api/v1/categories` | 預繳法會下拉（共用 `flatCategories`） |
| 24 | `LoadBelievers()` helper | 715-734 | 查詢並顯示信眾清單（資料源 `BelieverView` = Believers LEFT JOIN Signups，**含從未報名過的信眾**） | ✅ 已實作 | `GET /api/v1/signups` + `GET /api/v1/believers?searchKey=` | 常駐 in-form 結果清單（2026-07-17 改：由 modal picker 移回表單頂部常駐顯示，同舊 dgvBelievers；16 欄表格、每報名一列、1:1 對齊舊可見欄位；報名列最多 render 前 200 列）。**2026-07-27 補齊 BelieverView 語意**：前端兩路併查後合併——`/signups` 出報名列，`/believers?searchKey=`（新加參數，14 欄 OR）補「未報名過的信眾」列（報名欄位留空、接在最後、獨立額度 50 列），不新增後端 view/端點 |
| 25 | `GetNumberText()` helper | 736-751 | **避 4 規則** (個位 4 → "3-1") | ✅ 已實作 | `Domain.Services.AvoidFourFormatter` | 純函式，11 個 case xUnit 覆蓋；display only，DB 仍存 int |
| 26 | `PanelFormSwitch()` helper | 753-793 | 切換表單面板控制項狀態 | ✅ 已實作 | 前端 form mode | Angular form mode / overlay state（create vs edit） |
| 27 | `PanelFilterSwitch()` helper | 795-817 | 切換篩選面板控制項狀態 | ✅ 已實作 | 前端 form mode | 同上（單頁表單無獨立篩選面板，狀態由 form mode 控制） |
| 28 | `PanelFormEmpty()` helper | 819-859 | 清空所有表單欄位 | ✅ 已實作 | 前端 form reset | `reset()`（create 模式初始化）＋ `resetBelow()`（按「取消」＝清成新的一筆）。舊 :853-854 清 `cbKeepNumber`/`txtNumber`，`resetBelow()` 同樣清（**只有這裡清，改選信眾不清**，見 row 34）；**唯一偏離**：費用刻意保留（2026-07-28 使用者指定，同場法會連續輸入金額多半固定） |
| 29 | `PrintDataCard()` helper | 861-911 | 列印資料卡 (RDLC 渲染) | ❌ 故意捨棄 | – | server-side QuestPDF→PDF 取代 WinForms RDLC 渲染（與 SignupForm row 35 一致）；資料卡輸出邏輯在 `GenerateDataCardHandler` |
| 30 | `CreateStream()` helper | 914-919 | 建立列印用記憶流 | ❌ 故意捨棄 | – | WinForms 列印 stream 內部；web/PDF path 不需 |
| 31 | `BeginPrint()` event | 921-924 | 初始化列印頁面索引 | ❌ 故意捨棄 | – | WinForms PrintDocument 事件；web/PDF path 不需 |
| 32 | `PrintPage()` event | 927-952 | 繪製列印頁面 (EMF → 影像) | ❌ 故意捨棄 | – | WinForms EMF 繪製；改 server-side PDF |
| 33 | `printPreview_PrintClick` | 954-989 | 啟動列印對話 (含紙張設定) | ❌ 故意捨棄 | – | WinForms 列印對話；改瀏覽器 PDF 預覽（reports preview 頁） |
| 34 | `BelieverSelected()` helper | 991-1116 | **複合邏輯：填入信眾資料 + 地址 + 預繳歷史** (125 行) | ✅ 已實作 | `GET /api/v1/believers`（picker）+ `GET /api/v1/prepay?believerId` | **2026-08-05 起實作抽成 `applyBelieverRow(row, { markDirty })`**，`pickBeliever`（使用者點列，標髒）與代入新增（系統帶入，維持 pristine）共用同一段；代入新增先前自己 patchValue、漏帶預繳且地址不退回信眾主檔，現已一致。以下敘述的「`pickBeliever`」即指這段共用邏輯。`pickBeliever` 預填姓名/電話/堂號/員工類型/固定編號/寄件+文牒地址（city/area cascade）/陽上+往生名單/備註。**2026-07-27 起 1:1 對齊舊分支語意**：姓名/電話/兩組地址/名單/備註取**點到那筆報名**（舊 `signup != null ? signup.X : dgvRow/believer.X`；名單與備註舊系統本就直接取 grid row），該欄為空才退回 `GET /believers/{id}` 主檔；堂號/員工類型/固定編號取該筆值（`SignupView` COALESCE 回退信眾，等效舊系統一律取 believer）。此前一律以主檔覆蓋，是與舊系統的實質落差（已修）。**費用與編號欄（`cbKeepNumber`/`txtNumber`）刻意不動**——舊 BelieverSelected 從頭到尾沒碰這三個控件，只有 `PanelFormEmpty()`（row 28）才清；新版 2026-07-27 起不清費用、2026-07-31 起不清編號勾選與數字，至此與舊系統 1:1；**預繳歷史（舊 1102-1115）2026-07-31 起刻意偏離**：舊碼反查「該信眾今年以前最新一筆」且**完全不分 `SignupType`**，同一位信眾點法會列與普桌列（type 4）都拿到同一份 → 普桌沿用法會預繳（客訴）。新版改取**點到那一列自身**的 `prepayYear` / `prepayCeremonyCategoryId`，與本 row 其他欄位同一規則；`GET /prepay?believerId&year` 保留但已無呼叫端（blueprint：[get-prepay-believer-latest.md](../api-endpoints/get-prepay-believer-latest.md)、規則：[business-rules-implicit §19](../../business-rules-implicit.md)）|
