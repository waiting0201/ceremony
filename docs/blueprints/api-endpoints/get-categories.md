---
title: GET /api/v1/categories
purpose: 取得法會分類兩層樹狀結構（前端 dropdown / tree picker 使用）
status: shipped
endpoint: get-categories
http_method: GET
route: /api/v1/categories
legacy_form: CeremonyCategoryForm.cs
legacy_lines: 167-195
related_agents:
  - backend-engineer
related_docs:
  - ../../design/database-design.md
  - ../legacy-coverage/ceremony-category-form.md
keywords: [categories, tree, ceremony, dropdown]
last_updated: 2026-05-27
---

## 規格

`GET /api/v1/categories`，需要 JWT。

### Response

```jsonc
// 200 OK
{
  "items": [
    {
      "id": "18927907-dcad-42b2-8f2a-635c2e0fa98d",
      "title": "春季",
      "sort": 1,
      "children": [
        { "id": "...", "title": "梁皇寶懺", "sort": 1, "children": [] }
      ]
    }
  ],
  "total": 3
}
```

`total` = 根節點數。實際業務上只有 3 個根（春季 / 中元 / 秋季，固定 GUID 見 [database-design.md](../../design/database-design.md)）。

### 錯誤碼

| HTTP | 觸發 |
|---|---|
| 401 | 無 JWT |

## 舊系統對照（forward）

| 舊方法/事件 | 行 | 行為 |
|---|---|---|
| `CeremonyCategoryForm.LoadCeremonyCategorys` | 167-171 | `Where(ParentID==null).OrderBy(Sort)` 取根 |
| `CreateRootNode` | 173-181 | 在 TreeView 加固定「法會維護」根節點 |
| `CreateNode` 遞迴 | 183-195 | 沿 `CeremonyCategorys1` (children nav) 遞迴展開 |

### 業務邏輯區塊

1. **兩層階層限制**（[database-design.md §2 業務規則](../../design/database-design.md)）：根 (ParentID=null) → 第一層；第一層之下不可再新增。**新版 API 不 enforce 在 read path**（信任 DB 既有資料），但設計上葉節點 `children` 永遠 `[]`
2. **排序**：以 `Sort ASC` 排（同層內）
3. **固定根節點 "法會維護"**（舊 UI 元素）：**故意捨棄**——這是 WinForms TreeView UX 慣例，新版前端可自由決定是否顯示
4. **效能**：CeremonyCategorys 規模很小（~3 + 數十個子），單次 `SELECT *` 後在應用層 in-memory 建樹

### 邊界 case

| 場景 | 行為 |
|---|---|
| 無資料 | `{items:[], total:0}` |
| 子節點之子節點（超過 2 層） | 仍掛在第 2 層下；應用層用單層 lookup-by-ParentID，**不再遞迴** |

## 資料存取

```sql
SELECT CeremonyCategoryID, Title, ParentID, Sort
FROM dbo.CeremonyCategorys
ORDER BY Sort
```

Repository: `ICategoryRepository.GetAllAsync()` → `IReadOnlyList<CategoryRow>`，handler 在 app layer 組樹。

## 驗收

- [x] 對應 [ceremony-category-form.md](../legacy-coverage/ceremony-category-form.md) rows 1, 7, 8, 9 ✅
- [x] 含 integration test (200 + tree shape)
- [x] 不外洩任何欄位（schema 簡單，全部欄位可回）
