---
title: 業務術語 Glossary
purpose: 法會報名系統業務術語對照表 — 中英文/DB欄位/宗教文化背景/系統用法/常見誤解
applicable_when: 新成員入門、解讀業務需求、寫文件遇到名詞不確定時
related_agents:
  - software-architect-blueprint
  - system-analyst
related_docs:
  - design/database-design.md
  - blueprints/signup-management.md
  - blueprints/believer-management.md
  - blueprints/printing-reports.md
keywords: [glossary, 業務術語, 法會, 信眾, 陽上, 往生, 文牒, 薦牌, 普桌, 觀音會, 寺方, 郵撥, 民國年, 避4]
last_updated: 2026-05-26
---

> 本文依舊系統 v1.3.0 分析文件 + 原始碼整理；目的是讓新團隊成員（特別是非台灣 / 非佛道文化背景）能正確理解業務語意。

## 法會類

### 法會 / Ceremony

| 欄位 | 對應 |
|---|---|
| 中文 | 法會 |
| 英文 | Ceremony |
| DB | `CeremonyCategorys.Title`，被 `Signups.CeremonyCategoryID` 引用 |

**業務意義**：寺廟舉辦的宗教法事活動。本系統核心組織單位，每筆報名綁定一個法會。

**宗教背景**：佛教/道教集體誦經、做功德或祈福的儀式。

**系統使用**：`CeremonyCategorys` 自我參照樹（兩層）；每筆 Signup 必須有 `CeremonyCategoryID`。

**常見誤解**：「法會分類」與「報名類型」不同 — 法會是**時間/活動維度**（哪場法會），報名類型是**身份維度**（以什麼身份參加）。

### 法會分類 / CeremonyCategory

| | |
|---|---|
| DB 表 | `CeremonyCategorys`（注意拼字 quirk：少了 ie/y） |
| PK | `CeremonyCategoryID` (GUID) |

兩層樹：根節點 → 法會 → 子法會。刪除限制：該分類下無 signups 且無子分類才可刪。

三個固定 GUID：
- 春季：`18927907-dcad-42b2-8f2a-635c2e0fa98d`
- 中元：`0c478f0e-787c-448e-ba7b-b1579f3f1fce`
- 秋季：`3864e4dc-24db-4544-acb3-3351592f6dab`

### 觀音會 / Avalokitesvara Association

`Signups.SignupType = 3`，`NumberTitle = "觀"`，編號如「觀35」。

**宗教背景**：觀音菩薩信仰組織。觀音是東亞民間信仰中極受尊崇的菩薩。

### 寺方 / Temple (Official)

`Signups.SignupType = 2`，`NumberTitle = "寺"`。

**特殊規則**：寺方報名顯示時**只顯示「寺」，不附數字編號**（SignupForm.cs line 302）。

「寺方」可由任何 `EmployeeType` 的人以寺廟名義報名 — 與「寺方人員」（員工身份）是不同維度。

### 普桌 / Worship Table

`Signups.SignupType = 4`，`NumberTitle = "普"`。

**宗教背景**：法會中大眾共享祭祀餐桌的方式。

列印有專用模板（tmpWorship + 5 變體）；「列印普桌」menu 僅在 `dlSearchSignupType.SelectedValue == 4` 啟用。

### 郵撥 / Mail / Postal Payment

`Signups.SignupType = 5`，`NumberTitle = "郵"`。

透過郵政劃撥繳費的遠端報名。預繳載入時分兩 case：
- Case 5：郵撥大殿員工（SignupType=5 + EmployeeType=2）
- Case 6：郵撥非員工（SignupType=5 + EmployeeType=1）

### 一般 / General

`Signups.SignupType = 1`，`NumberTitle = "No"`，編號如「No.12」。

最常用的報名類型。預繳載入時分兩 case：
- Case 1：非員工一般（EmployeeType=1）
- Case 2：地藏殿員工一般（EmployeeType=3）

---

## 人員類

### 信眾 / Believer

| | |
|---|---|
| DB 表 | `Believers` |
| PK | `BelieverID` (GUID) |

宗教信徒。**人員主檔**；與 Signups 為 1:N（一信眾可多次報名）。刪除限制：有報名紀錄則不可刪。

**常見誤解**：「信眾」不等於「報名」— 報名是**人員 + 某場法會**的快照。Signups 表中的 Name/Phone 是報名當下的快照，**不會隨 Believers 更新**。

### 管理員 / Admin

| | |
|---|---|
| DB 表 | `Admins` |
| PK | `AdminID` (int identity) |

系統操作人員。**無權限分級**，所有管理員權限相同。登入優先檢查硬編碼後門 `weypro/weypro12ab`（AdminID=0），再查 DB。

### 員工類型 / EmployeeType

`Believers.EmployeeType` — int 列舉：

| ID | 名稱 | 說明 |
|---|---|---|
| 1 | 非員工 | 一般信眾 |
| 2 | 大殿 | 大殿員工 |
| 3 | 地藏殿 | 地藏殿員工 |

> **與 SignupType 是兩個獨立維度**。一個「非員工」可以以「寺方」名義報名；一個「大殿員工」可以以「郵撥」方式報名。預繳載入的 6 case 把兩維度交叉分組。

### 陽上 / Living Name

| | |
|---|---|
| DB 欄位 | `LivingNameOne` ~ `LivingNameSix`（在 Believers 與 Signups 各 6 欄） |

**被祝福的活人姓名**。代表信眾自己或現世家人安康。

**宗教背景**：「陽上」對「往生」是宗教祭祀的重要二分法。陽上＝現世祈福；往生＝超度亡靈。

UI 欄位 label 顯示「陽上 1 / 2 / 3 / **3-1** / 5 / 6」— 第 4 欄用「3-1」替代避「4」（**只是 label，欄位名仍是 LivingNameFour**）。

### 往生 / Dead Name

`DeadNameOne` ~ `DeadNameSix`，最多 6 位。已故親族的姓名，用於超度祈禱。

UI label 同樣用「3-1」替代「4」。

### 堂號 / Hall Name

`Believers.HallName` / `Signups.HallName` — 2-4 字的信眾組織名稱或暱稱（例：「慈光堂」「南無堂」）。

列印時：
- 2 字 → 拆為 First + Second（各 1 字）
- 4 字 → 拆為 First + Second（各 2 字）
- 用在牌位、文牒、普桌的上下兩行排列

---

## 編號類

### NumberTitle / 編號標題

`Signups.NumberTitle` (nvarchar(5))

由 `SignupType` **自動推導，不可手動覆寫**：

| SignupType | NumberTitle |
|---|---|
| 1 一般 | `No` |
| 2 寺方 | `寺` |
| 3 觀音會 | `觀` |
| 4 普桌 | `普` |
| 5 郵撥 | `郵` |

獨立儲存於 Signups 表的原因：SignupLogs 反正規化需要、DB View 直接讀取免轉換。

### 編號 / Number

`Signups.Number` (int nullable，但實質 NOT NULL)

`(Year, CeremonyCategoryID, SignupType)` 三元組下的序號。由 `Library.GetSignupNumber()` 取 MAX+1 生成；**不跳過 4**。

唯一性：`(Year, CeremonyCategoryID, SignupType, Number)` 應實質唯一（應用層 enforce，DB 無 unique constraint）。

### 固定編號 / IsFixedNumber

`Believers.IsFixedNumber` (bit)

旗標：信眾在預繳載入時是否保留原編號。

- 固定編號：載入預繳時原 Number 直接複製到新年度
- 非固定編號：依序填補固定編號留下的空號，再續序

**常見誤解**：「固定編號」不代表編號永不變 — 編輯報名時仍可改 Number；只是在 LoadPrepay 時優先保留。

### 避 4 / Avoid Number 4

宗教避諱「4」（諧音「死」）。

**僅顯示轉換，DB 存實值**。`GetNumberText()` 邏輯：
- 取個位 → 若為 4 → 顯示成 `3-1`
- 例：`4` → `3-1`、`14` → `13-1`、`44` → `43-1`
- **只避個位**：`40` 仍顯示 `40`、`400` 仍顯示 `400`、`140` 仍顯示 `140`

不影響 LoadPrepay 填補空號（基於實際 Number 值）。

---

## 文件類

### 資料卡 / DataCard

PrintType=1，模板 `tmpDataCard.rdlc`。A5 橫式（21×14.8cm）。含簽名欄「確認無誤請簽名」。

### 收據 / Receipt

PrintType=2，模板 `tmpReceipt.rdlc`。A4（21×29.7cm），Tablix 高 59.4cm 雙聯（收據聯 + 存根聯）。

### 薦牌 / Tablet

PrintType=3，**9 個 RDLC 變體**（依 DeadName / LivingName 數量自動選），窄長型（11.5×25.4cm）。

紙質牌位記錄；用來寫在實體牌位上或作為靈位記錄。

### 文牒 / Text Document（疏文）

PrintType=4，模板 `tmpText.rdlc` + `tmpTextTwo.rdlc`。超寬 Landscape（36.5×26.2cm）。

**宗教背景**：「文牒」「疏文」「牒文」是道教/佛教中**通知神明的正式文書**。內容包括信眾資訊、祈求事項、簽署者。

特殊設計：含**垂直地址圖片**（`PhotoAddress` byte[]），由 `Library.DrawText()` 將地址轉成 25×605px 直書 PNG（標楷體 25px，英數字旋轉 90° 處理）。

### 普桌 / Worship Table 列印

PrintType=5，**6 個 RDLC 變體**（依 LivingName 最高有值位置自動選）。A4（21×29.6cm），含背景圖 `worship2`。姓名用 2-3cm 大字直印。

**只列陽上不列往生**（WorshipViewModel 無 DeadName 欄位）。

### 疏文地址 / Text Address vs 寄件地址 / Mail Address

兩組獨立地址欄位：

| 用途 | 欄位 |
|---|---|
| 寄件（收據/薦牌/資料卡郵寄） | `MailAddress` + `MailZipcodeID` |
| 文牒疏文呈報 | `TextAddress` + `TextZipcodeID` |

「同寄件地址」勾選框自動複製。若疏文空，後端有 fallback 用寄件地址。

---

## 流程類

### 報名 / Signup

| | |
|---|---|
| DB 表 | `Signups` |
| PK | `SignupID` (GUID) |

信眾在某法會上的參加記錄。所有 Name/Phone/Living/Dead/地址欄位都是**快照**（不隨 Believers 更新）。

設計動機：信眾搬家、改電話可能多年才一次；但「該次要寄到哪、寫誰名字」每次可能不同。

### 預繳 / Prepay

`Signups.PrepayYear` + `Signups.PrepayCeremonyCategoryID`

信眾預先繳費參加未來法會。**預繳是報名上的屬性，不是獨立交易記錄**。

### 載入預繳 / Load Prepay

`LoadPrepayForm`。從「來源年+法會」查出有預繳的紀錄，批次建立到「目標年+法會」。

依 **6 case** 分群處理；複製 12 個 Living/Dead Name + 地址 + Fee + 預繳資訊；**不複製 Name 與 Phone**（新 Signup 兩欄為 null，列印時若需姓名從 Believer 取）。

**常見誤解**：載入預繳**不會刪除舊報名**，只新增複製的。

### 變更紀錄 / SignupLog

`SignupLogs` 表，每次新增/編輯 Signup 都建立一筆**完整快照**（反正規化）。

設計：所有關聯資料展開為純文字（如 `CeremonyCategoryTitle` 而非 ID），確保歷史記錄不受後續資料異動影響。

**無 diff 邏輯** — UI 需自行前後比對；舊系統也**無 action 欄位**區分新增/編輯/刪除（依「同 SignupID 第一筆 = 新增」推斷）。

---

## 其他

### 民國年 / Taiwan Year (ROC Year)

中華民國紀年（1912 = 民國 1 年）。`Signups.Year` 為 int 民國年（例：2026 = 民國 115）。

驗證 regex：`^1[0-9]{2}$`（3 位、1 開頭）。系統用 `System.Globalization.TaiwanCalendar` 處理轉換。

### 6 組陽上 / 6 組往生

固定上限：每筆報名最多 6 位陽上 + 6 位往生。寫死在 schema（`LivingNameOne..Six` / `DeadNameOne..Six`）與 UI（6 個 TextBox 各 2 組）。

**可部分填寫**，空欄位留白。

---

## 附錄：常見誤解速查表

| ❌ 誤解 | ✅ 正確 |
|---|---|
| 法會分類 = 報名類型 | 法會是**時間維度**，報名類型是**身份維度** |
| 信眾 = 報名 | 信眾是**人員檔案**，報名是**人員 + 法會**的快照 |
| 編號會跳過 4 | DB 存實值；**只在顯示層**轉 4 → 3-1 |
| 避 4 包含十位/百位 | **只避個位**；40、400、140 都不變 |
| NumberTitle 需人工選 | **完全由 SignupType 自動推導** |
| 寺方人員 = 寺方報名 | 獨立維度（EmployeeType vs SignupType） |
| 固定編號 = 永遠不變 | 只在**載入預繳時優先保留**；編輯可改 |
| 疏文地址 = 郵寄地址 | 獨立欄位，可不同 |
| 預繳是獨立交易 | 預繳是**報名上的屬性**（PrepayYear + PrepayCeremonyCategoryID） |
| 載入預繳會刪除舊報名 | **保留原報名，新增複製** |
| SignupLog 只記錄變更 | **記錄完整快照** |
| 6 組名單必須填滿 | **可部分填寫** |
| Believer.Name/Phone 編輯時會更新 | **不會** — 兩級設計，Signup 級獨立 |
