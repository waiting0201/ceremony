---
title: 報名堂號隔離（修正「改一筆堂號連動同信眾全部報名」）
purpose: 評估並修正「編輯報名改堂號會回寫共用 Believer，導致同信眾名下其他報名堂號被連動修改」的 legacy 缺陷
status: done
applicable_when: 要修改報名/信眾堂號寫入邏輯、要理解 HallName 為何被共用、評估是否讓 Signups 擁有自有堂號欄位時
related_agents:
  - software-architect-blueprint
  - backend-engineer
related_docs:
  - ../design/database-design.md
  - ../design/backend-design.md
  - ../design/api-design.md
  - ./data-migration.md
  - ./signup-management.md
  - ../business-rules-implicit.md
  - ../glossary.md
keywords: [堂號, HallName, 信眾連動, 資料隔離, 代入新增, 編輯報名, Believers, Signups]
last_updated: 2026-06-29
---

## 決議與實作（2026-06-29）

**業務定案**：堂號為**信眾層級**屬性（同信眾跨報名一致）→ 採**方案 C**（零 schema，停止回寫）。已實作：

- 後端 `UpdateWithLogAsync` 移除整段 Believer 更新 + 三個 `*ForBeliever` 參數（[ISignupRepository.cs](../../backend/src/Ceremony.Application/Signups/ISignupRepository.cs)、[SignupRepository.cs](../../backend/src/Ceremony.Infrastructure/Repositories/SignupRepository.cs)、[UpdateSignupHandler.cs](../../backend/src/Ceremony.Application/Signups/UpdateSignupHandler.cs)）；報名編輯從此絕不碰 Believer。堂號仍寫 `SignupLogs` 快照。
- 前端報名表單堂號改唯讀（比照員工類型/固定編號），值取自 `selectedBeliever()`；移除 `hallName` form control（[signup-edit-form.component.ts](../../frontend/src/app/features/signups/signup-edit-form.component.ts) / `.html`）。
- 回歸測試 `UpdateSignupHandlerTests.Edit_never_writes_back_to_Believer`（+5 其他）；後端 291+6 測試綠、前端 ng build 綠。
- 堂號維護單一入口：信眾維護頁（[believer-management.md](./believer-management.md)）。

> 下方原評估內容保留作決策脈絡。方案 A（Signups 加自有 HallName 欄）未採用——業務確認堂號信眾固有、不因報名而異，且 A 撞正式 DB 凍結。

## 背景與動機

使用者回報：舊系統「報名維護頁編輯某筆報名、修改堂號」後，**同一信眾名下其他報名的堂號也一起被改**。需確認新版是否重演。

### 根因（已確認，新版完整保留）

堂號（`HallName`）在資料模型上**不是報名（Signups）自己的欄位**，而是掛在**信眾（Believers）**身上的單一共用欄位。畫面/清單的堂號是透過 `SignupView` JOIN `Believers` 即時帶出的（非報名列自身的值）。

- `Believers.HallName` nvarchar(10) — 真正的儲存位置（[database-design.md](../design/database-design.md) §4，line 218）
- `Signups` 表**無 HallName 欄位**（[database-design.md](../design/database-design.md) §5，line 236-263）
- `SignupView.HallName` ← JOIN Believers（[database-design.md](../design/database-design.md) §7，line 300）
- `SignupLogs.HallName` — 僅 audit 快照（每次存檔寫一筆當下值）

新版 `UpdateSignupHandler` 編輯報名時，第一步就把表單堂號寫回共用的 Believer：

```csharp
// SignupRepository.cs:209-223（UpdateWithLogAsync 第 1 步）
UPDATE dbo.Believers SET
  HallName = COALESCE(@HallName, HallName), ...
WHERE BelieverID = @BelieverId
```

呼叫端 [UpdateSignupHandler.cs:108-113](../../backend/src/Ceremony.Application/Signups/UpdateSignupHandler.cs#L108-L113) 一律把表單堂號傳入 `hallNameForBeliever`。doc comment（同檔 line 9）說明這是「對齊 legacy EditSignupForm」的刻意設計，但後果即為使用者描述的缺陷。

### 兩條路徑現況（重要：缺陷只在「編輯」）

| 動作 | Handler / Repo | 是否回寫 Believer.HallName | 對其他報名的影響 |
|---|---|---|---|
| **代入新增** → 存成新報名 | `CreateSignupHandler` → `InsertWithLogAsync` | **否**（堂號只進 SignupLog 快照） | 無連動 ✅ |
| **編輯既有報名** → 改堂號 | `UpdateSignupHandler` → `UpdateWithLogAsync` | **是**（[SignupRepository.cs:209-223](../../backend/src/Ceremony.Infrastructure/Repositories/SignupRepository.cs#L209-L223)） | **同信眾全部報名連動** ❌ |

### 附帶資料正確性問題

因 `Signups` 無自有 HallName 欄位、且 Create 不回寫 Believer：**代入新增時使用者改的堂號其實不會落在新報名上**——新報名清單顯示的是該信眾「目前」的堂號。等於新增情境下堂號是唯讀的（只進 log）。是否符合預期需業務確認（見下方「未解問題」與 [pending-business-input.md](../pending-business-input.md) B13）。

## 範圍

### 做什麼
- 釐清並文件化「堂號到底是信眾層級還是報名層級」這個業務語意（這是選方案的前提）
- 依語意選定修法方案（見下方 3 選項），停止「編輯一筆報名改到他人堂號」的連動
- 同步更新 database-design / backend-design / glossary / business-rules-implicit / legacy-coverage

### 不做什麼
- 不在本 blueprint 直接改 code（本份為評估；定案後再開實作）
- 不更動 `SignupLogs` 既有快照行為（audit 一律記當下值）
- 不處理列印端堂號拆字（`HallNameSplitter`）邏輯——只改「值從哪來、寫到哪」

## 設計決策

### 前提問題（必先回答）
**堂號是「信眾」的屬性，還是「每筆報名」的屬性？**

- 若**信眾層級**（一個信眾恆定一個堂號）→ 現行共用模型語意正確，連動是「特性」而非 bug；問題退化為 UX（編輯報名時不該讓人以為改的是這一筆）。
- 若**報名層級**（同一信眾不同年度/法會可掛不同堂號）→ 必須讓 `Signups` 擁有自有堂號，現行回寫是真 bug。

> 使用者回報「會改到其他資料」即視此連動為**非預期**，傾向報名層級；但需業務拍板，因為兩者修法與 DB 影響差異大。

### 方案比較

| 方案 | 作法 | DB 變更 | 連動消除 | 新增可存堂號 | 風險 |
|---|---|---|---|---|---|
| **A（報名層級，推薦）** | `Signups` 新增 `HallName` 欄位；Create/Update 都寫 Signups 自己的列；**停止**回寫 Believers.HallName；`SignupView` 改讀 `Signups.HallName`（或讀取改走 Signups 直欄） | 需加欄 + 改 view | ✅ | ✅ | 撞「正式 DB 凍結」政策；需資料回填（見下） |
| **B（信眾層級，零 schema）** | 維持共用模型；編輯報名時堂號改唯讀或明確標示「此為信眾共用堂號，修改將套用到該信眾所有報名」；移除「逐筆編輯」的錯覺 | 無 | 連動保留但「告知後同意」 | ❌（仍信眾層級） | 無法滿足「同信眾不同堂號」；只是把 bug 變成明示行為 |
| **C（折衷）** | 編輯報名**不再**回寫 Believer（移除 `UpdateWithLogAsync` 第 1 步的 HallName 寫入）；堂號改由「信眾維護頁」單一入口維護 | 無 | ✅（報名頁不再改到他人） | ❌（報名頁無法改堂號） | 報名頁失去改堂號能力；需確認流程可接受 |

**推薦**：若業務確認堂號可因報名而異 → **方案 A**；若堂號恆為信眾固有 → **方案 C**（最小變更即可止血，堂號集中到信眾維護）。方案 B 僅為過渡。

### 「正式 DB 凍結」限制（方案 A 關鍵）
[data-migration.md](./data-migration.md) 載明正式 DB 凍結。方案 A 需要 `ALTER TABLE dbo.Signups ADD HallName` + 改 `SignupView`，屬 schema 變更，**須 DBA / 業務核可解除凍結或排維護窗**。並需決定歷史資料回填策略：既有 Signups 的 HallName 應從對應 Believer 帶入（一次性 `UPDATE ... FROM Believers`），或從各筆 SignupLogs 最後一筆快照回填。

## 跨層影響

| 層級 | 是否影響 | 變動摘要 |
|---|---|---|
| 視覺 | 否 | 欄位佈局不變 |
| 前端 | 視方案 | A：無；B：編輯表單堂號改唯讀/加提示；C：報名表單移除堂號輸入或標示「至信眾維護修改」 |
| 後端 | 是 | A：`SignupWriteModel` 加 HallName、Insert/Update SQL 寫 Signups、移除 `hallNameForBeliever` 回寫；C：移除 `UpdateWithLogAsync` 第 1 步 HallName 寫入 |
| API | 視方案 | 契約 `HallName` 既有；語意從「信眾欄」變「報名欄」（A）或「唯讀/移除」（C），需更新 [api-design.md](../design/api-design.md) |
| 資料庫 | 視方案 | A：`Signups` 加 `HallName` 欄 + 改 `SignupView` + 歷史回填（受凍結限制）；B/C：無 schema 變更 |
| 基礎建設 | 否 | – |
| 安全 | 否 | 堂號非敏感 PII 升級 |

## 驗收標準

- [ ] 業務已回答「堂號是信眾層級或報名層級」（回填 [pending-business-input.md](../pending-business-input.md) B13 與 [glossary.md](../glossary.md)）
- [ ] 編輯 A 信眾某筆報名的堂號後，**同信眾其他報名堂號不變**（方案 A/C）；或 UI 已明確告知將套用全部（方案 B）
- [ ] 代入新增改堂號後存檔，新報名顯示的是使用者輸入值（方案 A）或行為已明確定義（B/C）
- [ ] `glossary.md` 對堂號儲存位置的描述修正為正確（目前誤寫 `Signups.HallName`）
- [ ] [legacy-coverage/edit-signup-form.md](./legacy-coverage/) 對應行重新標註（行為已刻意偏離 legacy）
- [ ] 通過 [code-review](../workflows/code-review.md) 與 [qa-testing](../workflows/qa-testing.md)

## 風險與未解問題

- **業務語意未定**：堂號信眾層級 vs 報名層級 → 決定方案 A/C，未答前不動 code（B13）。
- **正式 DB 凍結**：方案 A 需解凍 + 歷史回填，須 DBA 核可。
- **legacy parity 取捨**：本修法**刻意偏離** `EditSignupForm` 原行為；需在 commit message 與 legacy-coverage 註明「已知且故意」。
- **代入新增堂號目前存不進去**：屬同源資料正確性問題，方案 A 一併解決；B/C 需另行說明預期行為。
- **glossary 既有錯誤**：`Believers.HallName / Signups.HallName` 並列，但 `Signups.HallName` 不存在——本份順手修正。

## 參考資料

- 2026-06-29 與使用者對話：確認新版仍重演 legacy 連動缺陷，使用者要求評估修法
- Legacy：[EditSignupForm.cs:225-231](../../reference/old/Ceremony/EditSignupForm.cs#L225-L231)（`believer.HallName = txtHallName.Text` 回寫）
- 現況：[SignupRepository.cs:205-223](../../backend/src/Ceremony.Infrastructure/Repositories/SignupRepository.cs#L205-L223)、[UpdateSignupHandler.cs:108-113](../../backend/src/Ceremony.Application/Signups/UpdateSignupHandler.cs#L108-L113)
