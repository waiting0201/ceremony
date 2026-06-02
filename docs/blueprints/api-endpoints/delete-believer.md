---
title: DELETE /api/v1/believers/:id
purpose: 硬刪除信眾（受報名衝突保護）
status: shipped
endpoint: delete-believer
http_method: DELETE
route: /api/v1/believers/:id
legacy_form: BelieverForm.cs
legacy_lines: 211-250
related_agents:
  - backend-engineer
related_docs:
  - ../legacy-coverage/believer-form.md
  - ../../business-rules-implicit.md
keywords: [believers, delete, soft-delete, conflict]
last_updated: 2026-05-27
---

## 規格

`DELETE /api/v1/believers/{id:guid}`，需要 JWT。

### Response

`204 No Content`。

### 錯誤碼

| HTTP | errorCode | message verbatim | 觸發 |
|---|---|---|---|
| 404 | `BELIEVER_NOT_FOUND` | `找不到信眾` | id 不存在 |
| 409 | `BELIEVER_HAS_SIGNUPS` | `{Name} 已有報名資料，不能刪除！` | 信眾尚有 Signups（line 220-223） |

## 舊系統對照

| 舊方法/事件 | 行 | 對應新版 |
|---|---|---|
| `tsmiDelete_Click` | 211-250 | `DeleteBelieverHandler.HandleAsync` |
| `believer.Signups.Any()` 衝突檢查 | 220 | `IBelieverRepository.HasSignupsAsync` |
| `Delete + SaveChanges` (硬刪除) | 236-237 | `DELETE FROM dbo.Believers WHERE BelieverID=@id` |

## ⚠️ 重要：硬刪除而非軟刪除

舊系統 Believers 表**無 IsEnabled 欄位**，DELETE 是硬刪除（line 236）。沿用此行為。
對比：[Admins](post-admins.md) 是軟刪除（IsEnabled=false）。

## 邊界 case

| 場景 | 舊行為 | 新版 |
|---|---|---|
| id 不存在 | 應該不會發生（grid 選列） | 404 |
| 有 Signup | MessageBox + return | 409 verbatim |
| 多筆選刪除中有一筆有 Signup | 整批中止（line 222-223） | 本 endpoint 單筆；批次刪除由前端逐個呼叫處理 |

> **行為差異 — 已記錄**：舊版 multi-select 整批中止，新版 REST API 是單筆操作；前端負責「批次刪除中遇衝突如何處理」UX。

## 驗收

- [x] 對應 [believer-form.md](../legacy-coverage/believer-form.md) row 8 ✅
- [x] 報名衝突 409 含信眾姓名
- [x] 含 integration test (204 / 404 / 409 三 case)
