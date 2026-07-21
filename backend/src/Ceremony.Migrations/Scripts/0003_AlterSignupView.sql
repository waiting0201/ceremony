-- 0003：SignupView 三欄改讀 per-signup 覆寫值，並新增數值 EmployeeType 欄。
-- 現有 view 已把 Signups 別名為 S、Believers 為 B，且 Name/Phone 早已是「S 為 NULL 則回退 B」的 per-signup
--   pattern；本次讓 堂號/員工類型/固定編號 比照。
--
-- 向後相容（並行期舊系統仍讀此 view）：
--   * 欄名/型別不變，只把來源由 B.X 改為 COALESCE(S.X, B.X)。
--   * 回填（0002）後既有列 S.X 已等於當下 B.X → 顯示值不變。
--   * 舊系統建立的列 S.X 為 NULL → COALESCE 回退 B.X。
--   * 新增的數值 EmployeeType 欄不影響舊系統（其查詢以明確欄名 SELECT，不含此欄）。
-- 註：view 完整定義取自本機 DB sys.sql_modules（2026-07-21），僅調整下列三處 + 新增 EmployeeType。

CREATE OR ALTER VIEW dbo.SignupView
AS
SELECT          S.SignupID, S.BelieverID, S.Year, CC.Title AS CeremonyTitle, CC.Sort AS CeremonySort, S.SignupType,
                            S.CeremonyCategoryID, S.NumberTitle, S.Number, S.Fee,
                            CASE COALESCE(S.EmployeeType, B.EmployeeType) WHEN 1 THEN N'非員工' WHEN 2 THEN N'大殿' WHEN 3 THEN N'地藏殿' END AS Employee,
                            COALESCE(S.EmployeeType, B.EmployeeType) AS EmployeeType,
                            CASE WHEN S.Name IS NULL THEN B.Name ELSE S.Name END AS Name, S.Remark, COALESCE(S.HallName, B.HallName) AS HallName, S.DeadNameOne,
                            S.DeadNameTwo, S.DeadNameThree, S.DeadNameFour, S.DeadNameFive, S.DeadNameSix, S.LivingNameOne,
                            S.LivingNameTwo, S.LivingNameThree, S.LivingNameFour, S.LivingNameFive, S.LivingNameSix, S.PrepayYear,
                            S.PrepayCeremonyCategoryID, PCC.Title AS PrepayCeremonyTitle, CASE WHEN S.Phone IS NULL
                            THEN B.Phone ELSE S.Phone END AS Phone, S.MailZipcode, MZ.City AS MailCity, MZ.Area AS MailZone,
                            S.MailAddress, S.TextZipcode, TZ.City AS TextCity, TZ.Area AS TextZone, S.TextAddress, A.Name AS AdminName,
                            S.Createdate, COALESCE(S.IsFixedNumber, B.IsFixedNumber) AS IsFixedNumber
FROM              dbo.Signups AS S LEFT OUTER JOIN
                            dbo.Believers AS B ON S.BelieverID = B.BelieverID LEFT OUTER JOIN
                            dbo.CeremonyCategorys AS CC ON S.CeremonyCategoryID = CC.CeremonyCategoryID LEFT OUTER JOIN
                            dbo.CeremonyCategorys AS PCC ON S.PrepayCeremonyCategoryID = PCC.CeremonyCategoryID LEFT OUTER JOIN
                            dbo.Zipcodes AS MZ ON S.MailZipcodeID = MZ.ZipcodeID LEFT OUTER JOIN
                            dbo.Zipcodes AS TZ ON S.TextZipcodeID = TZ.ZipcodeID LEFT OUTER JOIN
                            dbo.Admins AS A ON S.AdminID = A.AdminID;
GO
