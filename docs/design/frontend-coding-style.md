---
title: Frontend Coding Style
purpose: 前端各語言詳細風格指南，作為 PR / code review 的客觀依據
applicable_when: 撰寫前端程式碼、code review 前端 PR、設定 lint rule、新人 onboarding
related_agents:
  - frontend-architect
  - mobile-app-engineer
  - code-review-optimizer
related_docs:
  - frontend-design.md
  - visual-design.md
  - ../conventions.md
  - security.md
keywords: [coding-style, frontend, typescript, javascript, dart, style-guide, lint]
last_updated: 2026-07-18 (新增：純數字欄禁 type=number、一律 appNumericInput——IME 組字被丟棄客訴根因)
---

## 0. 元規則

四條原則優先於後續所有具體規則：

1. **可驗證 > 願望**：每條規則應對應到 lint rule、test、或 review checklist；無法自動驗證的規則標 `[guideline]`（建議性，不阻擋 PR）
2. **一致 > 個人偏好**：即使你覺得另一寫法更好，先遵守；要改就改規則本身（PR 改本檔）
3. **清楚 > 簡短**：除非簡短就清楚（聰明寫法不勝過明確寫法）
4. **入口驗證、內部信任**：邊界檢查集中在系統入口（API / form），內部不重複防禦

## 1. 跨語言通則

### 結構
- **Early return** 勝過多層 nesting（≥ 3 層 nesting 觸發拆分檢討）
- 函式單一職責；超過 ~50 行考慮拆分（`[guideline]`）
- 檔案 ≥ 300 行考慮拆分（`[guideline]`）

### 命名
- 表達意圖，不寫冗餘類型字尾（`user` 勝過 `userObj`）
- 避免無意義縮寫（`req` 可，但 `usrPrf` 不可）
- Boolean：`is_` / `has_` / `can_` / `should_` 前綴；避免雙重否定（`isNotEmpty` ❌、用 `hasItems`）

### 註解
- 預設**不寫註解**；只寫**why**，不寫 what（程式碼本身應說明 what）
- TODO / FIXME 必含 owner 或 issue 連結（`// TODO(@tim): #123 ...`）
- 公開 API 用 TSDoc / JSDoc

### Magic numbers / 字串
- 抽常數，命名表達意圖
- UI 文案外部化（i18n），不寫死於元件

### 日期 / 時區 / 金額
- 內部一律 **UTC**；顯示時才轉 local
- 金額用 `decimal.js` / `BigNumber`，**不**用 `number`（浮點誤差）
- 單位明確（`durationMs`、`priceCents`）

### Logging
- 結構化（含 `traceId` / `userId` / `level`）
- level：`debug` / `info` / `warn` / `error`
- **不 log PII / token / password**（cross-link [security.md](security.md)）

### Test 風格
- AAA / GWT pattern（Arrange-Act-Assert / Given-When-Then）
- Test 名稱表達意圖：`should_<behavior>_when_<condition>`
- 避免共享 mutable state；每個 test 獨立可重跑

### Error message
- **對使用者**（UI / toast）：友善、可行動，不洩漏內部結構
- **對開發者**（log / Sentry）：詳細、含 traceId

### Defensive programming 邊界
- 入口（API client / form）做完整驗證
- 內部函式 trust 上層，不重複 null check

## 2. TypeScript / JavaScript

### 基礎
- `strict: true` + `noImplicitAny`
- 偏好 `const`，`let` 僅用於需要 reassign 的場景
- **禁** `any`；必要時用 `unknown` + narrowing
- **禁** `@ts-ignore`；用 `@ts-expect-error` 並寫原因

### Imports
- 順序：node 內建 → 第三方 → 內部模組（依距離由遠到近）
- 由 ESLint `import/order` 規則自動執行
- 不用 default export（提升 refactor 友善度）— 若採用，全專案一致

### Naming
- React 元件：PascalCase（檔名與元件同名）
- Hooks：`useXxx`
- 事件 handler：內部 `handleXxx`、prop `onXxx`
- Type / Interface：PascalCase（不加 `I` 前綴；偏好 `type` 用於聯合，`interface` 用於物件 shape）

### React / Vue / Angular 慣例
- 函式元件 + hooks，**不**用 class component
- `key` 用穩定 ID，**不**用 array index（除非清單只 push 不會重排）
- **不**在 render 中產生新 reference（callback / object）— 用 `useCallback` / `useMemo`
- 副作用集中於 `useEffect`；資料抓取用 React Query / SWR

### 非同步
- 偏好 `async/await`；`.then()` 用於 chain 簡單轉換
- **不**吞錯：每個 `try/catch` 必有後續行動（rethrow / fallback / log）
- 平行操作用 `Promise.all`；不要 sequential await 獨立操作

### 錯誤處理
- Render 錯誤用 Error Boundary
- 全域非預期錯誤接到統一 reporter（Sentry / 自家）
- API 錯誤遵循 [api-design.md](api-design.md) 的錯誤碼結構

### 型別
- 不 export 內部 type（避免外部依賴內部結構）
- 判別聯合用 discriminated union + narrowing
- `Readonly<T>` / `as const` 用於不可變資料

## 3. Dart / Flutter

### 基礎
- `effective_dart` lints 為基礎
- `prefer_final_fields` / `prefer_final_locals` 開啟
- 避免 `dynamic`（除非真的需要）

### Naming
- Widget / Class：PascalCase
- 私有：底線開頭（`_HomePageState`）
- 檔名：`snake_case.dart`

### Widget 慣例
- 偏好 `StatelessWidget`；必要才 `StatefulWidget`
- `build()` 不放商業邏輯（移到 controller / cubit / provider）
- 大 widget 拆成多個小元件（`build` ≥ 50 行觸發檢討）
- `BuildContext` **不**跨 async gap 使用（`if (mounted)` 檢查）

### 錯誤處理
- async 用 `try/catch`
- **禁** `unawaited()` 隱藏 future（除非明確標註原因）

## 4. HTML / CSS（指引）

> 視覺決策（色彩、字體、間距）見 [visual-design.md](visual-design.md)。本段只談**結構規則**。

- 命名：BEM 或 Tailwind utility（**二擇一**，全專案一致）
- **禁** `!important`（除非覆寫第三方）
- **禁** inline style（動態值或 CSP nonce 例外）
- 響應式：mobile-first，breakpoint 與 [frontend-design.md](frontend-design.md) 對齊
- 對比度通過 WCAG AA（一般 4.5:1、大字 3:1）
- **純數字欄禁用 `type="number"`**，一律 `type="text" inputmode="numeric"` + `appNumericInput`（shared/directives/numeric-input.directive.ts）：Chromium number input 會把中文輸入法組字整段丟棄且無回饋（見 [../gotchas.md](../gotchas.md) 2026-07-18 條）；directive 的 CVA 讓 control 值維持 `number | null`，元件端邏輯不變

## 5. 與其他文件的關係

- **架構決策**（用什麼框架、狀態管理）：[frontend-design.md](frontend-design.md)
- **流程約定**（commit、branch、檔名）：[../conventions.md](../conventions.md)
- **視覺系統**（色彩、字體）：[visual-design.md](visual-design.md)
- **安全相關**（PII、XSS、CSP）：[security.md](security.md)
- **常見踩雷**：[../gotchas.md](../gotchas.md)
