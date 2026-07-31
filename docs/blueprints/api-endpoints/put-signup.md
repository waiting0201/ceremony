---
title: PUT /api/v1/signups/:id
purpose: 編輯既有報名（全欄位覆寫 + SignupLog 同步寫入）
status: shipped
endpoint: put-signup
http_method: PUT
route: /api/v1/signups/:id
legacy_form: EditSignupForm.cs
legacy_lines: 186-368
related_agents:
  - backend-engineer
related_docs:
  - post-signups.md
  - ../legacy-coverage/edit-signup-form.md
keywords: [signups, update, put, signuplog]
last_updated: 2026-07-31 (新增 §4c 清空要真的清得掉：文牒段留空不再抄寄件段、堂號清空送空字串〔null 會被 SignupView COALESCE 回退信眾堂號〕；先前 2026-07-29 新增 §4b：UPDATE 必須同步 MailZipcode/TextZipcode 文字快照〔由 FK 現查〕，否則改區域後會印出舊號碼；先前 2026-06-29 方案 C：不再回寫 Believer，堂號信眾層級〔已於 2026-07-21 反轉為方案 A：堂號 per-signup 可編輯，但仍不回寫 Believer〕)
---

## 規格

`PUT /api/v1/signups/{id:guid}`，需要 JWT。Body 同 [post-signups.md](post-signups.md) `CreateSignupRequest`（重用 DTO）。

### Response: `200 OK` + `SignupListItem`。

### 錯誤碼

| HTTP | errorCode | message verbatim | 觸發 |
|---|---|---|---|
| 400 | `VALIDATION_REQUIRED` | `請輸入年份`/`請輸入姓名`/`請選擇法會`/`請選擇類型` | 各 required 欄位 |
| 400 | `VALIDATION_INVALID` | `報名類型錯誤` | signupType ∉ 1..5 |
| 404 | `SIGNUP_NOT_FOUND` | `找不到報名` | id 不存在 |
| 404 | `BELIEVER_NOT_FOUND` | `找不到信眾` | believerId 不存在 |
| 409 | `SIGNUP_NUMBER_CONFLICT` | `{year}年編號{number}重複，請重新確認！` | (year, ceremony, type, number) 已被他筆佔用（排除自己） |

## 舊系統對照

| 舊方法/事件 | 行 | 對應 |
|---|---|---|
| `EditSignupForm.btnConfirm_Click` | 186-368 | `UpdateSignupHandler.HandleAsync` |
| Number 重複檢查（排除自己） | 215-223 | `SignupRepository.NumberExistsAsync(..., excludeSignupId=id)` |
| ~~Believer 部分欄位更新（HallName/EmployeeType/IsFixedNumber）~~ **新版故意不做** | 226-232 | 已移除：堂號等為信眾層級，僅信眾維護頁可改（方案 C，見 [signup-hallname-isolation.md](../signup-hallname-isolation.md)）|
| Signup 全欄位覆寫 + AdminID/Createdate 重新賦值 | 234-313 | UPDATE 全欄位 |
| SignupLog 同步寫入 | 317-348 | 與 Signup 同 transaction |

### 業務邏輯區塊（與 POST 不同處）

1. **可改 Number / Year / CeremonyCategoryId / SignupType**（舊系統允許；schema 無限制）
2. **Number 重複檢查排除自己**（`SignupID != ParamSignupID`）
3. **不回寫 Believer 任何欄位**（**故意偏離 legacy**）：堂號/員工類型/固定編號為信眾層級屬性，只在信眾維護頁修改；legacy 會回寫導致「改一筆報名堂號→連動同信眾全部報名」缺陷，新版不重演。堂號仍寫入 SignupLog 快照。回歸測試 `UpdateSignupHandlerTests.Edit_never_writes_back_to_Believer`
4. **不重新分配 Number**：用 request 提供的；衝突就 409
4b. **郵遞區號文字快照必須跟著改**（舊 line 264-266，**2026-07-29 補回**）：UPDATE 一併寫
   `MailZipcode=(SELECT Zipcode FROM dbo.Zipcodes WHERE ZipcodeID=@MailZipcodeId)`（Text 同）。
   這裡漏寫比 POST 漏寫更嚴重——改了區域但文字欄留舊值，收據封面會印出**錯的**號碼而不是空白。
   詳見 [post-signups.md](post-signups.md) §4b 與 [gotchas.md](../../gotchas.md)
4c. **清空要真的清得掉**（2026-07-31 客訴，兩處**刻意偏離 legacy**）：
   - **文牒地址／區號**：舊 `EditSignupForm.cs:255-267` 在文牒欄為空時抄寄件段 → 取消不掉。
     新版空就是空（存 null）。要沿用寄件地址請勾「同寄件地址」（實際填值，所見即所存）。
   - **堂號**：清空送 `""` 而非 `null`——`SignupView.HallName` 是 `COALESCE(S.HallName, B.HallName)`，
     存 null 會被回退成信眾堂號。詳見 [post-signups.md](post-signups.md) §4c
   - 回歸鎖：`UpdateSignupHandlerTests.Edit_cleared_hallName_stored_as_empty_string_not_null`、
     `Edit_cleared_addresses_stay_cleared`、`SignupsEndpointsTests.Clearing_hallName_and_addresses_actually_persists`
5. **SignupLog 同步寫入**（新一筆 audit 紀錄）

## 驗收

- [x] 對應 [edit-signup-form.md](../legacy-coverage/edit-signup-form.md) rows 9-13 ✅
- [x] 含 integration test (404 / 409 / happy path)
