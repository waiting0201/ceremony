---
title: DELETE /api/v1/categories/:id
purpose: 刪除法會分類（雙重限制：無報名且無子分類）
status: shipped
endpoint: delete-category
http_method: DELETE
route: /api/v1/categories/:id
legacy_form: CeremonyCategoryForm.cs
legacy_lines: 143-165
related_agents:
  - backend-engineer
related_docs:
  - ../legacy-coverage/ceremony-category-form.md
keywords: [categories, delete, conflict, double-check]
last_updated: 2026-05-27
---

## 規格

`DELETE /api/v1/categories/{id:guid}`，需要 JWT。

### Response: `204 No Content`。

### 錯誤碼

| HTTP | errorCode | message verbatim | 觸發 |
|---|---|---|---|
| 404 | `CATEGORY_NOT_FOUND` | `找不到法會` | id 不存在 |
| 409 | `CATEGORY_HAS_DEPENDENCY` | `已有報名或還有下層法會，無法刪除` | Signups OR 子分類存在 |

## 舊系統對照

| 舊方法/事件 | 行 | 對應 |
|---|---|---|
| `tsmiDelete_Click` | 143-165 | `DeleteCategoryHandler` |
| `!Signups.Any() && !CeremonyCategorys1.Any()` 雙重檢查 | 149 | `HasDependencyAsync` SQL |
| Verbatim 錯誤訊息 | 160 | 同 errorCode |

### 業務規則

- **雙重刪除限制**：必須 (a) 無 Signups 引用 AND (b) 無子分類
- **硬刪除**：CeremonyCategorys 表無 IsEnabled
- **不能刪固定 3 根**（春季 / 中元 / 秋季）：因為它們會有 Signups + 子節點，雙重檢查自然會擋下

## 驗收

- [x] 對應 [ceremony-category-form.md](../legacy-coverage/ceremony-category-form.md) row 6 ✅
- [x] 雙重檢查邏輯逐條對齊
- [x] 含 integration test (404 / 409 / 204)
