---
title: POST /api/v1/believers
purpose: 新增信眾
status: shipped
endpoint: post-believers
http_method: POST
route: /api/v1/believers
legacy_form: BelieverForm.cs
legacy_lines: 101-153, 320-351
related_agents:
  - backend-engineer
related_docs:
  - put-believer.md
  - ../legacy-coverage/believer-form.md
keywords: [believers, create, post]
last_updated: 2026-05-27
---

## 規格

`POST /api/v1/believers`，需要 JWT。

### Request DTO (`BelieverUpsertRequest`)

```jsonc
{
  "employeeType": 1,                   // 1=非員工, 2=大殿, 3=地藏殿
  "hallName": "string? (max 10)",
  "name": "string (required, max 30)",
  "phone": "string? (max 30)",          // 應用層做全→半形轉換
  "isFixedNumber": false,
  "mailZipcodeId": 1,                  // -1 / null = 未選
  "mailAddress": "string (required, max 250)",
  "textZipcodeId": null,
  "textAddress": "string? (max 250)",
  "livingNames": ["", "", "", "", "", ""],   // 必須 6 元素
  "deadNames":   ["", "", "", "", "", ""]    // 必須 6 元素
}
```

### Response

`201 Created` + `Location: /api/v1/believers/{guid}` + 完整 `BelieverListItem`。

### 錯誤碼

| HTTP | errorCode | message verbatim | 觸發 |
|---|---|---|---|
| 400 | `VALIDATION_REQUIRED` | `請輸入姓名` | name trimmed 為空（line 103-107） |
| 400 | `VALIDATION_REQUIRED` | `請輸入寄件地址` | mailAddress trimmed 為空（line 110-114） |
| 400 | `VALIDATION_INVALID` | `員工類別錯誤` | employeeType 不在 1/2/3 |
| 400 | `VALIDATION_LENGTH` | 各種長度 | 任一欄位超過 DB 上限 |
| 400 | `VALIDATION_INVALID` | `名單必須為 6 個元素` | livingNames / deadNames 長度 ≠ 6 |
| 401 | – | – | 無 JWT |

## 舊系統對照（forward）

| 舊方法/事件 | 行 | 對應新版 |
|---|---|---|
| `btnConfirm_Click` insert path | 119-152 | `CreateBelieverHandler.HandleAsync` |
| `txtPhone_Validating` | 320-351 | `[Required+Regex]` validator（`^0[0-9]*$`），舊邏輯只在 form-event；新版在 handler |
| `Strings.StrConv(text, VbStrConv.Narrow)` 全→半形 | line 128 | `ToNarrow` helper（U+FF01–U+FF5E → ASCII） |
| `MailZipcodeID/TextZipcodeID = -1 視為未選` | line 146-147 | request `null` 或 `-1` 皆視為未選（轉成 `null` 存 DB） |

## 邊界 case

| 場景 | 舊行為 | 新版 |
|---|---|---|
| name 空 | MessageBox + focus + return | 400 verbatim |
| mailAddress 空 | MessageBox + focus + return | 400 verbatim |
| phone 含全形數字 | `StrConv Narrow` 轉半 | 同 |
| zipcode -1 | 不寫入 (留 null) | 同 |
| 6 個 LivingName 全空 | 允許 | 允許（DB 欄位 nullable） |

## 資料存取

```sql
INSERT INTO dbo.Believers (
  BelieverID, EmployeeType, HallName, Name, Phone, IsFixedNumber,
  MailZipcodeID, MailAddress, TextZipcodeID, TextAddress,
  LivingNameOne, ..., DeadNameSix
) VALUES (@BelieverId, @EmployeeType, ...)
```

`BelieverID` 由應用層 `Guid.NewGuid()` 產生（沿用舊 line 123）。

## 驗收

- [x] 對應 [believer-form.md](../legacy-coverage/believer-form.md) rows 3, 5, 12 ✅
- [x] 中文錯誤訊息 verbatim
- [x] 含 unit + integration tests
