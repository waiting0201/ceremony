using System.Text;
using Ceremony.Application.Prepay;
using Ceremony.Application.Signups;
using Ceremony.Infrastructure.Persistence;
using Dapper;

namespace Ceremony.Infrastructure.Repositories;

/// <summary>
/// Dapper-based 報名搜尋（讀自既有 <c>dbo.SignupView</c>）。
/// </summary>
/// <remarks>
/// Legacy: SignupForm.cs:807-864 (LoadSearchSignups PredicateBuilder)
/// </remarks>
public sealed class SignupRepository(IDbConnectionFactory factory) : ISignupRepository
{
    public async Task<IReadOnlyList<SignupListItem>> SearchAsync(SignupSearchQuery query, CancellationToken ct = default)
    {
        var sql = new StringBuilder("""
            SELECT
              SignupID, Year, CeremonyCategoryID, CeremonyTitle, SignupType, NumberTitle, Number, Fee,
              Employee, BelieverID, Name, HallName, Phone, IsFixedNumber,
              LivingNameOne, LivingNameTwo, LivingNameThree, LivingNameFour, LivingNameFive, LivingNameSix,
              DeadNameOne,   DeadNameTwo,   DeadNameThree,   DeadNameFour,   DeadNameFive,   DeadNameSix,
              MailCity, MailZone, MailZipcode, MailAddress,
              TextCity, TextZone, TextZipcode, TextAddress,
              PrepayYear, PrepayCeremonyCategoryID, PrepayCeremonyTitle,
              Remark, AdminName, Createdate
            FROM dbo.SignupView
            WHERE 1=1
            """);

        var p = new DynamicParameters();

        // AND group — 對齊舊 line 815-820
        if (query.Year is { } y)
        {
            sql.AppendLine(query.IsScope ? " AND Year >= @Year" : " AND Year = @Year");
            p.Add("@Year", y);
        }
        if (query.CeremonyCategoryId is { } cid)
        {
            sql.AppendLine(" AND CeremonyCategoryID = @CeremonyCategoryId");
            p.Add("@CeremonyCategoryId", cid);
        }
        // 對齊舊 SignupForm.cs:828：SignupType == -1 視為「全部」不過濾
        if (query.SignupType is { } st && st != -1)
        {
            sql.AppendLine(" AND SignupType = @SignupType");
            p.Add("@SignupType", st);
        }
        // 對齊舊 SignupForm.cs:829：Number == 0 或空白視為「全部」不過濾
        if (query.Number is { } n && n != 0)
        {
            sql.AppendLine(" AND Number = @Number");
            p.Add("@Number", n);
        }

        // OR group — 對齊舊 line 822-830
        var orClauses = new List<string>();
        var key = query.SearchKey;
        if (!string.IsNullOrEmpty(key))
        {
            if (query.ScopeName)
                orClauses.Add("Name LIKE @SearchKey");
            if (query.ScopeLivingName)
                orClauses.Add("(LivingNameOne LIKE @SearchKey OR LivingNameTwo LIKE @SearchKey OR LivingNameThree LIKE @SearchKey OR LivingNameFour LIKE @SearchKey OR LivingNameFive LIKE @SearchKey OR LivingNameSix LIKE @SearchKey)");
            if (query.ScopeDeadName)
                orClauses.Add("(DeadNameOne LIKE @SearchKey OR DeadNameTwo LIKE @SearchKey OR DeadNameThree LIKE @SearchKey OR DeadNameFour LIKE @SearchKey OR DeadNameFive LIKE @SearchKey OR DeadNameSix LIKE @SearchKey)");
            if (query.ScopePhone)
                orClauses.Add("Phone LIKE @SearchKey");
            if (query.ScopeRemark)
                orClauses.Add("Remark LIKE @SearchKey");
            if (orClauses.Count > 0)
                p.Add("@SearchKey", Like(key));
        }
        if (query.IsFixedNumber)
            orClauses.Add("IsFixedNumber = 1");

        if (orClauses.Count > 0)
            sql.AppendLine($" AND ({string.Join(" OR ", orClauses)})");

        // 對齊舊 line 837 排序：Year, CeremonySort, NumberTitle, Number
        // SignupView 內 CeremonySort 欄位來自 join 的 CeremonyCategorys.Sort
        sql.AppendLine(" ORDER BY Year, CeremonySort, NumberTitle, Number");

        await using var conn = await factory.CreateOpenAsync(ct);
        var rows = await conn.QueryAsync(new CommandDefinition(sql.ToString(), p, cancellationToken: ct));

        var list = new List<SignupListItem>();
        foreach (var r in rows)
        {
            var d = (IDictionary<string, object?>)r;
            list.Add(new SignupListItem(
                Id: (Guid)d["SignupID"]!,
                Year: (int)d["Year"]!,
                CeremonyCategoryId: (Guid)d["CeremonyCategoryID"]!,
                CeremonyTitle: d["CeremonyTitle"] as string,
                SignupType: (int)d["SignupType"]!,
                NumberTitle: d["NumberTitle"] as string,
                Number: d["Number"] as int?,
                Fee: d["Fee"] as int?,
                Employee: d["Employee"] as string,
                BelieverId: d["BelieverID"] as Guid?,
                Name: d["Name"] as string,
                HallName: d["HallName"] as string,
                Phone: d["Phone"] as string,
                IsFixedNumber: d["IsFixedNumber"] is bool b && b,
                LivingNames:
                [
                    d["LivingNameOne"] as string, d["LivingNameTwo"] as string, d["LivingNameThree"] as string,
                    d["LivingNameFour"] as string, d["LivingNameFive"] as string, d["LivingNameSix"] as string,
                ],
                DeadNames:
                [
                    d["DeadNameOne"] as string, d["DeadNameTwo"] as string, d["DeadNameThree"] as string,
                    d["DeadNameFour"] as string, d["DeadNameFive"] as string, d["DeadNameSix"] as string,
                ],
                MailCity: d["MailCity"] as string,
                MailZone: d["MailZone"] as string,
                MailZipcode: d["MailZipcode"] as string,
                MailAddress: d["MailAddress"] as string,
                TextCity: d["TextCity"] as string,
                TextZone: d["TextZone"] as string,
                TextZipcode: d["TextZipcode"] as string,
                TextAddress: d["TextAddress"] as string,
                PrepayYear: d["PrepayYear"] as int?,
                PrepayCeremonyCategoryId: d["PrepayCeremonyCategoryID"] as Guid?,
                PrepayCeremonyTitle: d["PrepayCeremonyTitle"] as string,
                Remark: d["Remark"] as string,
                AdminName: d["AdminName"] as string,
                CreateDate: d["Createdate"] as DateTime?));
        }
        return list;
    }

    private static string Like(string value)
    {
        var escaped = value
            .Replace("[", "[[]")
            .Replace("%", "[%]")
            .Replace("_", "[_]");
        return $"%{escaped}%";
    }

    public async Task<SignupListItem?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = """
            SELECT TOP 1
              SignupID, Year, CeremonyCategoryID, CeremonyTitle, SignupType, NumberTitle, Number, Fee,
              Employee, BelieverID, Name, HallName, Phone, IsFixedNumber,
              LivingNameOne, LivingNameTwo, LivingNameThree, LivingNameFour, LivingNameFive, LivingNameSix,
              DeadNameOne,   DeadNameTwo,   DeadNameThree,   DeadNameFour,   DeadNameFive,   DeadNameSix,
              MailCity, MailZone, MailZipcode, MailAddress,
              TextCity, TextZone, TextZipcode, TextAddress,
              PrepayYear, PrepayCeremonyCategoryID, PrepayCeremonyTitle,
              Remark, AdminName, Createdate
            FROM dbo.SignupView
            WHERE SignupID = @Id
            """;

        await using var conn = await factory.CreateOpenAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
        return row is null ? null : MapRow(row);
    }

    public async Task<bool> NumberExistsAsync(int year, Guid ceremonyCategoryId, int signupType, int number, CancellationToken ct = default)
    {
        const string sql = """
            SELECT COUNT(1) FROM dbo.Signups
            WHERE Year = @Year AND CeremonyCategoryID = @Cat AND SignupType = @Type AND Number = @Number
            """;

        await using var conn = await factory.CreateOpenAsync(ct);
        var n = await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { Year = year, Cat = ceremonyCategoryId, Type = signupType, Number = number }, cancellationToken: ct));
        return n > 0;
    }

    public async Task<bool> NumberExistsExcludingAsync(int year, Guid ceremonyCategoryId, int signupType, int number, Guid excludeSignupId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT COUNT(1) FROM dbo.Signups
            WHERE Year = @Year AND CeremonyCategoryID = @Cat AND SignupType = @Type AND Number = @Number
              AND SignupID <> @ExcludeId
            """;
        await using var conn = await factory.CreateOpenAsync(ct);
        var n = await conn.ExecuteScalarAsync<int>(new CommandDefinition(sql,
            new { Year = year, Cat = ceremonyCategoryId, Type = signupType, Number = number, ExcludeId = excludeSignupId },
            cancellationToken: ct));
        return n > 0;
    }

    public async Task<IReadOnlyList<SignupDuplicateItem>> FindDuplicatesByBelieverAsync(
        int year, Guid ceremonyCategoryId, Guid believerId, Guid? excludeSignupId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT SignupID, SignupType, NumberTitle, Number, Name
            FROM dbo.Signups
            WHERE Year = @Year AND CeremonyCategoryID = @Cat AND BelieverID = @Believer
              AND (@Exclude IS NULL OR SignupID <> @Exclude)
            ORDER BY SignupType, Number
            """;

        await using var conn = await factory.CreateOpenAsync(ct);
        var rows = await conn.QueryAsync(new CommandDefinition(sql,
            new { Year = year, Cat = ceremonyCategoryId, Believer = believerId, Exclude = excludeSignupId },
            cancellationToken: ct));

        var list = new List<SignupDuplicateItem>();
        foreach (var r in rows)
        {
            var d = (IDictionary<string, object?>)r;
            list.Add(new SignupDuplicateItem(
                SignupId: (Guid)d["SignupID"]!,
                SignupType: (int)d["SignupType"]!,
                NumberTitle: d["NumberTitle"] as string,
                Number: d["Number"] as int?,
                Name: d["Name"] as string));
        }
        return list;
    }

    public async Task<bool> UpdateWithLogAsync(
        SignupWriteModel s,
        SignupLogWriteModel l,
        int number,
        CancellationToken ct = default)
    {
        await using var conn = await factory.CreateOpenAsync(ct);
        using var tx = await ((Microsoft.Data.SqlClient.SqlConnection)conn).BeginTransactionAsync(ct);
        try
        {
            // 注意：刻意不更新 Believer。堂號/員工類型/固定編號為信眾層級屬性，僅於信眾維護頁修改；
            // 報名編輯回寫 Believer 會連動同信眾全部報名（legacy EditSignupForm 缺陷），此處不重演。
            // 見 docs/blueprints/signup-hallname-isolation.md

            // 1. Update Signup（全欄位覆寫）
            const string updS = """
                UPDATE dbo.Signups SET
                  Year=@Year, CeremonyCategoryID=@CeremonyCategoryId, SignupType=@SignupType, BelieverID=@BelieverId,
                  NumberTitle=@NumberTitle, Number=@Number, Fee=@Fee, Name=@Name, Phone=@Phone,
                  LivingNameOne=@L1, LivingNameTwo=@L2, LivingNameThree=@L3, LivingNameFour=@L4, LivingNameFive=@L5, LivingNameSix=@L6,
                  DeadNameOne=@D1, DeadNameTwo=@D2, DeadNameThree=@D3, DeadNameFour=@D4, DeadNameFive=@D5, DeadNameSix=@D6,
                  MailZipcodeID=@MailZipcodeId, MailAddress=@MailAddress,
                  TextZipcodeID=@TextZipcodeId, TextAddress=@TextAddress,
                  Remark=@Remark, PrepayYear=@PrepayYear, PrepayCeremonyCategoryID=@PrepayCeremonyCategoryId,
                  AdminID=@AdminId, Createdate=@CreateDate
                WHERE SignupID = @SignupId
                """;
            var rows = await conn.ExecuteAsync(new CommandDefinition(updS, new
            {
                s.SignupId, s.Year, s.CeremonyCategoryId, s.SignupType, s.BelieverId,
                s.NumberTitle, Number = number, s.Fee, s.Name, s.Phone,
                L1 = s.LivingNames[0], L2 = s.LivingNames[1], L3 = s.LivingNames[2],
                L4 = s.LivingNames[3], L5 = s.LivingNames[4], L6 = s.LivingNames[5],
                D1 = s.DeadNames[0], D2 = s.DeadNames[1], D3 = s.DeadNames[2],
                D4 = s.DeadNames[3], D5 = s.DeadNames[4], D6 = s.DeadNames[5],
                s.MailZipcodeId, s.MailAddress, s.TextZipcodeId, s.TextAddress,
                s.Remark, s.PrepayYear, s.PrepayCeremonyCategoryId, s.AdminId, s.CreateDate,
            }, transaction: tx, cancellationToken: ct));

            if (rows == 0)
            {
                await tx.RollbackAsync(ct);
                return false;
            }

            // 2. Insert SignupLog（audit 紀錄編輯；HallName 仍記當下快照值）
            const string insLog = """
                INSERT INTO dbo.SignupLogs (
                  SignupLogID, SignupID, Year, CeremonyCategoryTitle, SignupType,
                  HallName, Name, Phone, NumberTitle, Number, Fee,
                  LivingNameOne, LivingNameTwo, LivingNameThree, LivingNameFour, LivingNameFive, LivingNameSix,
                  DeadNameOne, DeadNameTwo, DeadNameThree, DeadNameFour, DeadNameFive, DeadNameSix,
                  MailCity, MailZone, MailAddress, TextCity, TextZone, TextAddress,
                  Remark, PrepayYear, PrepayCeremonyCategoryTitle, Admin, Createdate
                ) VALUES (
                  @SignupLogId, @SignupId, @Year, @CeremonyCategoryTitle, @SignupType,
                  @HallName, @Name, @Phone, @NumberTitle, @Number, @Fee,
                  @L1, @L2, @L3, @L4, @L5, @L6,
                  @D1, @D2, @D3, @D4, @D5, @D6,
                  @MailCity, @MailZone, @MailAddress, @TextCity, @TextZone, @TextAddress,
                  @Remark, @PrepayYear, @PrepayCeremonyCategoryTitle, @Admin, @CreateDate
                )
                """;
            await conn.ExecuteAsync(new CommandDefinition(insLog, new
            {
                l.SignupLogId, l.SignupId, l.Year, l.CeremonyCategoryTitle, l.SignupType,
                l.HallName, l.Name, l.Phone, l.NumberTitle, Number = number, l.Fee,
                L1 = l.LivingNames[0], L2 = l.LivingNames[1], L3 = l.LivingNames[2],
                L4 = l.LivingNames[3], L5 = l.LivingNames[4], L6 = l.LivingNames[5],
                D1 = l.DeadNames[0], D2 = l.DeadNames[1], D3 = l.DeadNames[2],
                D4 = l.DeadNames[3], D5 = l.DeadNames[4], D6 = l.DeadNames[5],
                l.MailCity, l.MailZone, l.MailAddress, l.TextCity, l.TextZone, l.TextAddress,
                l.Remark, l.PrepayYear, l.PrepayCeremonyCategoryTitle, l.Admin, l.CreateDate,
            }, transaction: tx, cancellationToken: ct));

            await tx.CommitAsync(ct);
            return true;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM dbo.Signups WHERE SignupID = @Id";
        await using var conn = await factory.CreateOpenAsync(ct);
        var rows = await conn.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
        return rows > 0;
    }

    public async Task<string?> GetCeremonyCategoryTitleAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = "SELECT TOP 1 Title FROM dbo.CeremonyCategorys WHERE CeremonyCategoryID = @Id";
        await using var conn = await factory.CreateOpenAsync(ct);
        return await conn.ExecuteScalarAsync<string?>(new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
    }

    public async Task<BelieverLatestPrepayResult?> GetLatestPrepayByBelieverAsync(Guid believerId, int yearLte, CancellationToken ct = default)
    {
        // 對齊舊 NewSignupForm.BelieverSelected:1102-1108：取該信眾今年(含)以前最新一筆報名。
        // SignupView 已 join CeremonyCategorys → 直接用 CeremonySort（即該報名自身法會的 Sort）。
        const string sql = """
            SELECT TOP 1 PrepayYear, PrepayCeremonyCategoryID, PrepayCeremonyTitle
            FROM dbo.SignupView
            WHERE BelieverID = @BelieverId AND Year <= @Year
            ORDER BY Year DESC, CeremonySort DESC
            """;
        await using var conn = await factory.CreateOpenAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync(new CommandDefinition(sql, new { BelieverId = believerId, Year = yearLte }, cancellationToken: ct));
        if (row is null) return null;
        var d = (IDictionary<string, object?>)row;
        return new BelieverLatestPrepayResult(
            PrepayYear: d["PrepayYear"] as int?,
            PrepayCeremonyCategoryId: d["PrepayCeremonyCategoryID"] as Guid?,
            PrepayCeremonyCategoryTitle: d["PrepayCeremonyTitle"] as string);
    }

    public async Task InsertWithLogAsync(SignupWriteModel s, SignupLogWriteModel l, int? explicitNumber, CancellationToken ct = default)
    {
        await using var conn = await factory.CreateOpenAsync(ct);
        using var tx = await ((Microsoft.Data.SqlClient.SqlConnection)conn).BeginTransactionAsync(ct);

        try
        {
            int number;
            if (explicitNumber is { } provided)
            {
                number = provided;
            }
            else
            {
                // UPDLOCK + HOLDLOCK 序列化並發 insert，保證下一個編號唯一
                const string nextSql = """
                    SELECT ISNULL(MAX(Number), 0) + 1
                    FROM dbo.Signups WITH (UPDLOCK, HOLDLOCK)
                    WHERE Year = @Year AND CeremonyCategoryID = @Cat AND SignupType = @Type
                    """;
                number = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
                    nextSql,
                    new { Year = s.Year, Cat = s.CeremonyCategoryId, Type = s.SignupType },
                    transaction: tx,
                    cancellationToken: ct));
            }

            const string insertSignup = """
                INSERT INTO dbo.Signups (
                  SignupID, Year, CeremonyCategoryID, SignupType, BelieverID,
                  NumberTitle, Number, Fee, Name, Phone,
                  LivingNameOne, LivingNameTwo, LivingNameThree, LivingNameFour, LivingNameFive, LivingNameSix,
                  DeadNameOne, DeadNameTwo, DeadNameThree, DeadNameFour, DeadNameFive, DeadNameSix,
                  MailZipcodeID, MailAddress, TextZipcodeID, TextAddress,
                  Remark, PrepayYear, PrepayCeremonyCategoryID, AdminID, Createdate
                ) VALUES (
                  @SignupId, @Year, @CeremonyCategoryId, @SignupType, @BelieverId,
                  @NumberTitle, @Number, @Fee, @Name, @Phone,
                  @L1, @L2, @L3, @L4, @L5, @L6,
                  @D1, @D2, @D3, @D4, @D5, @D6,
                  @MailZipcodeId, @MailAddress, @TextZipcodeId, @TextAddress,
                  @Remark, @PrepayYear, @PrepayCeremonyCategoryId, @AdminId, @CreateDate
                )
                """;
            await conn.ExecuteAsync(new CommandDefinition(insertSignup, new
            {
                s.SignupId, s.Year, s.CeremonyCategoryId, s.SignupType, s.BelieverId,
                s.NumberTitle, Number = number, s.Fee, s.Name, s.Phone,
                L1 = s.LivingNames[0], L2 = s.LivingNames[1], L3 = s.LivingNames[2],
                L4 = s.LivingNames[3], L5 = s.LivingNames[4], L6 = s.LivingNames[5],
                D1 = s.DeadNames[0], D2 = s.DeadNames[1], D3 = s.DeadNames[2],
                D4 = s.DeadNames[3], D5 = s.DeadNames[4], D6 = s.DeadNames[5],
                s.MailZipcodeId, s.MailAddress, s.TextZipcodeId, s.TextAddress,
                s.Remark, s.PrepayYear, s.PrepayCeremonyCategoryId, s.AdminId, s.CreateDate,
            }, transaction: tx, cancellationToken: ct));

            const string insertLog = """
                INSERT INTO dbo.SignupLogs (
                  SignupLogID, SignupID, Year, CeremonyCategoryTitle, SignupType,
                  HallName, Name, Phone, NumberTitle, Number, Fee,
                  LivingNameOne, LivingNameTwo, LivingNameThree, LivingNameFour, LivingNameFive, LivingNameSix,
                  DeadNameOne, DeadNameTwo, DeadNameThree, DeadNameFour, DeadNameFive, DeadNameSix,
                  MailCity, MailZone, MailAddress, TextCity, TextZone, TextAddress,
                  Remark, PrepayYear, PrepayCeremonyCategoryTitle, Admin, Createdate
                ) VALUES (
                  @SignupLogId, @SignupId, @Year, @CeremonyCategoryTitle, @SignupType,
                  @HallName, @Name, @Phone, @NumberTitle, @Number, @Fee,
                  @L1, @L2, @L3, @L4, @L5, @L6,
                  @D1, @D2, @D3, @D4, @D5, @D6,
                  @MailCity, @MailZone, @MailAddress, @TextCity, @TextZone, @TextAddress,
                  @Remark, @PrepayYear, @PrepayCeremonyCategoryTitle, @Admin, @CreateDate
                )
                """;
            await conn.ExecuteAsync(new CommandDefinition(insertLog, new
            {
                l.SignupLogId, l.SignupId, l.Year, l.CeremonyCategoryTitle, l.SignupType,
                l.HallName, l.Name, l.Phone, l.NumberTitle, Number = number, l.Fee,
                L1 = l.LivingNames[0], L2 = l.LivingNames[1], L3 = l.LivingNames[2],
                L4 = l.LivingNames[3], L5 = l.LivingNames[4], L6 = l.LivingNames[5],
                D1 = l.DeadNames[0], D2 = l.DeadNames[1], D3 = l.DeadNames[2],
                D4 = l.DeadNames[3], D5 = l.DeadNames[4], D6 = l.DeadNames[5],
                l.MailCity, l.MailZone, l.MailAddress, l.TextCity, l.TextZone, l.TextAddress,
                l.Remark, l.PrepayYear, l.PrepayCeremonyCategoryTitle, l.Admin, l.CreateDate,
            }, transaction: tx, cancellationToken: ct));

            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<IReadOnlyList<SignupListItem>> SearchByNumberRangeAsync(SignupRangeQuery query, CancellationToken ct = default)
    {
        var sql = new StringBuilder("""
            SELECT
              SignupID, Year, CeremonyCategoryID, CeremonyTitle, SignupType, NumberTitle, Number, Fee,
              Employee, BelieverID, Name, HallName, Phone, IsFixedNumber,
              LivingNameOne, LivingNameTwo, LivingNameThree, LivingNameFour, LivingNameFive, LivingNameSix,
              DeadNameOne,   DeadNameTwo,   DeadNameThree,   DeadNameFour,   DeadNameFive,   DeadNameSix,
              MailCity, MailZone, MailZipcode, MailAddress,
              TextCity, TextZone, TextZipcode, TextAddress,
              PrepayYear, PrepayCeremonyCategoryID, PrepayCeremonyTitle,
              Remark, AdminName, Createdate
            FROM dbo.SignupView
            WHERE Number >= @Start AND Number <= @End
            """);

        var p = new DynamicParameters();
        p.Add("@Start", query.NumberStart);
        p.Add("@End", query.NumberEnd);

        if (query.Year is { } y)
        {
            sql.AppendLine(query.YearGte ? " AND Year >= @Year" : " AND Year = @Year");
            p.Add("@Year", y);
        }
        if (query.CeremonyCategoryId is { } cid)
        {
            sql.AppendLine(" AND CeremonyCategoryID = @CeremonyCategoryId");
            p.Add("@CeremonyCategoryId", cid);
        }
        if (query.SignupType is { } st && st != -1)
        {
            sql.AppendLine(" AND SignupType = @SignupType");
            p.Add("@SignupType", st);
        }

        sql.AppendLine(" ORDER BY Number");

        await using var conn = await factory.CreateOpenAsync(ct);
        var rows = await conn.QueryAsync(new CommandDefinition(sql.ToString(), p, cancellationToken: ct));
        var list = new List<SignupListItem>();
        foreach (var r in rows)
            list.Add(MapRow(r));
        return list;
    }

    public async Task<IReadOnlyList<SignupListItem>> SearchByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0) return Array.Empty<SignupListItem>();

        const string sql = """
            SELECT
              SignupID, Year, CeremonyCategoryID, CeremonyTitle, SignupType, NumberTitle, Number, Fee,
              Employee, BelieverID, Name, HallName, Phone, IsFixedNumber,
              LivingNameOne, LivingNameTwo, LivingNameThree, LivingNameFour, LivingNameFive, LivingNameSix,
              DeadNameOne,   DeadNameTwo,   DeadNameThree,   DeadNameFour,   DeadNameFive,   DeadNameSix,
              MailCity, MailZone, MailZipcode, MailAddress,
              TextCity, TextZone, TextZipcode, TextAddress,
              PrepayYear, PrepayCeremonyCategoryID, PrepayCeremonyTitle,
              Remark, AdminName, Createdate
            FROM dbo.SignupView
            WHERE SignupID IN @Ids
            ORDER BY Number
            """;

        await using var conn = await factory.CreateOpenAsync(ct);
        var rows = await conn.QueryAsync(new CommandDefinition(sql, new { Ids = ids }, cancellationToken: ct));
        var list = new List<SignupListItem>();
        foreach (var r in rows)
            list.Add(MapRow(r));
        return list;
    }

    private static SignupListItem MapRow(dynamic r)
    {
        var d = (IDictionary<string, object?>)r;
        return new SignupListItem(
            Id: (Guid)d["SignupID"]!,
            Year: (int)d["Year"]!,
            CeremonyCategoryId: (Guid)d["CeremonyCategoryID"]!,
            CeremonyTitle: d["CeremonyTitle"] as string,
            SignupType: (int)d["SignupType"]!,
            NumberTitle: d["NumberTitle"] as string,
            Number: d["Number"] as int?,
            Fee: d["Fee"] as int?,
            Employee: d["Employee"] as string,
            BelieverId: d["BelieverID"] as Guid?,
            Name: d["Name"] as string,
            HallName: d["HallName"] as string,
            Phone: d["Phone"] as string,
            IsFixedNumber: d["IsFixedNumber"] is bool b && b,
            LivingNames:
            [
                d["LivingNameOne"] as string, d["LivingNameTwo"] as string, d["LivingNameThree"] as string,
                d["LivingNameFour"] as string, d["LivingNameFive"] as string, d["LivingNameSix"] as string,
            ],
            DeadNames:
            [
                d["DeadNameOne"] as string, d["DeadNameTwo"] as string, d["DeadNameThree"] as string,
                d["DeadNameFour"] as string, d["DeadNameFive"] as string, d["DeadNameSix"] as string,
            ],
            MailCity: d["MailCity"] as string,
            MailZone: d["MailZone"] as string,
            MailZipcode: d["MailZipcode"] as string,
            MailAddress: d["MailAddress"] as string,
            TextCity: d["TextCity"] as string,
            TextZone: d["TextZone"] as string,
            TextZipcode: d["TextZipcode"] as string,
            TextAddress: d["TextAddress"] as string,
            PrepayYear: d["PrepayYear"] as int?,
            PrepayCeremonyCategoryId: d["PrepayCeremonyCategoryID"] as Guid?,
            PrepayCeremonyTitle: d["PrepayCeremonyTitle"] as string,
            Remark: d["Remark"] as string,
            AdminName: d["AdminName"] as string,
            CreateDate: d["Createdate"] as DateTime?);
    }
}
