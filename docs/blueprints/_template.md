---
title: <功能名稱>
purpose: <一句話：這個功能解決什麼問題>
status: draft
applicable_when: 要修改/擴充 <功能名稱>、要理解 <功能名稱> 背景時
related_agents:
  - software-architect-blueprint
  - <實作層 agent，例如 backend-engineer / frontend-architect>
related_docs:
  - ../design/frontend-design.md
  - ../design/backend-design.md
  - ../design/api-design.md
  - ../design/database-design.md
keywords: [<功能關鍵字>]
last_updated: YYYY-MM-DD
---

## 背景與動機

<為什麼要做這個功能？要解決什麼痛點？利害關係人是誰？>

## 範圍

### 做什麼
- <功能 1>
- <功能 2>

### 不做什麼
- <明確排除的範圍，避免 scope creep>

## 使用者流程

```
1. 使用者 ...
2. 系統 ...
3. ...
```

或以 sequence diagram / state diagram 描述。

## 設計決策

### 關鍵選擇
- **<決策 A>**：選 X 而非 Y。理由：...
- **<決策 B>**：...

### 取捨
- 取了什麼、捨了什麼

## 跨層影響

| 層級 | 是否影響 | 變動摘要 |
|---|---|---|
| 視覺 | <是/否> | <改了哪些 design system 元素？> |
| 前端 | <是/否> | <新增/改動哪些元件、頁面、狀態？> |
| 後端 | <是/否> | <新增/改動哪些服務、模組？> |
| API | <是/否> | <新增/改動哪些 endpoint、錯誤碼？> |
| 資料庫 | <是/否> | <新增/改動哪些表、欄位、索引？> |
| 基礎建設 | <是/否> | <新環境變數？新雲端服務？> |
| 安全 | <是/否> | <新權限？新 PII 欄位？> |

## 驗收標準

- [ ] <可測試條件 1>
- [ ] <可測試條件 2>
- [ ] 通過 [code-review](../workflows/code-review.md)
- [ ] 通過 [qa-testing](../workflows/qa-testing.md)
- [ ] 相關 design/ doc 已同步更新

## 風險與未解問題

- <已知風險>
- <待釐清問題>

## 參考資料

- <連結、討論、原始需求>
