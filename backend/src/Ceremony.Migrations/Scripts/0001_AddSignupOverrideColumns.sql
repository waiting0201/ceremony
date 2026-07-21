-- 0001：Signups 新增三個 per-signup 覆寫欄位（堂號 / 員工類型 / 固定編號）。
-- 背景：這三欄原僅存於 dbo.Believers，經 SignupView JOIN 帶出；本次讓報名可持有自己的值
--       （比照 Signups 既有的 Name/Phone 快照）。見 docs/blueprints/signup-hallname-isolation.md（方案 A）。
--
-- 用 NULL 而非 NOT NULL（向後相容關鍵）：並行期舊 WinForms 系統 INSERT Signups 不供這三欄，
--   NULL 才不會失敗；且搭配 0003 SignupView 的 COALESCE(S.X, B.X)，未覆寫（NULL）列自動回退信眾值。
-- 冪等：COL_LENGTH 為 NULL 才 ADD；每欄獨立語句（同批次 ADD 後不可立即引用新欄）。

IF COL_LENGTH('dbo.Signups', 'HallName') IS NULL
    ALTER TABLE dbo.Signups ADD HallName nvarchar(10) NULL;
GO

IF COL_LENGTH('dbo.Signups', 'EmployeeType') IS NULL
    ALTER TABLE dbo.Signups ADD EmployeeType int NULL;
GO

IF COL_LENGTH('dbo.Signups', 'IsFixedNumber') IS NULL
    ALTER TABLE dbo.Signups ADD IsFixedNumber bit NULL;
GO
