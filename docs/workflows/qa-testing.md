---
title: QA Testing Workflow
purpose: 規範 QA 品質審查的執行步驟，含靜態 + runtime 雙層次；以及法會報名系統重構的完整測試計畫
applicable_when: 要做品質審查、PR 進 QA 階段、上線前驗收、規劃測試 case
related_agents:
  - qa-test-engineer
related_docs:
  - code-review.md
  - ../design/security.md
  - ../business-rules-implicit.md
  - ../glossary.md
  - ../blueprints/printing-reports.md
keywords: [qa, test, 測試, 品質, 審查, runtime, lighthouse, unit, integration, e2e]
last_updated: 2026-05-27
---

## 兩層審查

### 靜態審查（必跑）
- **agent**：qa-test-engineer
- **內容**：邏輯、邊界、錯誤處理、安全、效能、可測試性、可讀性
- **工具**：依專案技術棧自動偵測對應 linter（read-only）

### Runtime 審查（前端專案必跑）
- **agent**：qa-test-engineer（含 chrome-devtools-mcp）
- **內容**：
  - Console 錯誤與警告
  - Network 請求審查（4xx/5xx、CORS、慢請求）
  - Performance / Web Vitals（LCP / CLS / INP）
  - 可訪問性與 DOM 檢查

## 審查報告格式

由 qa-test-engineer 產出，含：

- 🔴 嚴重問題（Critical）
- 🟡 警告（Warning）
- 🔵 建議（Suggestion）
- ❓ 疑問（Questions）
- ✅ 優點（Positives）
- 🧪 Linter / 靜態分析結果
- 🌐 瀏覽器執行期檢查結果（前端適用）

## QA 不通過的處理

- 🔴 必修
- 🟡 由開發者判斷是否修，未修須有理由
- 🔵 / ❓ 視情況討論

## 與 code-review 的差別

- code-review：**主動**提改善建議，可改 code
- qa-testing：**只**找問題與提問，不改 code
- 兩者並行不衝突；複雜功能建議都跑

## 上線前 sanity check

- [ ] 所有 🔴 已修
- [ ] 通過 [security 檢核](../design/security.md)
- [ ] runtime 審查（若前端）四面向至少 ⚠️ 以上
- [ ] 對應 design/ doc 已同步
- [ ] blueprint 驗收標準全打勾
- [ ] **Legacy coverage 稽核 100% 完成**（見下方「Legacy coverage 稽核」）
- [ ] **無 secret 進 repo**：`grep -rE "Password=[^_<]" .` 必須 0 命中（任何明文密碼，不限本專案的特定密碼字串）

## Legacy coverage 稽核（規則 A' reverse）

QA 階段必跑覆蓋稽核，確認沒漏功能：

1. 開 [../blueprints/legacy-coverage/README.md](../blueprints/legacy-coverage/README.md)
2. 確認 10 個 Form 的覆蓋率皆 100%（總計 = ✅ 已實作 + ❌ 故意捨棄）
3. **上線前 gate**（以下指令必須 0 命中）：
   ```bash
   grep -E "⏳ 缺口待補|🤔 待確認" docs/blueprints/legacy-coverage/*.md
   ```
4. 任何 `❌ 故意捨棄` 必須有理由 + 業務確認簽核（記載於 [../pending-business-input.md](../pending-business-input.md)）
5. 月度稽核：更新每份 `last_audited` 與 `coverage_percentage`，與最新 code 對齊

## 對應 RPEV

本流程對應 [research-plan-execute-verify.md](research-plan-execute-verify.md) 的 **V (Verify)** 階段：系統化驗收（含 runtime 觀察）。

---

# 法會報名系統 — 測試計畫

四層測試金字塔；每層的範圍、工具、目標不同：

```
            ┌─────────────────┐
            │   E2E (Playwright)│  全棧驗收（少而重）
        ┌───┴───────────────────┴───┐
        │  整合 (xUnit + LocalDB)    │  跨層整合
    ┌───┴───────────────────────────────┴───┐
    │  單元 (xUnit / Jest)                   │  純邏輯（多而快）
┌───┴───────────────────────────────────────────┴───┐
│              靜態 (lint / typecheck / Roslyn)        │  自動跑（極快）
└───────────────────────────────────────────────────────┘
```

## 1. 靜態（每次 commit）

- **後端**：`dotnet build` + Roslyn analyzers + nullable warnings as errors
- **前端**：`ng lint` + `tsc --noEmit` + Prettier
- **CI**：失敗即擋 PR

## 2. 單元測試（純邏輯，無 IO）

### 後端優先測（**避 4 / 編號 / 堂號 / NumberTitle**）

| 模組 | 測試類 | 覆蓋 case |
|---|---|---|
| `AvoidFourFormatter.Format` | AvoidFourFormatterTests | 0~999 全範圍 + 邊界（4 / 14 / 40 / 44 / 140 / 144 / 400 / 404）詳見 [business-rules-implicit.md](../business-rules-implicit.md) §2 |
| `HallNameSplitter.Split` | HallNameSplitterTests | 2 字 / 3 字 / 4 字 / 含 hyphen / 空 / null |
| `NumberTitleResolver.From(SignupType)` | NumberTitleTests | 1→No / 2→寺 / 3→觀 / 4→普 / 5→郵 / 其他→throw |
| RDLC 變體選擇 | TabletVariantSelectorTests | 9 變體的 3×3 矩陣 [printing-reports.md](../blueprints/printing-reports.md) |
| | TextVariantSelectorTests | tmpText vs tmpTextTwo |
| | WorshipVariantSelectorTests | 6 變體 by highest LivingName position |
| ParaFontSize 計算 | TabletFontSizeTests | DeadName 字長 7 字邊界 |
| Address fallback | AddressFallbackTests | Text 空 → 用 Mail；否則保留 |
| Phone 全/半形 | PhoneNormalizerTests | 全形 → 半形；空 → 空 |
| 民國年 helper | TaiwanCalendarTests | 西元 ↔ 民國年；當年計算 |
| Predicate builder | SignupSearchPredicateTests | AND / OR 組合；全空 → 全部 |

### 前端 Signal store 測試

| Store | 測試 |
|---|---|
| AuthStore | login / logout / token refresh / state derivation |
| SignupSearchStore | filter 變化觸發 search；分頁；reset |
| CategoriesStore | tree 載入；cache invalidate |

工具：Angular TestBed + `signal()` 直接讀寫驗證

## 3. 整合測試（含 DB）

**重要**：不 mock DB（避免舊系統的 mock/prod 不一致痛點）。用 **Testcontainers** 啟動真 MSSQL container（或 LocalDB）。

| 測試 | 範圍 |
|---|---|
| LoginHandlerTests | 真 DB 查 Admins、明文比對、JWT 簽發 |
| BelieverSearchHandlerTests | 6 種搜尋組合 LINQ → SQL |
| BelieverDeleteHandlerTests | 有 Signups 拒絕；多選整批中止 |
| SignupCreateHandlerTests | 三實體 atomic（Believer + Signup + SignupLog）；transaction rollback |
| SignupNumberGenerationTests | 10 concurrent 同時新增同 (Year, Ceremony, Type)，編號無碰撞 |
| LoadPrepayHandlerTests | 6 case 各驗證；gap-filling 演算法；**idempotency 檢查**（重跑回 409） |
| CategoryDeleteHandlerTests | 雙重檢查（無 Signups + 無子分類） |
| PrintReportHandlerTests | 5 報表類型；20 個樣本資料測模板選擇 |

## 4. E2E 測試（Playwright）

僅做**關鍵使用者流程**，每次 release 跑：

| Test scenario | 步驟 |
|---|---|
| 登入流程 | 登入頁 → 輸帳密 → 成功進主畫面 / 失敗顯訊息 / 後門帳號可用 / 5 次失敗鎖定 |
| 新增報名（完整） | 主畫面 → 新增報名 → 兩步驟 wizard → 填完所有欄位 → 儲存 → 列印資料卡 |
| 編輯報名 | 報名維護 → 搜尋 → 右鍵編輯 → 改欄位 → 儲存 → 確認 SignupLog 寫入 |
| 信眾搜尋 | 信眾維護 → 各種搜尋條件 → grid 結果 → 帶入編輯區 |
| 載入預繳 | 預繳載入 → 選來源/目標 → 預覽 → 載入 → 確認筆數 |
| 批次列印 | 報名維護 → 多選 → 右鍵列印薦牌 → PDF 預覽 / 存檔 |
| 法會分類維護 | 樹狀 → 新增子層 → 編輯 → 刪除（受限） |
| 資料備份 | 主畫面備份 → 確認檔案產生 |

## 5. 列印對位驗收（手動 + 對拍）

QuestPDF 產出與舊 RDLC 列印物的版面比對：

| 報表 | 樣本數 | 驗收方法 | 容忍度 |
|---|---|---|---|
| 資料卡 | 5 種資料組合 | A5 雙印疊合 | ±0.2cm |
| 收據 | 上下聯各 5 筆 | A4 雙印疊合 | ±0.2cm |
| 薦牌 9 變體 | 各 2 筆 | 牌位紙疊合 | **±0.1cm**（套印預印紙更嚴） |
| 文牒 2 變體 | 各 2 筆 + 含垂直地址 | 超寬紙疊合 | ±0.2cm |
| 普桌 6 變體 | 各 2 筆 | A4 對 worship2 背景 | ±0.2cm |

每張紙：對齊四角 → 透光或燈箱檢查每個欄位偏移。

## 6. 效能測試

詳見 [performance.md](../design/performance.md) §「效能預算」。

| 場景 | 工具 | 目標 |
|---|---|---|
| 搜尋 500k Signups P95 | NBomber / k6 | < 500ms（DB 索引版）/ < 1s（無索引版） |
| 編號生成 10 concurrent | xUnit + Parallel.For | 無碰撞、無 deadlock |
| 100 筆批次列印 | 整合測試 + stopwatch | < 5s |
| Excel 匯出 5k 列 | 整合測試 | < 3s |
| Login P95 | k6 | < 500ms |

## 7. 安全測試

詳見 [security.md](../design/security.md) §驗收。

- OWASP ZAP baseline scan
- 登入失敗鎖定觸發測試
- JWT 過期 / 無效 token 拒絕測試
- SQL injection 嘗試（Dapper 參數化驗證）
- PII log mask 確認

## 8. 跨平台測試（Electron）

| OS | 必測 |
|---|---|
| Windows 10 / 11 | 主要部署目標；完整 E2E |
| macOS 14+ | smoke + 列印測試 |
| Linux (Ubuntu 22) | smoke 即可 |

特別測試：
- 標楷體字型在各平台是否正確 render
- 印表機對話框
- 系統檔案另存對話框

## 9. UX 回歸（與舊系統對拍）

| 項目 | 方法 |
|---|---|
| 欄位位置 | 並排截圖比對；誤差 ≤ 8px |
| Tab 順序 | 從 LoginForm 開始一路 Tab 一遍 |
| 按鈕文字 | grep 確認全部 verbatim（「確認」「取消」「新增」「修改」「刪除」「搜尋」「下一步」「匯出Excel」） |
| 錯誤訊息 | 觸發每條驗證錯誤，verbatim 比對 |
| 列印格式對話 | CustomDialogForm 對等元件 |
| 操作流程步驟數 | 主要流程與舊版相同 |

## 10. 驗收測試（業務）

業務承辦人實機操作清單：

- [ ] 用真實上次法會資料跑完整流程
- [ ] 列印實體報表 → 業務確認版面與舊系統一致
- [ ] 中元高峰情境模擬（大量同時報名、列印）
- [ ] 預繳載入後核對筆數與固定編號保留
- [ ] 信眾搜尋找出特定家族（多名陽上/往生）
- [ ] 變更紀錄查詢與舊系統並排

## 測試資料策略

- **dev** 用 Docker MSSQL + 既有 Ceremony DB 備份 restore（**隱去個資**：姓名隨機化、電話替換）
- **CI** 用 Testcontainers + seed data（業務典型 case）
- **staging** 用 prod 副本（**完整個資**，限存取）

## 失敗策略

任一 🔴 失敗 → 中止部署、回滾、修正後重跑。
