---
title: Legacy Coverage Audit Index
purpose: 規則 A'（反向稽核）的總入口；10 個舊 Form 的覆蓋表概覽 + 上線前 gate
applicable_when: 月度稽核、上線前最終 gate、新進成員了解功能對應狀態
related_agents:
  - backend-engineer
  - qa-test-engineer
related_docs:
  - api-endpoints/README.md
  - ../workflows/qa-testing.md
  - ../workflows/feature-development.md
keywords: [legacy, coverage, audit, 反向稽核, 上線 gate]
last_updated: 2026-06-02
---

> 規則 A'（reverse）的落地處：每個舊 Form 都有一份覆蓋表，逐條方法/事件標記新系統對應狀態。上線前所有 Form 必須 0 個 `pending` / `🤔 待確認`。

## 為什麼要做反向稽核？

forward（每個新 endpoint → 對照舊 code）只能保證**已實作的**正確；漏做的功能它抓不到。
reverse（每個舊 Form → 對照新實作）才能確認**沒漏功能**。兩個方向缺一不可。

## 維護規則

1. **初始稽核（API 開發前必做一次）**：把每個 Form 所有方法 / 事件 / 業務邏輯列入對應檔，狀態先全 `⏳ 缺口待補`
2. **每完成一個 endpoint**：回頭把對應 `<form>.md` 的相關行勾為 `✅ 已實作`
3. **故意捨棄**：列為 `❌ 故意捨棄`，**必填**捨棄理由（例：「Enter=Tab 行為改成標準 submit，符合 Web 慣例」）
4. **待確認**：列為 `🤔 待確認`，必須在 [pending-business-input.md](../pending-business-input.md) 開條目
5. **每月稽核**：更新 `last_audited` 與 `coverage_percentage`
6. **上線前 gate**：以下指令必須 0 命中
   ```bash
   grep -E "⏳ 缺口待補|🤔 待確認" docs/blueprints/legacy-coverage/*.md
   ```

## 10 個 Form 覆蓋表

> ✅ **Baseline 已完成 (2026-05-27)**：所有 10 個 Form 的方法/事件已枚舉完畢，共 **160 個項目**。狀態全 `pending`（等對應 endpoint 實作後逐條勾選）。

| Form | 路徑 | code 行 | 方法數 | 狀態 | 覆蓋率 | 最後稽核 |
|---|---|---:|---:|---|---:|---|
| SignupLogForm | [signup-log-form.md](signup-log-form.md) | 47 | 2 | **complete** | **100%** | 2026-06-02 |
| LoginForm | [login-form.md](login-form.md) | 83 | 3 | **complete** | **100%** | 2026-06-02 |
| MainForm | [main-form.md](main-form.md) | 115 | 8 | **complete** | **100%** | 2026-06-02 |
| CeremonyCategoryForm | [ceremony-category-form.md](ceremony-category-form.md) | 227 | 11 | **complete** | **100%** | 2026-06-02 |
| AdminsForm | [admins-form.md](admins-form.md) | 241 | 14 | **complete** | **100%** | 2026-06-02 |
| BelieverForm | [believer-form.md](believer-form.md) | 516 | 17 | **complete** | **100%** | 2026-06-02 |
| EditSignupForm | [edit-signup-form.md](edit-signup-form.md) | 628 | 20 | **complete** | **100%** | 2026-06-02 |
| LoadPrepayForm | [load-prepay-form.md](load-prepay-form.md) | 925 | 8 | **complete** | **100%** | 2026-05-27 |
| NewSignupForm | [new-signup-form.md](new-signup-form.md) | 1118 | 34 | **complete** | **100%** | 2026-06-02 |
| SignupForm | [signup-form.md](signup-form.md) | 1944 | 43 | **complete** | **100%** | 2026-06-02 |

**總計**：10 個 Form / 5964 行 code / **160 個方法/事件/邏輯區塊**

> **2026-06-02 交叉稽核刷新（進 Electron 前最終比對）**：10/10 Form 全達 100%（complete），上線前 gate grep（`⏳ 缺口待補|🤔 待確認`）**0 row-level 命中**。先前的低覆蓋率多為「前端已 shipped 未回頭打勾」的 stale 紀錄；本輪逐行對程式碼驗證後刷新。NewSignupForm 剩餘 WinForms 列印內部事件統一 ❌ 故意捨棄（改 server-side QuestPDF→PDF + 瀏覽器預覽，不受列印 PoC 影響）。
> ✅ 2026-06-02 補：兩項便利功能已 ship — 新增報名選信眾自動帶入「預繳歷史」（新 `GET /prepay?believerId&year`）+ `BelieverListItem` 補 `IsFixedNumber`（固定編號唯讀顯示；連帶修信眾編輯存檔會把 IsFixedNumber 洗成 false 的既有 bug + 補 checkbox UI）。
> ⚠️ 仍待後續（不阻 gate）：(1) 列印實機驗收 + Worship variant 精修（backlog P1）；(2) 安全簽核 — prod 關閉後門帳號、密碼明文儲存取捨需明確 owner（見 [security.md](../../design/security.md)）。

### 已標註的候選決策（baseline 階段）

實作時可直接參考以下標記為候選的取捨：

- **❌ 候選故意捨棄**（已標註）：
  - `AdminsForm.ProcessCmdKey` (Enter→Tab 轉換) — 新版用標準 Enter=submit
  - `NewSignupForm.btnNextStep_Click` (兩步驟流程) — 新版用單頁表單（mockup v4）
- **🔥 重點業務邏輯**（實作時要小心）：
  - `LoginForm.ValidateUser` 含後門帳號 `weypro`（受 `Auth:BackdoorEnabled` 控制）
  - `LoadPrepayForm.btnConfirm_Click` 780 行單方法、6 case 分群、**無 idempotency 必修**
  - `NewSignupForm.btnConfirm_Click` 含編號分配 UPDLOCK 風險
  - `SignupForm.btnPrint_Click` + `PrintTablet/Text/Worship` 為 RDLC 變體選擇核心，要等列印 PoC 結論
  - `CeremonyCategoryForm.tsmiDelete_Click` 雙重刪除限制（報名 + 預繳）

## 建議稽核順序

從小到大，逐步累積稽核手感：

1. SignupLogForm (47) — 暖身
2. LoginForm (83) — 簡單登入流程
3. MainForm (115) — 主視窗框架
4. CeremonyCategoryForm (227) — CRUD 範本
5. AdminsForm (241) — CRUD + 驗證
6. BelieverForm (516) — CRUD + 地址元件
7. EditSignupForm (628) — 編輯流程
8. LoadPrepayForm (925) — 預繳載入邏輯（核心業務）
9. NewSignupForm (1118) — 報名建立流程（核心業務）
10. SignupForm (1944) — 報名查詢 + RDLC 變體選擇（最複雜）

## 相關文件

- [api-endpoints/README.md](api-endpoints/README.md) — forward 索引（新 endpoint → 舊 code）
- [qa-testing.md](../../workflows/qa-testing.md) — 上線前 QA 流程含 legacy coverage gate
- [feature-development.md](../../workflows/feature-development.md) — DoD 含「已更新對應 legacy-coverage」
