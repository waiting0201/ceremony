---
title: 待業務 / 客戶確認清單
purpose: 文件化完成度已達上限後、剩餘需要業務或客戶提供資訊的問題清單；逐項確認後回填至對應 docs
applicable_when: 開會前準備、上線前準備、實作中遇到不確定的業務規則
related_agents:
  - software-architect-blueprint
  - system-analyst
related_docs:
  - status.md
  - blueprints/auth-and-admin.md
  - blueprints/prepay-loading.md
  - blueprints/printing-reports.md
  - design/database-design.md
  - design/security.md
keywords: [pending, 待確認, 業務輸入, 客戶確認, gap]
last_updated: 2026-06-29 (B13 定案信眾層級＋方案 C 已實作)
---

> 本檔列出**需要業務 / 客戶 / DBA 提供資訊**才能完成的項目。每項含：問題、為何重要、影響哪些 docs、預計確認時機。
>
> 確認後請：
> 1. 把答案寫進對應 doc
> 2. 把本檔該項標 ✅ 並寫日期 + 來源（誰回答的）
> 3. 不要刪除歷史條目（留作 audit trail）

## A. DB 連線後可立即取得（技術性，不必業務）

連你本機 SQL Server 跑 SQL 即可。詳細指令見 [database-design.md](design/database-design.md) §「部署前要對 DB 跑的查詢」。

| # | 項目 | 影響 docs | 狀態 |
|---|---|---|---|
| A1 | SignupView 完整 DDL | database-design.md | ⬜ |
| A2 | BelieverView 完整 DDL | database-design.md | ⬜ |
| A3 | 各表現有資料量（Signups / SignupLogs / Believers / 各年）| performance.md / database-design.md | ⬜ |
| A4 | 既有非叢集索引清單（DBA 可能手動加過）| database-design.md / performance.md | ⬜ |
| A5 | CeremonyCategorys 完整內容（確認跨年共用同 GUID）| business-rules-implicit.md | ⬜ |
| A6 | 既有 Admins 帳號數與 Password 長度分佈 | security.md / auth-and-admin.md | ⬜ |
| A7 | SQL Server 版本（@@VERSION）+ Edition | database-design.md / performance.md | ⬜ |
| A8 | 現有 SQL Server Agent 備份排程細節 | infrastructure.md | ⬜ |

## B. 業務 / 客戶需要回答

### B1. 系統 SuperAdmin `sa@system.local` 是否保留？

- **目前決策**：保留。**2026-06-18 已移除舊系統 weypro 後門**，改為系統內建 SuperAdmin `sa@system.local`（非 DB，受 `Auth:SuperAdminEnabled` 控制，可關閉）。
- **影響**：[security.md](design/security.md)、[auth-and-admin.md](blueprints/auth-and-admin.md)、[infrastructure.md](design/infrastructure.md)（`Auth:SuperAdmin*`）
- **建議**：prod 上線前評估是否關閉（`SuperAdminEnabled=false`），改由 DBA 直接 SSMS 維護 Admins。
- **確認時機**：上線前
- **狀態**：⬜（待業務確認 prod 是否啟用）

### B2. 同時上線使用者數？

- **目前假設**：3-5 人
- **影響**：[performance.md](design/performance.md) connection pool 大小、UPDLOCK 設計、SignalR 必要性
- **理想答案**：「同時上線 N 人；高峰時段 M 人」
- **確認時機**：實作前
- **狀態**：⬜

### B3. 法會旺季時段與每日尖峰報名筆數？

- **目前假設**：中元期間最忙，無實際數據
- **影響**：效能測試樣本、上線時機選擇、訓練時程
- **理想答案**：「春季法會 X 月、中元 Y 月、秋季 Z 月；高峰每日約 N 筆報名」
- **確認時機**：訓練排程前
- **狀態**：🟡 部分定案（2026-06-23）— **月份範圍已定**：1-4月春季 / 5-8月中元 / 9-12月秋季（見 [business-rules-implicit.md](business-rules-implicit.md) §17，已實作於報名表單自動帶季別）；**尚待**：每日尖峰報名筆數

### B4. 印表機型號 / 紙張供應商 / 預印格式樣本

- **目前狀況**：未知
- **影響**：[printing-reports.md](blueprints/printing-reports.md) 列印對位驗收
- **需要**：
  - 牌位紙樣本（11.5×25.4cm 預印格式）
  - 文牒紙樣本（36.5×26.2cm 預印格式）
  - 普桌紙樣本（21×29.6cm 預印格式，含 worship2 紋飾）
  - 雙聯收據紙樣本（21×59.4cm 或拆兩張 A4）
  - 印表機型號清單
- **確認時機**：列印模組開發前
- **狀態**：⬜

### B5. 信眾刪除流程（個資法相關）

- **目前狀況**：舊系統「有報名不能刪」；客戶若要支援當事人刪除請求需新流程
- **影響**：[security.md](design/security.md) §資料保留與銷毀
- **需要決策**：
  - 信眾申請刪除時的處理（軟刪 / 標記 / 30 日後 hard delete？）
  - 已有 Signups 的信眾如何處理？（保留 Signups 但刪 Believer？匿名化？）
- **確認時機**：上線前合規檢查
- **狀態**：⬜

### B6. SignupType 是否還有擴充計畫？

- **目前**：1=一般、2=寺方、3=觀音會、4=普桌、5=郵撥
- **影響**：[glossary.md](glossary.md)、NumberTitle 推導邏輯、預繳 6 case
- **問題**：未來會有新類型嗎？（例：6=線上、7=企業？）
- **確認時機**：可不急；列為未來迭代考量
- **狀態**：⬜

### B7. 「同寄件地址」勾選邏輯保留還是強化？

- **目前**：勾選複製 Mail → Text；mail 空時阻擋
- **可能強化**：兩地址綁定（mail 改 text 自動跟著改）
- **影響**：[believer-management.md](blueprints/believer-management.md)、[signup-management.md](blueprints/signup-management.md)
- **確認時機**：UX review 時
- **狀態**：⬜（沿用舊行為）

### B8. 變更歷程是否需要區分新增 / 編輯 / 刪除？

- **目前**：SignupLogs 現況無 action 欄位（用「同 SignupID 第一筆 = 新增」推斷）；DB 已解除凍結，如需可走 migration 加 action 欄位
- **影響**：[signup-management.md](blueprints/signup-management.md)、變更歷程 UI 顯示
- **問題**：使用者需要明確標示嗎？或推斷夠用？
- **確認時機**：UX review 時
- **狀態**：⬜

### B9. 列印是否需要紀錄誰列印的、列印幾份？

- **目前**：列印無紀錄
- **影響**：是否要在 file log 記錄列印行為 + 報表 ID
- **問題**：業務需要追蹤誰列印過嗎？（防止重複列印、稽核用）
- **確認時機**：上線前合規檢查
- **狀態**：⬜

### B10. 預繳金額是否要記錄？

- **目前**：`Signups.Fee` 是該次報名費用；無獨立預繳金額欄位
- **影響**：[prepay-loading.md](blueprints/prepay-loading.md)
- **問題**：預繳是否要顯示金額？預繳金額和當期費用是否分開？
- **確認時機**：實作前
- **狀態**：⬜

### B11. 標楷體字型供應

- **目前**：BiauKai / 標楷體；跨平台需 bundle
- **影響**：[visual-design.md](design/visual-design.md)、[printing-reports.md](blueprints/printing-reports.md)
- **需要**：
  - 寺方是否有授權的 DFKai-SB 字型檔？
  - 或同意使用開源替代（TW-Kai 等）？
- **確認時機**：實作前
- **狀態**：⬜

### B12. 個資使用告知書

- **目前狀況**：舊系統無
- **影響**：[security.md](design/security.md) §個資法合規
- **需要**：業務評估是否要在報名表加「個資使用告知 + 同意 checkbox」
- **確認時機**：上線前合規檢查
- **狀態**：⬜

### B13. 堂號是「信眾層級」還是「報名層級」？

- **目前狀況**：堂號實體只存於 `Believers.HallName`（信眾共用），報名/清單靠 `SignupView` JOIN 帶出；編輯一筆報名改堂號會回寫共用 Believer，**連動同信眾所有報名**（沿用 legacy `EditSignupForm` 行為）。使用者回報此連動為非預期。
- **影響**：[blueprints/signup-hallname-isolation.md](blueprints/signup-hallname-isolation.md)、[design/database-design.md](design/database-design.md)、[design/backend-design.md](design/backend-design.md)、[glossary.md](glossary.md)
- **需要**：業務確認——同一信眾在不同年度/法會可否掛**不同**堂號？
  - 「會不同」→ 報名層級：`Signups` 需加自有 HallName 欄（方案 A，走 migration + 歷史回填）
  - 「永遠相同」→ 信眾層級：堂號集中到信眾維護、報名頁停止回寫（方案 C，零 schema）
- **附帶確認**：目前「代入新增」改的堂號其實存不進新報名（只進 audit log），是否符合預期？→ 已隨方案 C 一併處理：堂號改唯讀，新增/編輯都不可改、僅信眾維護頁維護。
- **確認時機**：實作此修正前
- **狀態**：✅ 2026-06-29 定案「信眾層級」→ 採方案 C（報名編輯/新增不回寫 Believer、堂號唯讀）。已實作並回填 [signup-hallname-isolation.md](blueprints/signup-hallname-isolation.md) / [business-rules-implicit.md §3.1](business-rules-implicit.md) / [glossary.md](glossary.md)

## C. 環境部署需求

### C1. 部署位置與 IP

- **目前假設**：寺方內網 192.168.x.x
- **影響**：[infrastructure.md](design/infrastructure.md) 環境變數
- **需要**：實際 prod / staging IP、域名（若有）、TLS 憑證來源
- **狀態**：⬜

### C2. update server 位置

- **目前假設**：寺方 NAS 或內部一台 web server
- **影響**：electron-updater feed URL
- **需要**：實際 update server URL
- **狀態**：⬜

### C3. Code Signing 證書

- **目前狀況**：未購買
- **影響**：Windows / macOS 安裝體驗
- **需要**：是否購買 EV/OV 證書？哪家 CA？
- **狀態**：⬜

### C4. Sentry 是否採用

- **目前**：建議 self-hosted
- **影響**：error tracking 規模
- **狀態**：⬜

## D. 訓練與導入

### D1. 訓練排程

- **影響**：[user-training.md](workflows/user-training.md)
- **需要**：訓練日期、地點、可參加人員清單
- **狀態**：⬜

### D2. 並行運行期長度

- **目前計畫**：1 週並行 → 全切換 → 1 個月舊系統唯讀 → 2 個月下架
- **確認**：客戶接受時程嗎？
- **狀態**：⬜

### D3. 緊急聯絡清單

- **需要**：寺方資訊主管、業務窗口、開發團隊 hotline
- **狀態**：⬜

---

## 確認進度看板

```
A. DB 技術性（自己跑）      ⬜⬜⬜⬜⬜⬜⬜⬜ 0/8
B. 業務需求                ✅⬜⬜⬜⬜⬜⬜⬜⬜⬜⬜⬜⬜ 1/13 (B13)
C. 環境部署                ⬜⬜⬜⬜ 0/4
D. 訓練導入                ⬜⬜⬜ 0/3
                          ──────────────────
                          1/28
```

## 確認流程建議

1. **A 系列**先做（不必業務、可立即進行）
   - 連你本機 DB 跑 SQL
   - 回填數據至 database-design.md / performance.md
2. **B 系列**安排一次業務會議集中問
   - 把 B1-B12 整理成議程
   - 預估 1.5 小時可問完
3. **C 系列**併入部署規劃會議
4. **D 系列**併入訓練規劃會議

每次回答後在本檔對應條目標 ✅ + 日期 + 來源。
