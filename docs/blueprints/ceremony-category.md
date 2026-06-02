---
title: 法會分類維護
purpose: 法會（主分類）與子法會的階層管理；對應舊 CeremonyCategoryForm
status: draft
applicable_when: 要新增/修改法會分類、調整排序、改變刪除規則
related_agents:
  - software-architect-blueprint
  - backend-engineer
  - frontend-architect
related_docs:
  - ../design/database-design.md
  - ../design/api-design.md
keywords: [ceremony, category, 法會, 法會分類, 樹狀, tree, 春季, 中元, 秋季]
last_updated: 2026-05-26
---

## 背景與動機

法會分類是兩層樹（根 → 法會 → 子法會），排序影響列印順序與預繳判斷。舊 CeremonyCategoryForm 用 TreeView 呈現，右鍵選單操作。新版需保留同樣樹狀體驗，並保留三個寫死的根 GUID（春季 / 中元 / 秋季）以維持 DataTrans 相容。

## 範圍

### 做什麼
- 兩層階層管理（根=「法會維護」、Level 1 法會、Level 2 子法會）
- 新增 / 編輯 Title 與 Sort
- 刪除限制：無報名 AND 無子分類才可刪
- TreeView 右鍵 context menu 操作

### 不做什麼
- 三層以上階層（業務上不需要）
- 法會範本複製（手動建立即可）
- 法會時間日期欄位（舊 CeremonyDate 是死欄位）

## 使用者流程

```
1. 進入 /categories
2. 顯示樹（根節點「法會維護」展開）
3. 右鍵 Level 1 節點：
   - 新增子層 → 編輯區填 Title + Sort → 「確認」→ 「新增法會成功！」
   - 編輯 → 修改 Title / Sort → 「確認」→ 「修改法會成功！」
   - 刪除 → 檢查 dependencies → 無 → 「確認刪除嗎？」→ 「刪除法會成功！」
                                  有 → 「已有報名或還有下層法會，無法刪除」
4. 右鍵 Level 2 節點：
   - 編輯 / 刪除（無新增子層）
```

## 設計決策

### 關鍵選擇

- **保留三個根 GUID** 不變動
  - 春季：`18927907-dcad-42b2-8f2a-635c2e0fa98d`
  - 中元：`0c478f0e-787c-448e-ba7b-b1579f3f1fce`
  - 秋季：`3864e4dc-24db-4544-acb3-3351592f6dab`
  - 理由：DataTrans 寫死引用；新增法會用 newsequentialid
- **CHECK constraint 限制兩層**
  - 用 trigger 或 application-level enforce
- **Sort 變更後自動 re-render tree**
  - 舊系統需手動 refresh，新版改為即時

### 取捨

- 取了：保留資料連續性
- 捨了：寫死 GUID 的彈性

## 跨層影響

| 層級 | 是否影響 | 變動摘要 |
|---|---|---|
| 視覺 | 是 | 樹狀介面 |
| 前端 | 是 | categories feature；mat-tree 元件 |
| 後端 | 是 | CategoryService |
| API | 是 | `/categories/*` |
| 資料庫 | 是 | ceremony_categories + 兩層 CHECK |
| 安全 | 部分 | 限 Admin role 可改 |

## 驗收標準

- [ ] 三個根 GUID 不變
- [ ] 兩層階層強制（無法新增第三層）
- [ ] 刪除限制：有報名或有子分類 → 拒絕；訊息「已有報名或還有下層法會，無法刪除」verbatim
- [ ] Sort 編輯後 tree 立即重排
- [ ] 預繳判斷使用 sort（驗證跨頁面整合）

## 風險與未解問題

- 舊 DataTrans 寫死 GUID — 新 Migration 工具是否同樣寫死或改用 lookup？建議改 lookup
- 多人同時編輯同分類 — 暫不處理併發

## 參考資料

- [scratch/04-signup-create-edit-prepay-category.md](../../.scratch/explore/04-signup-create-edit-prepay-category.md) §D
- 舊原始碼：[reference/old/Ceremony/CeremonyCategoryForm.cs](../../reference/old/Ceremony/CeremonyCategoryForm.cs)
