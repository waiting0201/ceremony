---
title: POST /api/v1/categories
purpose: 新增法會分類（根節點或子節點）
status: shipped
endpoint: post-categories
http_method: POST
route: /api/v1/categories
legacy_form: CeremonyCategoryForm.cs
legacy_lines: 94-122
related_agents:
  - backend-engineer
related_docs:
  - get-categories.md
  - put-category.md
  - delete-category.md
  - ../legacy-coverage/ceremony-category-form.md
keywords: [categories, create, tree, parent]
last_updated: 2026-05-27
---

## 規格

`POST /api/v1/categories`，需要 JWT。

### Request DTO

```jsonc
{
  "title": "string (required, max 50)",
  "sort": 1,                            // 顯示序
  "parentId": null                      // 根節點 = null；子節點 = 根節點的 GUID
}
```

### Response: `201 Created` + `Location` + `CategoryNode`。

### 錯誤碼

| HTTP | errorCode | message verbatim | 觸發 |
|---|---|---|---|
| 400 | `VALIDATION_REQUIRED` | `請輸入名稱` | title 空 |
| 400 | `VALIDATION_LENGTH` | `名稱最多 50 個字` | title > 50 |
| 404 | `CATEGORY_NOT_FOUND` | `找不到父分類` | parentId 不存在 |
| 422 | `CATEGORY_DEPTH_LIMIT` | `第一層之下不可再新增` | parentId 指向的節點本身有 ParentID（已是第 2 層） |

## 舊系統對照

| 舊方法/事件 | 行 | 對應 |
|---|---|---|
| `CeremonyCategoryForm.btnConfirm_Click` (insert path) | 94-114 | `CreateCategoryHandler` |
| `CurrentCeremonyCategoryID != Guid.Empty` 判定父節點 | 100 | API 用 `parentId` 顯式參數 |

### 業務規則

- **兩層階層限制**（[database-design.md §2](../../design/database-design.md)）：API 必須 enforce
- 三個固定根 GUID（春季 / 中元 / 秋季）已存在 DB，不應透過 API 刪除

## 驗收

- [x] 對應 [ceremony-category-form.md](../legacy-coverage/ceremony-category-form.md) rows 3, 4 (insert path) ✅
- [x] 含 unit + integration tests
