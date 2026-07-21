-- 0002：一次性回填——把每筆既有報名的三欄「凍結」成當下對應信眾的值（使用者指定 2026-07-21）。
-- 回填後既有列成為明確 per-signup 快照，之後信眾維護頁改動不會連動歷史報名（與 Name/Phone 快照語意一致）。
--
-- 冪等：僅回填三欄「皆為 NULL」的列（尚未覆寫者）；可重跑。
-- 孤兒 Signup（BelieverID 無對應信眾，或 BelieverID 為 NULL）→ JOIN 不命中 → 維持 NULL → 由 SignupView COALESCE 回退。
-- 大表請於離峰執行。

UPDATE s
SET
    s.HallName      = b.HallName,
    s.EmployeeType  = b.EmployeeType,
    s.IsFixedNumber = b.IsFixedNumber
FROM dbo.Signups AS s
INNER JOIN dbo.Believers AS b ON b.BelieverID = s.BelieverID
WHERE s.HallName IS NULL
  AND s.EmployeeType IS NULL
  AND s.IsFixedNumber IS NULL;
GO
