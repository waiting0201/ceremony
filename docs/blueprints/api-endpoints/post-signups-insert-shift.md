---
title: POST /api/v1/signups/insert-shift
purpose: 插入報名於指定編號，並把同群組 (Year, CeremonyCategoryID, SignupType) 內 Number ≥ 該編號的既有報名 +1 順移
status: shipped
endpoint: post-signups-insert-shift
http_method: POST
route: /api/v1/signups/insert-shift
legacy_form: N/A（新版增強，舊 NewSignupForm 無「插入並順移」）
legacy_lines: N/A
related_agents:
  - backend-engineer
  - qa-test-engineer
related_docs:
  - ../../design/api-design.md
  - ../../design/backend-design.md
  - ../signup-management.md
  - post-signups.md
  - ../legacy-coverage/new-signup-form.md
keywords: [insert, shift, 順移, 插入, 編號, renumber, signups]
last_updated: 2026-07-04
---

## 規格

`POST /api/v1/signups/insert-shift`，需 JWT。

### Request DTO

沿用 `CreateSignupRequest`（與 `POST /signups` 同 body）。差異：

| 欄位 | 說明 |
|---|---|
| `customNumber` | **插入位置編號（必填、> 0）** |
| `keepNumber` | 忽略（視為 true） |
| 其餘 | 同 `POST /signups`（year / ceremonyCategoryId / signupType / believerId / name / mailAddress / 名單 / 地址 / 費用 / 備註 / 預繳…） |

### Response

`201 Created`，body 為新建的 `SignupListItem`（同 `POST /signups`）。

### 錯誤碼

| HTTP | errorCode | 觸發 |
|---|---|---|
| 400 | `VALIDATION_REQUIRED` | `customNumber` 為 null 或 ≤ 0（「請輸入編號」）、姓名/寄件地址空白 |
| 404 | `BELIEVER_NOT_FOUND` | believerId 不存在 |
| 404 | `CATEGORY_NOT_FOUND` | ceremonyCategoryId 不存在 |
| 409 | `SIGNUP_BUSY` | 取群組互斥鎖（`sp_getapplock`）逾時 30s |
| 401 | – | 無 JWT |

> **刻意不做編號重複檢查**：插入位置本來就會落在已佔用編號上（那正是要順移的對象）。因此與 `POST /signups` 的 `keepNumber` 路徑不同——後者遇已佔用號回 `SIGNUP_NUMBER_CONFLICT`，本 endpoint 反而以此觸發順移。

## 行為（順移語意）

在**單一交易**內：

1. `sp_getapplock @Resource='signup-number:{year}:{cat}:{type}'`（`Exclusive` / `Transaction` / 30s）——與**預繳載入共用**同一 resource 命名空間，序列化同群組配號作業。逾時 → `SIGNUP_BUSY`。
2. `UPDATE dbo.Signups WITH (UPDLOCK, HOLDLOCK) SET Number = Number + 1 WHERE Year=@ AND CeremonyCategoryID=@ AND SignupType=@ AND Number >= @Number`（set-based 一句；`(Year,Cat,Type,Number)` **無 unique index**，故無中間衝突）。
3. 插入新報名（`Number = @Number`）+ 對應 SignupLog（沿用 `InsertWithLogAsync` 的兩段 INSERT）。
4. commit（失敗全 rollback）。

**順移的既有列只 UPDATE Number、不另 append SignupLog**：SignupLog.Number 是歷史快照（設計上不隨後續變動），且避免一次插入在大群組觸發上百列 log。新插入的那筆仍照常寫自己的 log。

## 舊系統對照（規則 A — forward）

**N/A（新需求）**。舊 `NewSignupForm`（`reference/old/Ceremony/NewSignupForm.cs`）只能：自動 `MAX(Number)+1`（`Library.GetSignupNumber:20-32`）或手動指定**空號**（指定已佔用號會被 `編號重複` 擋下）。**沒有**「插入到已佔用位並讓其後 +1 順移」的路徑。本 endpoint 為新版增強，主要用於**預繳載入後補插一筆**。

## 業務規則

- 順移範圍 = 同 `(Year, CeremonyCategoryID, SignupType)` 且 `Number ≥ 插入編號`。
- 並發安全靠 `UPDLOCK/HOLDLOCK` 範圍鎖 + `sp_getapplock`（見上），不是靠重複檢查。
- 僅 create 情境（列表右鍵「在此前插入」）；編輯既有報名改編號仍走 `PUT /signups`（`SIGNUP_NUMBER_CONFLICT` 擋重複，不順移）。

## 資料存取 / 元件

| 元件 | 檔案 |
|---|---|
| Handler | `Ceremony.Application/Signups/InsertShiftSignupHandler.cs` |
| Repo 介面/實作 | `ISignupRepository.InsertWithShiftAsync` / `SignupRepository.InsertWithShiftAsync`（共用私有 `InsertSignupWithLogRowsAsync`） |
| Controller | `SignupsController.InsertShift`（`[HttpPost("insert-shift")]`） |
| applock 共用 | `PrepayRepository.InsertPrepayBatchAsync` 亦改用 `signup-number:` resource |

## 前端整合

- API：`SignupApi.insertShift()`（`frontend/src/app/core/api/signups/signup.api.ts`）
- 入口：報名維護列表**右鍵某列 →「在此前插入」**（`signup-list-page.ts` `actionInsertBefore` + `insert-above` icon）。
- 表單：`SignupEditFormComponent` 收 `insertAt` input（number/year/ceremonyCategoryId/signupType），套用後鎖定年/法會/類型 + `keepNumber`、預填 `customNumber`；`submit()` 在插入模式改呼叫 `insertShift()`。overlay title「插入報名（後續順移）」。

## 驗收標準

- [x] 插入位置編號必填（null/≤0 → 400）
- [x] 不做編號重複檢查（單元測試 `Times.Never`）
- [x] 同群組 Number ≥ N 全部 +1、新筆取得 N（真實 MSSQL 整合測試）
- [x] 新筆有對應 SignupLog；順移列不 append log
- [x] applock + UPDLOCK/HOLDLOCK 並發鎖，與預繳共用 resource
- [x] 前端右鍵入口 + 插入模式鎖定欄位（Playwright 實機）

## 參考

- 舊 Form：N/A（無對應）
- 相關：[post-signups.md](post-signups.md)、[post-prepay-load.md](post-prepay-load.md)、[signup-management.md](../signup-management.md)
