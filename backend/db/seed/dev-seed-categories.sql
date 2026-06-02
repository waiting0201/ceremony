/*
 * DEV-ONLY seed：法會類型子分類範例資料
 * ------------------------------------------------------------------
 * 用途：dev DB 的 dbo.CeremonyCategorys 預設只有 3 筆根分類（春季/中元/秋季）、
 *       無任何子項，導致「法會類型維護」頁看不到階層。此腳本在每個根分類下
 *       塞入幾筆範例子法會，方便本機驗證樹狀顯示 / 新增報名的法會分類連動。
 *
 * ⚠️ 僅供本機 dev 測試。正式 DB 完全凍結（見 docs/blueprints/data-migration.md），
 *    切勿在 prod 執行。
 *
 * 特性：
 *   - Idempotent：以 (Title, ParentID) 判存在，重複執行不會重覆塞。
 *   - 以根分類「名稱」對應 ParentID（不寫死 GUID），dev DB 重建後仍可用。
 *   - 僅寫入 Repository 實際使用的 4 欄（CeremonyCategoryID/Title/ParentID/Sort）。
 *
 * 執行（dev，(local) MSSQL）：
 *   sqlcmd -S (local) -U sa -P <pwd> -d Ceremony -i dev-seed-categories.sql
 *   或透過任何能連 dev DB 的工具貼上執行。
 *
 * 清除範例子項（如需還原成只剩 3 根分類）：
 *   DELETE c FROM dbo.CeremonyCategorys c
 *   JOIN dbo.CeremonyCategorys p ON p.CeremonyCategoryID = c.ParentID
 *   WHERE p.ParentID IS NULL
 *     AND p.Title IN (N'春季', N'中元', N'秋季')
 *     AND c.Title IN (N'梁皇寶懺', N'藥師法會', N'大悲懺',
 *                     N'盂蘭盆', N'三時繫念', N'瑜伽焰口',
 *                     N'地藏法會', N'水陸法會');
 *   （刪除前請確認這些子項未被任何 Signups 引用，否則先處理報名資料。）
 */
SET NOCOUNT ON;

DECLARE @children TABLE (RootTitle nvarchar(50), Title nvarchar(50), Sort int);
INSERT INTO @children (RootTitle, Title, Sort) VALUES
    (N'春季', N'梁皇寶懺', 1),
    (N'春季', N'藥師法會', 2),
    (N'春季', N'大悲懺',   3),
    (N'中元', N'盂蘭盆',   1),
    (N'中元', N'三時繫念', 2),
    (N'中元', N'瑜伽焰口', 3),
    (N'秋季', N'地藏法會', 1),
    (N'秋季', N'水陸法會', 2);

INSERT INTO dbo.CeremonyCategorys (CeremonyCategoryID, Title, ParentID, Sort)
SELECT NEWID(), c.Title, p.CeremonyCategoryID, c.Sort
FROM @children c
JOIN dbo.CeremonyCategorys p
    ON p.Title = c.RootTitle AND p.ParentID IS NULL
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.CeremonyCategorys x
    WHERE x.Title = c.Title AND x.ParentID = p.CeremonyCategoryID
);

PRINT N'dev-seed-categories: 完成（idempotent）。';
