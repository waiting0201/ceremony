---
title: <HTTP METHOD> <route>
purpose: <一句話：這個 endpoint 做什麼>
status: draft  # draft / in-progress / shipped
endpoint: <verb>-<resource>  # 例：post-signups
http_method: <GET | POST | PUT | PATCH | DELETE>
route: <例：/api/v1/signups>
legacy_form: <對應舊 WinForms Form 檔名，例：NewSignupForm.cs；無對應寫 N/A 並說明>
legacy_lines: <例：1148-1228；無寫 N/A>
related_agents:
  - backend-engineer
  - qa-test-engineer
related_docs:
  - ../../design/api-design.md
  - ../../design/backend-design.md
  - ../../design/database-design.md
  - ../legacy-coverage/<form>.md
keywords: [<關鍵字>]
last_updated: YYYY-MM-DD
---

## 規格

### Route + Method

`<HTTP METHOD>` `<route>`

### Request DTO

```jsonc
{
  // 欄位 + 型別 + 必填 + 驗證規則
}
```

### Response DTO

```jsonc
// 200 / 201
{
  // ...
}
```

### 錯誤碼

| HTTP | errorCode | message (verbatim 對齊舊 MessageBox) | 觸發條件 |
|---|---|---|---|
| 400 | `...` | `...` | ... |
| 409 | `...` | `...` | ... |

詳見 [api-design.md 業務錯誤碼表](../../design/api-design.md)。

## 舊系統對照（規則 A — forward）

> 必填：每個欄位 / 驗證 / 邊界 case 都要從舊 code 擷取對應行號。沒對到的列 `N/A (新需求)` 並說明來源。

### 對應 Form / 事件

| 舊方法/事件 | 舊 code line ref | 行為摘要 |
|---|---|---|
| 例：`NewSignupForm.btnConfirm_Click` | `NewSignupForm.cs:580-720` | 表單驗證 + 編號分配 + 插入 Signup |
| 例：`NewSignupForm.Number_KeyPress` | `NewSignupForm.cs:842-855` | 編號避 4 規則 |

### 驗證規則對照

| 欄位 | 舊驗證 (line) | 新驗證 | 差異 / 取捨 |
|---|---|---|---|
| 例：`year` | `NewSignupForm.cs:120` (regex `^1[0-9]{2}$`) | `[FromBody] int year` + FluentValidation `Matches("^1[0-9]{2}$")` | 等價 |

### 業務邏輯區塊

逐區塊列出舊 code 的邏輯流程，標註新版實作差異：

1. **<邏輯 1，例：編號分配>**（舊：`SignupForm.cs:1148-1228`）
   - 舊行為：...
   - 新實作：...
   - 差異 / 為什麼這樣改：...

### 邊界 case

| 場景 | 舊 code 行為 (line) | 新版行為 | 對應測試 |
|---|---|---|---|
| 例：3 個以上亡者 + ParaFontSize | `SignupForm.cs:1554-1593` 動態縮字 | 沿用 | TestSignupParaFontSize |

## 業務規則

- 對應 [business-rules-implicit.md](../business-rules-implicit.md) 第 X 條：...
- 對應 [glossary.md](../glossary.md)：「...」

## 資料存取

### 相關資料表

| Table | 用途 | 索引 | 注意 |
|---|---|---|---|
| `Signup` | 主表 | – | UPDLOCK 編號分配 |

### 預期 SQL

```sql
-- 範例
SELECT TOP 50 * FROM dbo.Signup WHERE Year = @year AND Number = @number
```

### Repository 方法（舊系統參照）

| 舊 Service / Repository 方法 | line | 行為 |
|---|---|---|
| `SignupService.Insert` | – | – |

## 驗收標準

- [ ] 規格段所有欄位有 DTO 型別 + 驗證 + 範例
- [ ] 舊系統對照表已逐行對到舊 code line ref（無遺漏）
- [ ] 錯誤碼與舊 MessageBox 文字 verbatim
- [ ] 對應的 `legacy-coverage/<form>.md` 行已勾選為 `✅ 已實作`
- [ ] 含舊系統行為對照測試（XUnit + 舊 fixture / golden master）
- [ ] 通過 [code-review](../../workflows/code-review.md)
- [ ] 通過 [qa-testing](../../workflows/qa-testing.md)

## 風險與未解問題

- ...

## 參考

- 舊 Form：`reference/old/Ceremony/<Form>.cs:<line>-<line>`
- Repository：`reference/old/Ceremony.Models/Repository/...`
- Service：`reference/old/Ceremony.Service/...`
- Legacy coverage：[../legacy-coverage/<form>.md](../legacy-coverage/<form>.md)
