---
title: GET /prepay?believerId={id}&year={y}
purpose: 取某信眾「今年(含)以前最新一筆報名」的預繳資訊，供新增報名選信眾時自動帶入預繳年/法會
status: shipped
endpoint: get-prepay-believer-latest
http_method: GET
route: /api/v1/prepay?believerId={id}&year={y}
legacy_form: NewSignupForm.cs
legacy_lines: 1102-1115
related_agents:
  - backend-engineer
  - qa-test-engineer
related_docs:
  - ../../design/api-design.md
  - ../legacy-coverage/new-signup-form.md
  - post-prepay-load.md
keywords: [prepay, 預繳, believer, 信眾, 自動帶入, BelieverSelected]
last_updated: 2026-06-02
---

## 規格

### Route + Method

- `GET` `/api/v1/prepay?believerId={guid}&year={int}` — `[Authorize]`
- 與既有 `POST /api/v1/prepay/load`（批次載入，LoadPrepayForm）**不同功能**：此為單一信眾的「最近預繳」反查，供新增報名 UX 自動帶入。

### Request

- query `believerId`（Guid，必填）
- query `year`（int，選填）：篩 `Year <= year` 的報名；未帶 → 預設當前民國年（`DateTime.Now.Year - 1911`）

### Response DTO

```jsonc
// 200 — 最新報名有預繳
{ "prepayYear": 121, "prepayCeremonyCategoryId": "….", "prepayCeremonyCategoryTitle": "春季" }

// 200 — 查無報名 / 最新報名無預繳（三欄皆 null；前端只在 prepayYear 非 null 時才預填）
{ "prepayYear": null, "prepayCeremonyCategoryId": null, "prepayCeremonyCategoryTitle": null }
```

## 舊系統對照

對齊 `NewSignupForm.BelieverSelected`（NewSignupForm.cs:1102-1115）：

```csharp
//取得此信眾今年以前最新的報名
int Y = Convert.ToInt32(txtYear.Text.Trim());
IQueryable<Signups> signups = signupsService.Get()
    .Where(a => a.BelieverID == BelieverID && a.Year <= Y)
    .OrderByDescending(o => o.Year).ThenByDescending(o => o.CeremonyCategorys.Sort);
if (signups.Any()) {
    Signups latestsignup = signups.FirstOrDefault();
    if (latestsignup.PrepayYear != null) {        // 只在最新一筆有預繳時才帶入
        LoadPrepayCeremony();
        txtPrepayYear.Text = latestsignup.PrepayYear.ToString();
        dlPrepayCeremony.SelectedValue = latestsignup.PrepayCeremonyCategoryID;
    }
}
```

## 資料存取

```sql
SELECT TOP 1 PrepayYear, PrepayCeremonyCategoryID, PrepayCeremonyTitle
FROM dbo.SignupView
WHERE BelieverID = @BelieverId AND Year <= @Year
ORDER BY Year DESC, CeremonySort DESC
```

`CeremonySort` 為 `SignupView` 已 join 的該報名自身法會 `CeremonyCategorys.Sort`，對齊舊 `o.CeremonyCategorys.Sort`。
`SignupRepository.GetLatestPrepayByBelieverAsync` 回 row → `GetBelieverLatestPrepayHandler` 包成 `BelieverLatestPrepayResult`（查無回三欄 null）。

## 業務規則 / 取捨

- **唯讀、無副作用**：純查詢，不寫任何資料。
- **只取最新一筆的預繳**：與舊一致 — 即使更早的報名有預繳，只看最新那筆；最新那筆無預繳則不帶入（回 null）。
- **前端行為**：`signup-edit-form.pickBeliever` 選信眾後呼叫；`prepayYear` 非 null 才 patch `prepayYear` + `prepayCeremonyCategoryId`，失敗（網路/錯誤）以 try/catch 吞掉、不阻斷選信眾。

## 驗收

- [x] `dotnet build` 0 warning；`GetBelieverLatestPrepayHandlerTests` 3 case 綠
- [x] 實機（dev real DB）：有預繳信眾 → 200 帶 `prepayYear`/title；不存在 believerId → 200 三欄 null
- [x] 前端選信眾自動帶入預繳年/法會（tsc 0 / ng build 0 warning）
