---
title: PUT /api/v1/categories/:id
purpose: 編輯法會分類的 Title + Sort（不改 ParentID）
status: shipped
endpoint: put-category
http_method: PUT
route: /api/v1/categories/:id
legacy_form: CeremonyCategoryForm.cs
legacy_lines: 115-133
related_agents:
  - backend-engineer
related_docs:
  - post-categories.md
  - ../legacy-coverage/ceremony-category-form.md
keywords: [categories, update, put]
last_updated: 2026-05-27
---

## 規格

`PUT /api/v1/categories/{id:guid}`，需要 JWT。

### Request DTO

```jsonc
{
  "title": "string (required, max 50)",
  "sort": 1
}
```

**不能改 ParentID**（樹結構固定後不變；要搬位置請刪後重建）。

### Response: `200 OK` + `CategoryNode`。

### 錯誤碼

| HTTP | errorCode | 觸發 |
|---|---|---|
| 400 | `VALIDATION_REQUIRED` | title 空 |
| 400 | `VALIDATION_LENGTH` | title > 50 |
| 404 | `CATEGORY_NOT_FOUND` | id 不存在 |

## 舊系統對照

| 舊方法/事件 | 行 | 對應 |
|---|---|---|
| `btnConfirm_Click` (else path) | 115-127 | `UpdateCategoryHandler` |

## 驗收

- [x] 對應 [ceremony-category-form.md](../legacy-coverage/ceremony-category-form.md) row 4 (update path) ✅
