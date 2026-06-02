# 法會報名系統 - 舊系統完整分析文件

> **系統名稱：** 法會報名系統  
> **版本：** v1.3.0  
> **原始碼位置：** D:\appsystems\Ceremony  
> **技術框架：** .NET Framework 4.8 / Windows Forms / Entity Framework 6  
> **資料庫：** Microsoft SQL Server（192.168.1.151）  
> **分析日期：** 2026-04-01（v1.3.0 補充缺漏）

---

## 目錄

1. [系統概述](#1-系統概述)
2. [專案架構](#2-專案架構)
3. [資料庫設計](#3-資料庫設計)（v1.3.0 補充欄位型別、Nullable、約束）
4. [業務邏輯層](#4-業務邏輯層)（v1.3.0 補充 NumberTitle 邏輯、避 4 確認、交易邊界）
5. [使用者介面](#5-使用者介面)（v1.3.0 補充搜尋組合邏輯、資料帶入優先順序、列印格式對應）
6. [報表與列印](#6-報表與列印)（v1.3.0 補充版面規格）
7. [資料遷移工具](#7-資料遷移工具)
8. [系統相依套件](#8-系統相依套件)
9. [已知問題與安全風險](#9-已知問題與安全風險)
10. [補充細節](#10-補充細節v130-新增)（v1.3.0 新增：Global.cs、App.config、未使用欄位/套件）

---

## 1. 系統概述

法會報名系統是一套 **Windows Forms 桌面應用程式**，用於管理宗教法會的信眾報名、繳費、預繳、名牌列印及收據管理等作業。

### 核心業務功能

| 功能模組 | 說明 |
|----------|------|
| 管理者維護 | 系統管理員帳號的 CRUD（啟用/停用） |
| 信眾維護 | 信眾基本資料管理（堂號、姓名、電話、地址、陽上/往生名單） |
| 報名維護 | 法會報名紀錄查詢、新增、編輯、刪除 |
| 新增報名 | 多步驟表單建立新報名紀錄（選法會→選信眾→填寫資料→儲存） |
| 載入預繳 | 將預繳資料批次載入到指定年份法會 |
| 法會維護 | 法會分類的階層式管理（樹狀結構） |
| 報表列印 | 資料卡、收據、薦牌、文牒、普桌等多種報表列印 |
| 資料備份 | SQL Server 資料庫備份至 D:\Backup\ |

### 應用程式流程

```
程式啟動 (Program.cs)
    ↓
LoginForm（登入驗證）
    ├─ 硬編碼帳號：weypro / weypro12ab
    └─ 資料庫帳號：查詢 Admins 資料表（明文密碼比對）
    ↓
MainForm（主選單）
    ├─ 管理者維護 → AdminsForm
    ├─ 信眾維護 → BelieverForm
    ├─ 報名維護 → SignupForm
    │   ├─ 新增報名 → NewSignupForm
    │   ├─ 編輯報名 → EditSignupForm
    │   └─ 變更紀錄 → SignupLogForm
    ├─ 新增報名 → NewSignupForm
    ├─ 載入預繳 → LoadPrepayForm
    ├─ 法會維護 → CeremonyCategoryForm
    └─ 備份 → SQL Server BACKUP DATABASE
```

---

## 2. 專案架構

### 方案結構（Ceremony.sln）

```
Ceremony.sln
├── Ceremony/                  # 主 WinForms UI 專案 (WinExe)
│   ├── Program.cs             # 進入點
│   ├── MainForm.cs            # 主視窗
│   ├── LoginForm.cs           # 登入
│   ├── AdminsForm.cs          # 管理者維護
│   ├── BelieverForm.cs        # 信眾維護
│   ├── SignupForm.cs          # 報名維護（最大的 Form，含多種列印）
│   ├── NewSignupForm.cs       # 新增報名
│   ├── EditSignupForm.cs      # 編輯報名
│   ├── LoadPrepayForm.cs      # 載入預繳
│   ├── CeremonyCategoryForm.cs# 法會分類維護
│   ├── SignupLogForm.cs       # 報名變更紀錄
│   ├── CustomDialogForm.cs    # 自訂對話框
│   ├── CustomMessageForm.cs   # 自訂訊息框
│   ├── Commons/
│   │   ├── Global.cs          # 全域狀態（登入狀態、使用者、版本號）
│   │   └── Library.cs         # 工具函式（編號產生、垂直文字圖片繪製）
│   ├── tmp*.rdlc              # RDLC 報表範本（19 個）
│   ├── DataCardDataSet.xsd    # 報表資料集
│   └── App.config             # 組態（連線字串）
│
├── Ceremony.Models/           # 資料模型專案 (Library)
│   ├── Model1.edmx            # Entity Framework EDMX 模型
│   ├── Model1.Context.cs      # DbContext (CeremonyEntities)
│   ├── Admins.cs              # 管理者 Entity
│   ├── Believers.cs           # 信眾 Entity
│   ├── Signups.cs             # 報名 Entity
│   ├── SignupLogs.cs          # 報名異動紀錄 Entity
│   ├── CeremonyCategorys.cs   # 法會分類 Entity
│   ├── Zipcodes.cs            # 郵遞區號 Entity
│   ├── BelieverView.cs        # 信眾檢視（DB View）
│   ├── SignupView.cs          # 報名檢視（DB View）
│   ├── Interface/
│   │   ├── IRepository.cs     # 泛型 Repository 介面
│   │   ├── IBaseService.cs    # 基礎 Service 介面
│   │   └── IResult.cs         # 操作結果介面
│   ├── Repository/
│   │   └── GenericRepository.cs # 泛型 Repository 實作
│   └── ViewModels/            # 檢視模型
│       ├── BelieverViewModel.cs
│       ├── BelieverTypeViewModel.cs
│       ├── EmployeeTypeViewModel.cs
│       ├── SignupTypeViewModel.cs
│       ├── PrintTypeViewModel.cs
│       ├── DataCardViewModel.cs
│       ├── ReceiptViewModel.cs
│       ├── TabletViewModel.cs
│       ├── TextViewModel.cs
│       ├── WorshipViewModel.cs
│       └── SignupLogViewModel.cs
│
├── Ceremony.Service/          # 業務邏輯服務專案 (Library)
│   ├── BaseService.cs         # 泛型基礎服務（CRUD）
│   ├── AdminsService.cs
│   ├── BelieversService.cs
│   ├── BelieverViewService.cs
│   ├── SignupsService.cs
│   ├── SignupViewService.cs
│   ├── SignupLogsService.cs
│   ├── CeremonyCategorysService.cs
│   └── ZipcodesService.cs
│
├── Ceremony.Library/          # 共用工具庫 (Library)
│   └── PredicateBuilder.cs    # LINQ 動態條件組合器（True/False/Or/And 方法）
│
├── DataTrans/                 # 資料遷移工具 (Console)
│   ├── Program.cs             # 遷移主程式
│   ├── Address.cs             # 台灣地址解析器
│   ├── Number.cs              # 編號與預繳解析器
│   ├── ceremonies.cs          # 舊系統法會 Entity
│   ├── ceremonyfee.cs         # 舊系統法會費用 Entity
│   ├── NOModel.Context.cs     # CeremonyNO DbContext
│   └── ONModel.Context.cs     # CeremonyON DbContext
│
└── Setup/                     # Windows 安裝程式 (VDProj)
```

### 分層架構

```
┌─────────────────────────────┐
│   UI Layer (WinForms)       │  Ceremony 專案
│   Forms + Commons           │
├─────────────────────────────┤
│   Service Layer             │  Ceremony.Service 專案
│   BaseService<T> + 各 Service│
├─────────────────────────────┤
│   Data Layer                │  Ceremony.Models 專案
│   EF6 + GenericRepository   │
├─────────────────────────────┤
│   Database                  │  SQL Server (Ceremony)
└─────────────────────────────┘
```

---

## 3. 資料庫設計

### 3.1 連線資訊

| 項目 | 值 |
|------|-----|
| 伺服器 | 192.168.1.151 |
| 資料庫名稱 | Ceremony |
| 驗證方式 | SQL Server 驗證 |
| 帳號 | sa |
| ORM | Entity Framework 6.4.4（Database First via EDMX） |

### 3.2 資料表結構

#### Admins（管理者）

| 欄位 | 型別 | Nullable | 說明 |
|------|------|----------|------|
| **AdminID** | int (PK, Identity) | NOT NULL | 管理者編號（自動遞增） |
| Name | nvarchar(50) | NULL | 姓名 |
| Username | nvarchar(50) | NOT NULL | 帳號 |
| Password | nvarchar(20) | NOT NULL | 密碼（明文儲存） |
| IsEnabled | bit | NOT NULL | 是否啟用 |

#### Believers（信眾）

| 欄位 | 型別 | Nullable | 說明 |
|------|------|----------|------|
| **BelieverID** | uniqueidentifier (PK) | NOT NULL | 信眾編號（GUID） |
| EmployeeType | int | NOT NULL | 員工類型（1=非員工, 2=大殿, 3=地藏殿） |
| HallName | nvarchar(10) | NULL | 堂號 |
| Name | nvarchar(30) | NOT NULL | 姓名 |
| Phone | nvarchar(30) | NULL | 電話 |
| MailZipcodeID | int (FK→Zipcodes) | NULL | 郵寄地址郵遞區號 ID |
| MailZipcode | nvarchar(10) | NULL | 郵寄郵遞區號 |
| MailAddress | nvarchar(250) | NULL | 郵寄地址 |
| TextZipcodeID | int (FK→Zipcodes) | NULL | 疏文地址郵遞區號 ID |
| TextZipcode | nvarchar(10) | NULL | 疏文郵遞區號 |
| TextAddress | nvarchar(250) | NULL | 疏文地址 |
| LivingNameOne ~ LivingNameSix | nvarchar(30)×6 | NULL | 陽上姓名（最多 6 位） |
| DeadNameOne ~ DeadNameSix | nvarchar(30)×6 | NULL | 往生姓名（最多 6 位） |
| IsFixedNumber | bit | NOT NULL | 是否固定編號 |

**關聯：**
- Believers → Zipcodes（MailZipcodeID）：郵寄地址區號
- Believers → Zipcodes（TextZipcodeID）：疏文地址區號
- Believers → Signups（一對多）：一位信眾可有多筆報名

#### CeremonyCategorys（法會分類）

| 欄位 | 型別 | Nullable | 說明 |
|------|------|----------|------|
| **CeremonyCategoryID** | uniqueidentifier (PK) | NOT NULL | 分類編號（GUID） |
| Title | nvarchar(50) | NOT NULL | 分類名稱 |
| ParentID | uniqueidentifier (FK→自身) | NULL | 父分類（階層式） |
| Sort | int | NOT NULL | 排序序號 |

**關聯：**
- 自我參照（ParentID → CeremonyCategoryID）：樹狀階層結構
- CeremonyCategorys → Signups（一對多）：該法會下的報名
- CeremonyCategorys → Signups.PrepayCeremonyCategoryID：預繳法會

#### Signups（報名）

| 欄位 | 型別 | Nullable | 說明 |
|------|------|----------|------|
| **SignupID** | uniqueidentifier (PK) | NOT NULL | 報名編號（GUID） |
| Year | int | NOT NULL | 年份（民國年） |
| CeremonyCategoryID | uniqueidentifier (FK→CeremonyCategorys) | NOT NULL | 法會分類 |
| CeremonyDate | datetime | NULL | 法會日期（**未使用：程式碼中從未讀取或設定**） |
| SignupType | int | NOT NULL | 報名類型（1=一般, 2=寺方, 3=觀音會, 4=普桌, 5=郵撥） |
| BelieverID | uniqueidentifier (FK→Believers) | NULL | 信眾 |
| NumberTitle | nvarchar(5) | NULL | 編號標題（由 SignupType 決定，見 §4.3） |
| Number | int | NULL | 編號 |
| Fee | int | NULL | 費用 |
| Name | nvarchar(30) | NULL | 姓名（報名時快照，非即時參照 Believers） |
| Phone | nvarchar(30) | NULL | 電話（報名時快照） |
| LivingNameOne ~ LivingNameSix | nvarchar(30)×6 | NULL | 陽上姓名（報名時快照） |
| DeadNameOne ~ DeadNameSix | nvarchar(30)×6 | NULL | 往生姓名（報名時快照） |
| MailZipcodeID | int (FK→Zipcodes) | NULL | 郵寄區號 ID |
| MailZipcode | nvarchar(10) | NULL | 郵寄郵遞區號 |
| MailAddress | nvarchar(250) | NULL | 郵寄地址 |
| TextZipcodeID | int (FK→Zipcodes) | NULL | 疏文區號 ID |
| TextZipcode | nvarchar(10) | NULL | 疏文郵遞區號 |
| TextAddress | nvarchar(250) | NULL | 疏文地址 |
| Remark | nvarchar(250) | NULL | 備註 |
| PrepayYear | int | NULL | 預繳至年份 |
| PrepayCeremonyCategoryID | uniqueidentifier (FK→CeremonyCategorys) | NULL | 預繳至法會 |
| AdminID | int | NOT NULL | 建立者管理員 ID |
| Createdate | datetime | NOT NULL | 建立時間（Precision=3，毫秒精度） |

**關聯：**
- Signups → Believers（多對一）
- Signups → CeremonyCategorys（多對一，CeremonyCategoryID）
- Signups → CeremonyCategorys（多對一，PrepayCeremonyCategoryID）
- Signups → Zipcodes（多對一，MailZipcodeID）
- Signups → Zipcodes（多對一，TextZipcodeID）

#### SignupLogs（報名異動紀錄）

| 欄位 | 型別 | Nullable | 說明 |
|------|------|----------|------|
| **SignupLogID** | uniqueidentifier (PK) | NOT NULL | 紀錄編號（GUID） |
| SignupID | uniqueidentifier | NOT NULL | 對應報名編號 |
| Year | int | NOT NULL | 年份 |
| CeremonyCategoryTitle | nvarchar(50) | NOT NULL | 法會名稱（反正規化） |
| SignupType | int | NOT NULL | 報名類型 |
| HallName | nvarchar(10) | NULL | 堂號 |
| Name | nvarchar(30) | NOT NULL | 姓名 |
| Phone | nvarchar(30) | NULL | 電話 |
| NumberTitle | nvarchar(5) | NULL | 編號標題 |
| Number | int | NULL | 編號 |
| Fee | int | NULL | 費用 |
| LivingNameOne ~ LivingNameSix | nvarchar(30)×6 | NULL | 陽上姓名 |
| DeadNameOne ~ DeadNameSix | nvarchar(30)×6 | NULL | 往生姓名 |
| MailCity | nvarchar(50) | NULL | 郵寄城市（反正規化） |
| MailZone | nvarchar(50) | NULL | 郵寄區域（反正規化） |
| MailAddress | nvarchar(250) | NULL | 郵寄地址（反正規化） |
| TextCity | nvarchar(50) | NULL | 疏文城市（反正規化） |
| TextZone | nvarchar(50) | NULL | 疏文區域（反正規化） |
| TextAddress | nvarchar(250) | NULL | 疏文地址（反正規化） |
| Remark | nvarchar(250) | NULL | 備註 |
| PrepayYear | int | NULL | 預繳年份 |
| PrepayCeremonyCategoryTitle | nvarchar(50) | NULL | 預繳法會名稱（反正規化） |
| Admin | nvarchar(50) | NOT NULL | 操作管理員（反正規化） |
| Createdate | datetime | NOT NULL | 異動時間（Precision=3） |

> **設計說明：** SignupLogs 採用反正規化設計，將所有關聯資料展開為純文字欄位，確保歷史紀錄不受後續資料異動影響。

#### Zipcodes（郵遞區號）

| 欄位 | 型別 | Nullable | 說明 |
|------|------|----------|------|
| **ZipcodeID** | int (PK) | NOT NULL | 區號編號 |
| CountryID | int | NOT NULL | 國家 ID |
| City | nvarchar(10) | NOT NULL | 縣市 |
| Area | nvarchar(10) | NOT NULL | 鄉鎮區 |
| Zipcode | nvarchar(10) | NOT NULL | 郵遞區號 |
| IsDisplay | int | NOT NULL | 是否顯示 |

> **索引說明：** EDMX 中除主鍵外未定義額外索引。編號唯一性（年份+法會+類型+編號）僅在程式層驗證，資料庫層無唯一約束。無 Database Trigger。

### 3.3 資料庫檢視（Views）

#### BelieverView（信眾檢視）

合併 Believers 與 Signups 資料的扁平化檢視：

| 欄位 | 型別 | 來源 |
|------|------|------|
| BelieverID | Guid | Believers |
| HallName, Name, Phone | string | Believers |
| MailCity, MailZone, MailAddress | string | Believers + Zipcodes |
| TextCity, TextZone, TextAddress | string | Believers + Zipcodes |
| SignupID | Guid? | Signups |
| Year | int? | Signups |
| CeremonyTitle | string | CeremonyCategorys |
| CeremonySort | int? | CeremonyCategorys |
| CeremonyCategoryID | Guid? | Signups |
| SignupType | int? | Signups |
| NumberTitle | string | Signups |
| Number, Fee | int? | Signups |
| LivingNameOne ~ Six | string | Signups |
| DeadNameOne ~ Six | string | Signups |
| PrepayYear | int? | Signups |
| PrepayCeremonyTitle | string | CeremonyCategorys |
| PrepayCeremonyCategoryID | Guid? | Signups |
| Remark | string | Signups |

#### SignupView（報名檢視）

合併 Signups 與相關資料的扁平化檢視：

| 欄位 | 型別 | 來源 |
|------|------|------|
| SignupID | Guid | Signups |
| BelieverID | Guid? | Signups |
| Year | int | Signups |
| CeremonyTitle | string | CeremonyCategorys |
| CeremonySort | int? | CeremonyCategorys |
| CeremonyCategoryID | Guid | Signups |
| SignupType | int | Signups |
| NumberTitle | string | Signups |
| Number, Fee | int? | Signups |
| Employee | string | Believers.EmployeeType 轉文字 |
| Name, HallName, Phone | string | Signups |
| IsFixedNumber | bool? | Believers |
| LivingNameOne ~ Six | string | Signups |
| DeadNameOne ~ Six | string | Signups |
| MailZipcode, MailCity, MailZone, MailAddress | string | Signups + Zipcodes |
| TextZipcode, TextCity, TextZone, TextAddress | string | Signups + Zipcodes |
| PrepayYear | int? | Signups |
| PrepayCeremonyCategoryID | Guid? | Signups |
| PrepayCeremonyTitle | string | CeremonyCategorys |
| Remark | string | Signups |
| AdminName | string | Admins |
| Createdate | DateTime | Signups |

### 3.4 ER 關聯圖

```
┌──────────┐     ┌───────────────────┐     ┌──────────┐
│  Admins  │     │  CeremonyCategorys │◄───┐│ Zipcodes │
└──────────┘     │  (自我參照階層)      │    ││          │
                 └───────┬───────────┘    │└────┬─────┘
                         │ 1:N            │     │ 1:N (×4)
                         ▼                │     ▼
┌──────────┐  1:N  ┌──────────┐          │  ┌──────────┐
│ Believers├──────►│ Signups  ├──────────┘  │          │
└──────────┘       │          ├─FK─────────►│ Zipcodes │
                   └────┬─────┘             └──────────┘
                        │ 1:N (邏輯關聯)
                        ▼
                   ┌───────────┐
                   │ SignupLogs│
                   │（反正規化） │
                   └───────────┘
```

---

## 4. 業務邏輯層

### 4.1 泛型 Repository 模式

**GenericRepository\<TEntity\>** 提供統一的 CRUD 操作：

```
IRepository<T>
├── Insert(entity)           # 新增
├── Update(entity)           # 更新（全欄位）
├── SpecificUpdate(entity, properties[])  # 指定欄位更新
├── Delete(id / entity / entities)        # 刪除
├── GetByID(id)              # 依 ID 取得
├── Get()                    # 取得 IQueryable<T>
├── SaveChanges()            # 儲存變更
├── SwitchLazyLoading(bool)  # 切換延遲載入
└── ExeLog()                 # 輸出 SQL 日誌
```

### 4.2 Service 層

**BaseService\<TEntity\>** 封裝 Repository，加入 IResult 回傳（含 ID、Success、Message、Exception、InnerResults 屬性）：

| Service | Entity | 說明 |
|---------|--------|------|
| AdminsService | Admins | 管理者 CRUD |
| BelieversService | Believers | 信眾 CRUD |
| BelieverViewService | BelieverView | 信眾檢視查詢 |
| SignupsService | Signups | 報名 CRUD |
| SignupViewService | SignupView | 報名檢視查詢 |
| SignupLogsService | SignupLogs | 異動紀錄 CRUD |
| CeremonyCategorysService | CeremonyCategorys | 法會分類 CRUD |
| ZipcodesService | Zipcodes | 郵遞區號查詢 |

> 所有 Service 均繼承 BaseService\<T\>，無額外業務方法。複雜業務邏輯全部寫在 Form 層。

### 4.3 關鍵業務邏輯

#### 編號產生（Library.GetSignupNumber）
- 依據「年份 + 法會分類 + 報名類型」取得目前最大編號 + 1
- 支援「固定編號」（IsFixedNumber）的信眾保留原編號

#### 編號顯示規則
- SignupType=2（寺方）：僅顯示 NumberTitle（如「寺」），**不加數字編號**
- 其他類型：顯示 NumberTitle + Number（如「No.12」、「觀35」）

#### NumberTitle 指派邏輯

NumberTitle 由 SignupType **程式自動決定**，不可手動覆寫。在 NewSignupForm 和 EditSignupForm 的 btnConfirm 中以 switch 指派：

```
SignupType=1 → "No"    SignupType=2 → "寺"    SignupType=3 → "觀"
SignupType=4 → "普"    SignupType=5 → "郵"
```

> NumberTitle 獨立儲存於 Signups 表的原因：SignupLogs 反正規化快照需要，且 DB View 直接讀取不需再做轉換。

#### 編號文字轉換（GetNumberText）— 避諱「4」

**關鍵結論：避 4 是「僅顯示轉換」，不是跳號。**

- 資料庫中 Number 欄位包含 4（如 4、14、24、34...）
- GetSignupNumber() 純粹取 MAX(Number)+1，**不跳過 4**
- GetNumberText() 在**顯示/列印時**才做轉換：取個位數，若為 4 則替換為 `3-1`
- 例：Number=4 顯示為「No.3-1」、Number=14 顯示為「No.13-1」、Number=24 顯示為「No.23-1」
- **預繳載入的「填補空號」演算法不受影響**，因為空號判斷基於實際 Number 值
- **業務原因：** 避諱數字 4（與「死」諧音），在宗教法會場景中尤為重要

#### 預繳載入（LoadPrepayForm）

從「來源年份 + 法會」查詢有預繳紀錄的報名，批次建立到「目標年份 + 法會」。依 6 種員工/類型組合分別處理：

| Case | 名稱 | SignupType | EmployeeType | 說明 |
|------|------|-----------|-------------|------|
| 1 | 非員工一般 | 1 | 1（非員工） | 固定編號優先保留，非固定自動填補空號 |
| 2 | 一般地藏殿員工 | 1 | 3（地藏殿） | 獨立編號序列，同樣固定優先+空號填補 |
| 3 | 寺方 | 2 | 不限 | 不分員工類型，SignupType=2 獨立序列 |
| 4 | 觀音會 | 3 | 不限 | SignupType=3 獨立序列 |
| 5 | 郵撥大殿員工 | 5 | 2（大殿） | SignupType=5 共用序列 |
| 6 | 郵撥非員工 | 5 | 1（非員工） | SignupType=5 共用序列 |

**編號策略：**
- 固定編號（IsFixedNumber=true）的信眾保留原編號
- 非固定編號的信眾：先填補固定編號留下的空號，再依序遞增
- 各 SignupType 有各自獨立的編號序列

**預繳條件判斷：**
```
PrepayYear != null AND (
  (PrepayYear == 目標年份 AND 預繳法會Sort >= 目標法會Sort) OR
  (PrepayYear > 目標年份 AND PrepayCeremonyCategoryID != null)
)
```
- 預繳年份等於目標年份時，僅載入排序在目標法會之後（含）的預繳
- 預繳年份大於目標年份時，全部載入並保留預繳資訊往後遞延

**複製欄位：** SignupType、BelieverID、NumberTitle、Fee、6 組陽上/往生姓名、郵寄/疏文地址（含 ZipcodeID）、備註、符合條件的預繳年份與法會

> **注意：** 預繳載入**不複製** Name 和 Phone 欄位（這些欄位在預繳載入產生的 Signup 中為 null）

#### 疏文地址自動回填邏輯（NewSignupForm / EditSignupForm）
- 若疏文地址（TextAddress）為空且郵寄區域已選擇，自動將 TextZipcodeID 設為 MailZipcodeID
- TextZipcode 為空時回填 MailZipcode，TextAddress 為空時回填 MailAddress
- 確保報名紀錄至少有一組完整地址

#### 新增/編輯報名時同步更新信眾資料
- **新增報名（NewSignupForm）：** 未選擇既有信眾時，自動建立新 Believers 記錄
- **編輯報名（EditSignupForm）：** 儲存時同步更新 Believers 的 HallName、EmployeeType、IsFixedNumber（但**不更新** Name 和 Phone，程式碼已註解）

#### 報名異動紀錄觸發時機
- **新增報名時：** NewSignupForm.btnConfirm 同時建立 Signup + SignupLog
- **編輯報名時：** EditSignupForm.btnConfirm 同時更新 Signup + 建立新 SignupLog
- 每筆 SignupLog 為當下完整快照，包含所有欄位值

#### 登入驗證（LoginForm.ValidateUser）
- 優先比對硬編碼帳號 `weypro / weypro12ab`（AdminID = 0）
- 再查詢 Admins 資料表，比對 Username + Password（明文），且 IsEnabled 必須為 true
- 登入成功設定 Global.Islogin、Username、AdminID

#### 民國年轉換
- 使用 `System.Globalization.TaiwanCalendar` 處理民國年份

#### 全形轉半形
- 電話、費用、編號、預繳年份等欄位使用 `Microsoft.VisualBasic.Strings.StrConv(VbStrConv.Narrow)` 自動將全形數字轉為半形

### 4.4 交易邊界（Transaction Boundaries）

| 操作 | SaveChanges 次數 | 是否原子性 | 說明 |
|------|-----------------|-----------|------|
| **新增報名** (NewSignupForm) | 3 次 | **否** | ①建立 Believers → SaveChanges ②建立 Signups → SaveChanges ③建立 SignupLogs → SaveChanges |
| **編輯報名** (EditSignupForm) | 3 次 | **否** | ①更新 Believers → SaveChanges ②更新 Signups → SaveChanges ③建立 SignupLogs → SaveChanges |
| **載入預繳** (LoadPrepayForm) | 1 次 | **是** | 6 種 case 全部 Create() 後，最終一次 SaveChanges() 提交 |

> **風險：** 新增/編輯報名若在第 2 次 SaveChanges 失敗，Believers 已更新但 Signups 未寫入，造成資料不一致。SignupLogs 失敗則報名無審計紀錄。無使用 DbTransaction 或 TransactionScope。

---

## 5. 使用者介面

### 5.1 表單清單

| 表單 | 功能說明 | 主要控件 |
|------|----------|----------|
| **LoginForm** | 登入 | 帳號/密碼文字框、確認按鈕 |
| **MainForm** | 主選單 | 6 個功能按鈕（管理者/信眾/報名/新增報名/載入預繳/備份）+ 版本標籤 |
| **AdminsForm** | 管理者維護 | DataGridView（管理者列表）、新增/修改/軟刪除（IsEnabled=false）、帳號重複驗證、密碼確認欄位 |
| **BelieverForm** | 信眾維護 | 搜尋區（姓名/堂號/電話/陽上/往生）、DataGridView、詳細資料編輯區 |
| **SignupForm** | 報名維護 | 搜尋篩選（法會/年份/類型）、DataGridView、右鍵選單（新增/編輯/列印） |
| **NewSignupForm** | 新增報名 | 兩步驟：Step1 選法會年份 → Step2 選信眾填寫資料、信眾搜尋 DataGridView |
| **EditSignupForm** | 編輯報名 | 信眾下拉選單（AutoComplete 自動完成搜尋）、地址選擇、各欄位編輯、同步更新信眾資料 |
| **LoadPrepayForm** | 載入預繳 | 來源/目標年份法會選擇、確認載入 |
| **CeremonyCategoryForm** | 法會分類維護 | TreeView（兩層樹狀結構）、右鍵選單（新增子層/編輯/刪除） |
| **SignupLogForm** | 報名異動紀錄 | 唯讀 DataGridView，依 Createdate 倒序顯示，每次編輯報名產生一筆完整快照 |
| **CustomDialogForm** | 自訂輸入對話框 | 列印格式選擇用 |
| **CustomMessageForm** | 自訂訊息框 | 通用訊息顯示 |

### 5.2 介面操作流程

#### 新增報名流程
```
1. 選擇法會（下拉選單，載入 ParentID=null 的頂層分類）+ 年份 + 報名類型
2. 點擊「下一步」
3. 搜尋信眾（姓名或電話）
4. 從 DataGridView 選取信眾 → 自動帶入資料
   或 直接填寫新信眾資料
4a. 選取信眾時資料帶入優先順序：
     - 「Signup 記錄」的來源：
       ① 若從報名維護右鍵「新增報名」進入，使用 ParamSignupID（傳入的報名 ID）
       ② 否則使用 DataGridView 選取列的 ColSignupID 欄位值
       ③ 兩者皆無時 signup = null，直接讀 Believer 記錄
     - 姓名/電話：Signup.Name/Phone > DataGridView ColName/ColPhone > Believer.Name/Phone
     - 地址：Signup.Zipcodes（Mail/Text）> Believer.Zipcodes
     - 陽上/往生名單：從 DataGridView（BelieverView）帶入
     - 預繳：自動查詢該信眾「今年以前最新的報名」，若有預繳則帶入
5. 填寫：堂號、姓名、電話、員工類型、固定編號
6. 填寫陽上名單（最多 6 位）
7. 填寫往生名單（最多 6 位）
8. 選擇郵寄地址（縣市→區→詳細地址）
9. 選擇疏文地址
10. 填寫費用、備註
11. 設定預繳（年份 + 法會）
12. 點擊「確認」→ 自動產生編號 → 儲存
13. 可選擇列印資料卡
```

#### 信眾維護流程
```
1. 輸入搜尋條件（姓名/堂號/電話/陽上名/往生名），至少填一項
2. 搜尋使用 CONTAINS 模糊比對，6 組陽上名 OR 連結、6 組往生名 OR 連結
3. 點選信眾 → 右側表單帶入資料
4. 新增時：必填姓名、郵寄地址；自動產生 GUID
5. 編輯時：不可修改姓名與電話（唯讀），可修改其他欄位
6. 電話驗證：必須以 0 開頭（regex: ^0[0-9]*$）
7. 刪除驗證：若信眾有報名紀錄則無法刪除
8. 「疏文地址同郵寄地址」勾選框：自動複製郵寄地址至疏文地址
9. 點擊「確認」儲存 或「取消」放棄
```

#### 報名維護流程
```
1. 搜尋條件（使用 PredicateBuilder 動態組合 AND/OR）：
   - 法會（下拉選單）
   - 年份（文字框，支援「以上」勾選框 cbIsScope 做 >= 篩選）
   - 報名類型（含「全部」選項，ID=-1）
   - 編號（NumericUpDown，指定單一編號）
   - 關鍵字搜尋（txtSearchKey）+ 勾選搜尋範圍：
     ☑ 姓名（cbSearchName）→ Name CONTAINS
     ☑ 陽上（cbSearchLivingName）→ LivingNameOne~Six OR CONTAINS
     ☑ 往生（cbSearchDeadName）→ DeadNameOne~Six OR CONTAINS
     ☑ 電話（cbSearchPhone）→ Phone CONTAINS
   - 固定編號（cbIsFixedNumber）→ 篩選 IsFixedNumber=true
2. 點擊「搜尋」→ DataGridView 顯示結果（含載入狀態提示「搜尋中，請稍後...」）
3. 右鍵選單操作（單筆）：
   - 新增報名（帶入選取列的信眾姓名+SignupID）
   - 編輯報名 → EditSignupForm
   - 查看變更紀錄 → SignupLogForm
4. 右鍵選單操作（多選）：
   - 列印資料卡（DataCard）
   - 列印收據（Receipt）
   - 列印薦牌（Tablet）
   - 列印文牒（Text）
   - 列印普桌（Worship）→ 僅 SignupType=4（普桌）時啟用
   - 刪除（多選支援，確認後逐筆刪除）
5. 依編號區間列印（btnPrint）：
   - 輸入起始/結束編號（nudStart / nudEnd）
   - 選擇列印類型（PrintType 下拉選單：資料卡/收據/薦牌/文牒/普桌）
   - 結合搜尋條件篩選後批次列印
6. 列印前透過 CustomDialogForm 選擇輸出格式（PDF 檔案 或 預覽列印）
7. 匯出 Excel（btnExportExcel）：
   - 匯出 DataGridView 全部列至 .xls 檔
   - 欄位包含：年份、法會、編號、費用、員工類型、姓名、備註、
     堂號、往生×6、陽上×6、預繳年份、預繳法會、電話、
     郵寄地址（城市/區/地址）、疏文地址（城市/區/地址）共 30 欄
8. 支援 PDF 合併匯出（PDFsharp）
```

#### 搜尋組合邏輯詳解

搜尋使用兩層 PredicateBuilder：

**AND 層（結構化篩選，所有條件必須同時成立）：**
- 年份（== 或 >= 取決於 cbIsScope 勾選）
- 法會分類（下拉選單，Guid.Empty 時不篩選）
- 報名類型（-1 代表全部，不篩選）
- 編號（NumericUpDown 值為 0 時不篩選）

**OR 層（關鍵字搜尋，任一勾選條件命中即可）：**
- 勾選「姓名」→ Name CONTAINS 關鍵字
- 勾選「陽上」→ LivingNameOne~Six 任一 CONTAINS
- 勾選「往生」→ DeadNameOne~Six 任一 CONTAINS
- 勾選「電話」→ Phone CONTAINS
- 勾選「固定編號」→ IsFixedNumber == true

**最終查詢：** `WHERE (AND條件) AND (任一OR條件)`

**未輸入任何條件時：** AND predicate 預設為 true，OR predicate 不套用 → **回傳所有資料**。結果為空時顯示「無資料，請重新搜尋！」。

#### 列印格式對應

**CustomDialogForm 提供 2 種輸出格式：**
- 「PDF」→ 輸出為 PDF 檔案
- 「預覽列印」→ 以 EMF 格式預覽

**5 種列印類型 × RDLC 範本對應：**
- 列印類型透過 dlPrintType 下拉選單選擇（ID=1~5）
- 各類型使用對應的基底 RDLC 範本（tmpDataCard / tmpReceipt / tmpTablet / tmpText / tmpWorship）
- 薦牌的 9 種變體和普桌的 5 種變體是**同類型的不同版面格式**，供不同法會或場景使用

### 5.3 地址選擇機制

系統使用兩層地址選擇：
1. **縣市（City）** 下拉選單 → 從 Zipcodes 取得不重複縣市（GroupBy City）
2. **鄉鎮區（Zone/Area）** 下拉選單 → 依選取縣市篩選
3. 選取後自動帶入郵遞區號
4. 地址分為「郵寄地址」（Mail）和「疏文地址」（Text）兩組
5. 支援「疏文地址同郵寄地址」勾選框，自動複製

### 5.4 表單驗證規則

#### 報名編輯（EditSignupForm）驗證
| 欄位 | 規則 | 錯誤訊息 |
|------|------|----------|
| 年份 | 民國年格式 `^1[0-9]{2}$`，不可小於當前民國年 | 年份格式錯誤，請重新確認！ |
| 費用 | 數字 `^[0-9]*$` | 費用格式錯誤，請重新確認！ |
| 編號 | 正整數 `^[1-9][0-9]*$`，年份+編號+法會+類型不可重複（排除自身） | 編號重複，請重新確認！ |
| 預繳年份 | 民國年格式 `^1[0-9]{2}$`，必須 >= 當前民國年，合法時自動載入預繳法會下拉選單 | 預繳年份格式錯誤/預繳年份需大於N |
| 姓名 | 不可為空 | 請輸入姓名 |
| 法會 | 不可為空（下拉選單） | 請選擇法會 |
| 報名類型 | 不可為空（下拉選單） | 請選擇類型 |

#### 新增報名（NewSignupForm）驗證
| 欄位 | 規則 | 錯誤訊息 |
|------|------|----------|
| 年份 | 民國年格式 `^1[0-9]{2}$`，不可小於當前民國年 | 年份格式錯誤，請重新確認！ |
| 電話 | 以 0 開頭 `^0[0-9]*$` | 聯絡電話格式錯誤，請重新確認！ |
| 編號 | 正整數 `^[1-9][0-9]*$`，年份+法會+類型+編號不可重複 | 編號重複，請重新確認！ |
| 費用 | 數字 `^[0-9]*$` | 費用格式錯誤，請重新確認！ |
| 預繳年份 | 民國年格式 `^1[0-9]{2}$` | 格式錯誤時清空預繳法會 |
| 姓名 | 不可為空 | 請輸入姓名 |
- 固定編號勾選時，編號欄位啟用、不可為空
- 未選擇信眾時自動建立新 Believers 記錄
- 儲存成功後可列印資料卡，列印後自動回到 Step1

#### 管理者（AdminsForm）驗證
| 欄位 | 規則 |
|------|------|
| 帳號 | 不可為空，不可重複（更新時排除自身 AdminID） |
| 密碼 | 不可為空 |
| 確認密碼 | 必須與密碼相同 |

#### 法會分類（CeremonyCategoryForm）規則
- 支援兩層階層：根節點（法會維護）→ 第一層（法會）→ 第二層（子法會）
- 第一層可新增子項目、可編輯 Title/Sort、可刪除
- 第二層不可再新增子項目、可編輯、可刪除
- **刪除限制：** 該分類下無報名紀錄且無子分類時才能刪除，否則提示「已有報名或還有下層法會，無法刪除」

### 5.5 枚舉值定義

#### EmployeeType（員工類型）

| ID | 名稱 | 說明 |
|----|------|------|
| 1 | 非員工 | 一般信眾 |
| 2 | 大殿 | 大殿員工 |
| 3 | 地藏殿 | 地藏殿員工 |

#### SignupType（報名類型）

| ID | 名稱 | NumberTitle（編號標題） |
|----|------|------------------------|
| 1 | 一般 | No |
| 2 | 寺方 | 寺 |
| 3 | 觀音會 | 觀 |
| 4 | 普桌 | 普 |
| 5 | 郵撥 | 郵 |

> SignupForm 搜尋篩選另有 ID=-1「全部」選項。
> LoadPrepayForm 的信眾下拉選單使用擴展版 6 值分類：1=一般非員工, 2=一般地藏殿員工, 3=寺方, 4=觀音會, 5=郵撥大殿員工, 6=郵撥非員工。

#### PrintType（列印類型）

| ID | 名稱 |
|----|------|
| 1 | 資料卡 |
| 2 | 收據 |
| 3 | 薦牌 |
| 4 | 文牒 |
| 5 | 普桌 |

> BelieverTypeViewModel 已定義但系統中未使用。

---

## 6. 報表與列印

### 6.1 報表範本（RDLC）

| 報表檔案 | 用途 | 對應 ViewModel |
|----------|------|----------------|
| tmpDataCard.rdlc | 資料卡 | DataCardViewModel |
| tmpReceipt.rdlc | 收據 | ReceiptViewModel |
| tmpTablet.rdlc | 薦牌（基本版） | TabletViewModel |
| tmpTabletOne.rdlc | 薦牌（變體 1） | TabletViewModel |
| tmpTabletOneOne.rdlc | 薦牌（變體 1-1） | TabletViewModel |
| tmpTabletOneTwo.rdlc | 薦牌（變體 1-2） | TabletViewModel |
| tmpTabletTwo.rdlc | 薦牌（變體 2） | TabletViewModel |
| tmpTabletTwoOne.rdlc | 薦牌（變體 2-1） | TabletViewModel |
| tmpTabletTwoTwo.rdlc | 薦牌（變體 2-2） | TabletViewModel |
| tmpTablet_One.rdlc | 薦牌（格式 A） | TabletViewModel |
| tmpTablet_Two.rdlc | 薦牌（格式 B） | TabletViewModel |
| tmpText.rdlc | 文牒 | TextViewModel |
| tmpTextTwo.rdlc | 文牒（變體 2） | TextViewModel |
| tmpWorship.rdlc | 普桌（基本版） | WorshipViewModel |
| tmpWorshipOne.rdlc | 普桌（變體 1） | WorshipViewModel |
| tmpWorshipTwo.rdlc | 普桌（變體 2） | WorshipViewModel |
| tmpWorshipThree.rdlc | 普桌（變體 3） | WorshipViewModel |
| tmpWorshipFour.rdlc | 普桌（變體 4） | WorshipViewModel |
| tmpWorshipFive.rdlc | 普桌（變體 5） | WorshipViewModel |

### 6.1.1 報表版面規格

| 報表類型 | 紙張寬度 | 紙張高度 | 約等於 | 方向 | 邊界 |
|----------|---------|---------|--------|------|------|
| **資料卡** (DataCard) | 21cm | 14.8cm | A5 橫式 | Portrait（自定義卡片） | 全部 0cm |
| **收據** (Receipt) | 21cm | 29.7cm | A4 | Portrait | 全部 0cm |
| **薦牌** (Tablet) | 11.5cm | 25.4cm | 窄長型（4.5"×10"） | Portrait | 全部 0cm |
| **文牒** (Text) | 36.5cm | 26.2cm | 超寬型（14.4"×10.3"） | Landscape | 全部 0cm |
| **普桌** (Worship) | 21cm | 29.6cm | A4 | Portrait | 全部 0cm |

**共通規格：**
- **字型：** 全部使用標楷體（BiauKai）
- **字級範圍：** 0.6cm（小標注）~ 2cm（普桌大字）
- **邊界：** 所有報表均為 0cm（滿版列印）
- **報表格式：** MS SQL Server 2008 RDLC Schema
- **資料來源：** DataCardDataSet（定義於 DataCardDataSet.xsd）

**各報表特殊設計：**
- **資料卡：** 含實線/虛線分隔、簽名欄位「確認無誤請簽名」
- **收據：** 雙聯設計（收據聯 + 存根聯），Tablix 高度 59.4cm（兩張 A4 份量）
- **薦牌：** 窄長型版面，適用於牌位印刷
- **文牒：** 超寬版面，含垂直地址文字圖片（PhotoAddress）
- **普桌：** 含背景圖片（worship2），名字使用 2cm 大字，有分頁設定（StartAndEnd）

### 6.2 報表資料模型

#### DataCardViewModel（資料卡）
- SignupID, HallName, Number, Prepay
- LivingNameOne ~ LivingNameSix
- DeadNameOne ~ DeadNameSix
- Address, Phone, Remark

#### ReceiptViewModel（收據）
- SignupID, Name, Zipcode, Address
- Fee, Number
- Year, Month, Day（民國年月日）
- Prepay

#### TabletViewModel（牌位）
- SignupID, Number
- HallNameFirst, HallNameSecond（堂號拆分：2 字拆 1+1，4 字拆 2+2）
- LivingNameOne ~ LivingNameSix
- DeadNameOne ~ DeadNameSix

#### TextViewModel（疏文）
- SignupID, Number
- HallNameFirst, HallNameSecond
- TextAddress
- **PhotoAddress** (byte[])：由 Library.DrawText() 產生的垂直地址圖片
- LivingNameOne ~ LivingNameSix
- DeadNameOne ~ DeadNameSix

#### WorshipViewModel（禮斗）
- SignupID, Number
- **僅含 LivingNameOne ~ LivingNameSix**（無往生名單、無堂號）

### 6.3 列印特殊處理

- **垂直文字繪製（Library.DrawText）：** 將地址文字轉為垂直排列的 PNG 圖片，使用標楷體 25px，英數字旋轉 90 度處理
- **列印格式選擇：** 透過 CustomDialogForm 讓使用者選擇列印格式
- **PDF 合併：** 使用 PDFsharp 合併多頁 PDF
- **Excel 匯出：** 使用 NPOI 匯出信眾/報名資料
- **Microsoft.Reporting.WinForms：** RDLC 報表引擎

---

## 7. 資料遷移工具

### DataTrans 專案

用於從舊系統遷移資料至新系統的 Console 應用程式。

#### 來源資料庫
- **CeremonyNO**（NOModel.Context.cs）
- **CeremonyON**（ONModel.Context.cs）

#### 舊系統 Entity

**ceremonies（舊法會）**
- ceremonyid, ceremonydate, ceremonyname, makeuser, makedate

**ceremonyfee（舊法會費用/報名）**
- serialid, trial_CEREMONYID_2, feemoney, advancemoney, status
- family, die1/die2/die3, live1/live2/live3
- phone, address, ps, makeuser, makedate

#### 資料轉換工具

**Address 解析器：** 使用正則表達式解析台灣地址格式
- 輸入：完整地址字串
- 輸出：City, District, Others, ZipCode

**Number 解析器：** 解析編號與預繳資訊
- 輸入：編號字串
- 輸出：Num, PrePayYear, PrePayCeremonyCategoryID
- 預設法會分類 GUID：
  - 春季：`18927907-dcad-42b2-8f2a-635c2e0fa98d`
  - 中元：`0c478f0e-787c-448e-ba7b-b1579f3f1fce`
  - 秋季：`3864e4dc-24db-4544-acb3-3351592f6dab`

---

## 8. 系統相依套件

| 套件 | 版本 | 用途 |
|------|------|------|
| .NET Framework | 4.8 | 執行環境 |
| EntityFramework | 6.4.4 | ORM 資料存取 |
| Newtonsoft.Json | 13.0.1 | JSON 序列化 |
| NPOI | 2.5.6 | Excel 匯出 |
| PDFsharp | 1.50.5147 | PDF 產生與合併 |
| LinqKit | 1.2.2 | 動態 LINQ 查詢 |
| BouncyCastle | 1.8.9 | 加密（NPOI 相依） |
| SharpZipLib | 1.3.3 | 壓縮（NPOI 相依） |
| Microsoft.ReportViewer.WinForms | 10.0.40219.1 | RDLC 報表引擎 |

---

## 9. 已知問題與安全風險

### 安全性問題

1. **密碼明文儲存：** Admins 資料表的 Password 欄位為明文，未經雜湊或加密
2. **硬編碼後門帳號：** LoginForm 中硬編碼 `weypro / weypro12ab` 萬用帳號
3. **SA 帳號連線：** 使用 SQL Server sa 帳號，權限過大
4. **連線字串明文：** App.config 中密碼明文存放
5. **無密碼複雜度要求：** 管理者密碼無任何驗證規則

### 架構問題

1. **業務邏輯在 UI 層：** 大量業務邏輯直接寫在 Form 的事件處理中（如 SignupForm.cs 超過 800 行），Service 層僅為 Repository 的薄包裝
2. **BaseService.Dispose 遞迴：** `BaseService.Dispose()` 呼叫 `this.Dispose()` 形成無窮遞迴
3. **資料冗餘：** Signups 與 Believers 均有 LivingName/DeadName 各 6 欄位，存在大量資料重複
4. **魔術數字：** EmployeeType（1/2/3）、SignupType（1~5）等使用 int 硬編碼而非 enum
5. **Navigation Property 命名不直覺：** EF 自動產生的名稱如 Zipcodes1、CeremonyCategorys1 缺乏語意
6. **AdminsForm Enter 鍵覆寫：** ProcessCmdKey 將 Enter 鍵改為 Tab 行為，非標準 UX
7. **Connection Pooling 關閉：** App.config 中設定 `Pooling=False`，每次操作都重建連線，影響效能
8. **CeremonyDate 死欄位：** Signups.CeremonyDate 從未在程式碼中使用，佔用資料庫空間
9. **Newtonsoft.Json 死相依：** 3 個 Form 有 using 引入但無實際使用，為無用相依

### 功能限制

1. **單機架構：** Windows Forms 桌面應用，無法遠端或多人同時操作
2. **無權限控管：** 所有登入管理者權限相同，無角色/權限區分
3. **無操作日誌：** 僅 SignupLogs 記錄報名異動，無其他操作審計
4. **備份路徑硬編碼：** 備份固定至 `D:\Backup\`，無法自訂
5. **陽上/往生人數限制：** 固定 6 位，無法彈性擴充

---

## 10. 補充細節（v1.3.0 新增）

### 10.1 全域狀態完整清單（Global.cs）

| 變數 | 型別 | 說明 |
|------|------|------|
| Islogin | bool | 是否已登入 |
| Username | string | 目前登入使用者帳號 |
| AdminID | int | 目前登入管理者 ID |
| AppTitle | string | 應用程式標題，固定值「法會報名系統」 |
| Version | string | 版本號，固定值「v1.2.8」 |

> **注意：** Global.cs 僅包含以上 5 個靜態屬性，無快取資料、無目前選擇年份等其他狀態。Web 化時僅需將 Islogin/Username/AdminID 映射至 Session。

### 10.2 App.config 完整內容

| 設定項 | 值 |
|--------|-----|
| 目標框架 | .NET Framework 4.8 |
| Entity Framework 版本 | 6.0.0.0 |
| Database Provider | System.Data.SqlClient |
| 連線字串名稱 | CeremonyEntities |
| 伺服器 | 192.168.1.151（正式）/ (local)（已註解的開發環境） |
| 資料庫 | Ceremony |
| 帳號 | sa |
| MultipleActiveResultSets | True |
| Connection Pooling | **Disabled**（Pooling=False） |
| App | EntityFramework |

> **注意：** App.config 除連線字串外**無其他自訂設定**（無 appSettings、無自訂組態區段）。Connection Pooling 被關閉是異常設定，可能導致效能問題。

### 10.3 未使用的欄位與套件

| 項目 | 狀態 | 說明 |
|------|------|------|
| Signups.CeremonyDate | **未使用** | 定義於 Entity Model 和資料庫，但程式碼中從未讀取或寫入 |
| Newtonsoft.Json | **未使用** | 在 3 個 Form 檔案中有 `using` 引入但無任何 JSON 操作（死引用） |
| BelieverTypeViewModel | **未使用** | 已定義但系統中未使用（原文件已註明） |

### 10.4 資料庫連線補充

- **連線字串中有明文密碼**（已在 §9 安全性問題中記錄）
- **正式環境與開發環境** 使用同一份 App.config 切換（註解/取消註解）
- **LazyLoadingEnabled = true**（EDMX 設定），所有 Navigation Property 預設延遲載入
