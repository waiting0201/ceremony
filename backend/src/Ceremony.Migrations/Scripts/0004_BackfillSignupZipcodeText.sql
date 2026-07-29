-- 0004：一次性回填——補上「有 ZipcodeID 但郵遞區號文字為空」的報名列。
--
-- 背景：dbo.Signups 的郵遞區號有**兩欄**——MailZipcodeID（FK → Zipcodes）與 MailZipcode（nvarchar(10)
-- 文字快照），TextZipcodeID / TextZipcode 同理。`SignupView` 曝出去的 MailZipcode / TextZipcode
-- 讀的是**文字欄**（不是 join Zipcodes 得來），而新版後端一直只寫 FK、沒寫文字欄
-- → 新系統建立的報名，API / 報表（收據郵寄封面）/ 舊系統編輯畫面看到的郵遞區號一律空白。
-- 寫入端已於同一次修正補上（SignupRepository insert/update、PrepayRepository insert 由 FK 現查）。
--
-- 冪等：只動「FK 有值 且 文字欄為 NULL 或空字串」的列，可重跑。
-- **刻意不碰**「文字欄有值但與 FK 對應的 Zipcode 不同」的列：那批混雜了舊系統時期的歷史快照
-- （Zipcodes 表本身多年間有異動），文字欄的用途正是保存「當時印出去的號碼」，不應被現值覆蓋。
-- 這批列的清點與處置見 docs/pending-business-input.md。

UPDATE s
SET s.MailZipcode = z.Zipcode
FROM dbo.Signups AS s
INNER JOIN dbo.Zipcodes AS z ON z.ZipcodeID = s.MailZipcodeID
WHERE s.MailZipcodeID IS NOT NULL
  AND (s.MailZipcode IS NULL OR s.MailZipcode = N'');
GO

UPDATE s
SET s.TextZipcode = z.Zipcode
FROM dbo.Signups AS s
INNER JOIN dbo.Zipcodes AS z ON z.ZipcodeID = s.TextZipcodeID
WHERE s.TextZipcodeID IS NOT NULL
  AND (s.TextZipcode IS NULL OR s.TextZipcode = N'');
GO
