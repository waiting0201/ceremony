---
title: GET /api/v1/signups/:id/logs
purpose: 取得某筆報名的變更紀錄（審計快照）
status: shipped
endpoint: get-signup-logs
http_method: GET
route: /api/v1/signups/:id/logs
legacy_form: SignupLogForm.cs
legacy_lines: 26-45
related_agents:
  - backend-engineer
related_docs:
  - ../legacy-coverage/signup-log-form.md
  - ../../design/database-design.md
keywords: [signups, logs, audit, snapshot]
last_updated: 2026-05-27
---

## 規格

`GET /api/v1/signups/{id:guid}/logs`，需要 JWT。

### Response

```jsonc
// 200 OK
{
  "items": [
    {
      "id": "guid",
      "signupId": "guid",
      "year": 115,
      "ceremonyCategoryTitle": "春季",
      "signupType": 1,
      "numberTitle": "No",
      "number": 42,
      "hallName": "...",
      "name": "...",
      "phone": "...",
      "fee": 1200,
      "livingNames": ["", ...×6],
      "deadNames":   ["", ...×6],
      "mailCity": "", "mailZone": "", "mailAddress": "",
      "textCity": "", "textZone": "", "textAddress": "",
      "remark": "",
      "prepayYear": null,
      "prepayCeremonyCategoryTitle": null,
      "admin": "sa@system.local",
      "createDate": "2026-05-27T..."
    }
  ],
  "total": 1
}
```

按 `Createdate DESC` 排（**最新在前**，對齊舊 `SignupLogForm.LoadSignupLog` 的 `OrderByDescending(Createdate)`）。
> ⚠️ 2026-06-02 修正：先前誤用 `ASC`（最舊在前），與舊系統相反；交叉稽核發現後改回 `DESC`。

### 邊界

- signupId 不存在的報名 → 仍回 200 + `{items:[], total:0}`（log 表獨立存在，無 FK；對齊舊行為）
- signupId 非 GUID → 400

## 舊系統對照

| 舊方法/事件 | 行 | 對應 |
|---|---|---|
| `SignupLogForm()` constructor | 26-37 | controller |
| `LoadSignupLog()` | 39-45 | `ListSignupLogsHandler` + `SignupLogRepository.GetBySignupIdAsync` |

## 業務規則

- **無 FK 關聯**：原 Signup 即使被刪除，logs 仍存在（[database-design.md §6 設計重點](../../design/database-design.md)）
- **無 action 欄位**：「第一筆 = 新增」推論由前端做（API 不額外加欄位）

## 資料存取

```sql
SELECT SignupLogID, SignupID, Year, CeremonyCategoryTitle, SignupType,
       HallName, Name, Phone, NumberTitle, Number, Fee,
       LivingNameOne..Six, DeadNameOne..Six,
       MailCity, MailZone, MailAddress,
       TextCity, TextZone, TextAddress,
       Remark, PrepayYear, PrepayCeremonyCategoryTitle,
       Admin, Createdate
FROM dbo.SignupLogs
WHERE SignupID = @SignupId
ORDER BY Createdate DESC
```

## 驗收

- [x] 對應 [signup-log-form.md](../legacy-coverage/signup-log-form.md) rows 1, 2 ✅
- [x] 含 integration test (空 result + happy path)
