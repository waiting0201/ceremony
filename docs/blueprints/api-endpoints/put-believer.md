---
title: PUT /api/v1/believers/:id
purpose: 編輯既有信眾（全欄位覆寫）
status: shipped
endpoint: put-believer
http_method: PUT
route: /api/v1/believers/:id
legacy_form: BelieverForm.cs
legacy_lines: 154-185
related_agents:
  - backend-engineer
related_docs:
  - post-believers.md
  - ../legacy-coverage/believer-form.md
keywords: [believers, update, put]
last_updated: 2026-07-31 (新增「清空既有值」段：地址三段與堂號的清空送法與結果；前端移除 mailAddress required)
---

## 規格

`PUT /api/v1/believers/{id:guid}`，需要 JWT。Body 同 [post-believers.md](post-believers.md) `BelieverUpsertRequest`。

### Response

`200 OK` + `BelieverListItem`。

### 錯誤碼

| HTTP | errorCode | message | 觸發 |
|---|---|---|---|
| 400 / 同 POST | 同 POST | 同 POST | 同 POST |
| 404 | `BELIEVER_NOT_FOUND` | `找不到信眾` | id 不存在 |

## 舊系統對照

| 舊方法/事件 | 行 | 對應新版 |
|---|---|---|
| `btnConfirm_Click` edit path | 154-184 | `UpdateBelieverHandler.HandleAsync` |
| `GetByID + 逐欄位賦值 + SaveChanges` | 157-181 | Dapper UPDATE 全欄位 |

新版用「全欄位覆寫」（PUT 慣例），不做欄位 diff。

### 清空既有值（2026-07-31 客訴）

全欄位覆寫的直接後果：**送什麼就是什麼**，既有值送空即被清掉。使用者要求的「可不留地址」靠這個成立：

| 清空對象 | 送法 | 結果 |
|---|---|---|
| 寄件地址 | `mailAddress=""`、`mailZipcodeId=null` | 地址空字串；城市/區域由 `MailZipcodeID` join 而來，區號清掉整段消失 |
| 文牒地址 | `textAddress=""`、`textZipcodeId=null` | 同上（信眾這側本來就沒有「抄寄件段」的 fallback） |
| 堂號 | `hallName=""` 或 `null` | 存 `null`（信眾主檔無 COALESCE 回退，兩者等價） |

前端側 2026-07-31 一併移除信眾表單 `mailAddress` 的 `Validators.required`——後端自 2026-07-21
就允許空地址，那個 required 是唯一擋住「刪掉既有地址」的關卡。
回歸鎖：`BelieversEndpointsTests.PUT_can_clear_existing_addresses_and_hallName`。

> 注意：清掉信眾主檔的堂號**不會**連動已存在的報名——報名自 2026-07-21 起持有 per-signup 堂號快照
> （見 [signup-hallname-isolation.md](../signup-hallname-isolation.md)），要改那筆報名請走報名維護。

## 驗收

- [x] 對應 [believer-form.md](../legacy-coverage/believer-form.md) row 5 (edit path) ✅
- [x] 含 integration test (404 / 200)
