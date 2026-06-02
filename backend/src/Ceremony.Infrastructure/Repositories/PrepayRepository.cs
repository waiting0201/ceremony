using Ceremony.Application.Prepay;
using Ceremony.Application.Signups;
using Ceremony.Infrastructure.Persistence;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Ceremony.Infrastructure.Repositories;

/// <summary>
/// Dapper-based 預繳載入 repository。
/// </summary>
/// <remarks>
/// Legacy: LoadPrepayForm.cs:45-824 (btnConfirm_Click)
/// </remarks>
public sealed class PrepayRepository(IDbConnectionFactory factory) : IPrepayRepository
{
    public async Task<(string Title, int Sort)?> GetCeremonyCategoryAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = "SELECT TOP 1 Title, Sort FROM dbo.CeremonyCategorys WHERE CeremonyCategoryID = @Id";
        await using var conn = await factory.CreateOpenAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
        if (row is null) return null;
        var d = (IDictionary<string, object?>)row;
        return (Title: (string)d["Title"]!, Sort: (int)d["Sort"]!);
    }

    public async Task<int> GetMaxNumberAsync(int targetYear, Guid targetCeremonyId, int signupType, CancellationToken ct = default)
    {
        const string sql = """
            SELECT ISNULL(MAX(Number), 0) FROM dbo.Signups
            WHERE Year = @Year AND CeremonyCategoryID = @Cat AND SignupType = @Type
            """;
        await using var conn = await factory.CreateOpenAsync(ct);
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            sql, new { Year = targetYear, Cat = targetCeremonyId, Type = signupType }, cancellationToken: ct));
    }

    public async Task<bool> SignupExistsAsync(int targetYear, Guid targetCeremonyId, int signupType, Guid believerId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT COUNT(1) FROM dbo.Signups
            WHERE Year = @Year AND CeremonyCategoryID = @Cat
              AND SignupType = @Type AND BelieverID = @BelieverId
            """;
        await using var conn = await factory.CreateOpenAsync(ct);
        var n = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            sql, new { Year = targetYear, Cat = targetCeremonyId, Type = signupType, BelieverId = believerId }, cancellationToken: ct));
        return n > 0;
    }

    public async Task<IReadOnlyList<PrepaySourceRow>> GetPrepaySourcesAsync(
        int sourceYear,
        Guid sourceCeremonyId,
        int signupType,
        int? employeeType,
        int targetYear,
        int targetSort,
        CancellationToken ct = default)
    {
        // 預繳條件：(PrepayYear=targetYear AND prepay.Sort>=targetSort) OR (PrepayYear>targetYear AND PrepayCeremonyCategoryID 不空)
        // 對齊 LoadPrepayForm.cs:80 source filter
        const string sql = """
            SELECT
              s.SignupID, s.BelieverID, s.SignupType, s.NumberTitle, s.Number, s.Fee,
              s.Name, s.Phone,
              s.LivingNameOne, s.LivingNameTwo, s.LivingNameThree, s.LivingNameFour, s.LivingNameFive, s.LivingNameSix,
              s.DeadNameOne,   s.DeadNameTwo,   s.DeadNameThree,   s.DeadNameFour,   s.DeadNameFive,   s.DeadNameSix,
              s.MailZipcodeID, s.MailZipcode, s.MailAddress,
              s.TextZipcodeID, s.TextZipcode, s.TextAddress,
              s.Remark,
              s.PrepayYear, s.PrepayCeremonyCategoryID,
              pc.Sort AS PrepayCeremonySort, pc.Title AS PrepayCeremonyTitle,
              b.IsFixedNumber, b.EmployeeType
            FROM dbo.Signups s
            INNER JOIN dbo.Believers b ON b.BelieverID = s.BelieverID
            LEFT JOIN dbo.CeremonyCategorys pc ON pc.CeremonyCategoryID = s.PrepayCeremonyCategoryID
            WHERE s.Year = @SourceYear
              AND s.CeremonyCategoryID = @SourceCat
              AND s.SignupType = @SignupType
              AND (@EmployeeType IS NULL OR b.EmployeeType = @EmployeeType)
              AND s.PrepayYear IS NOT NULL
              AND (
                   (s.PrepayYear = @TargetYear AND pc.Sort >= @TargetSort)
                OR (s.PrepayYear > @TargetYear AND s.PrepayCeremonyCategoryID IS NOT NULL)
              )
            ORDER BY b.IsFixedNumber DESC, s.Number
            """;

        await using var conn = await factory.CreateOpenAsync(ct);
        var rows = await conn.QueryAsync(new CommandDefinition(
            sql,
            new
            {
                SourceYear = sourceYear,
                SourceCat = sourceCeremonyId,
                SignupType = signupType,
                EmployeeType = employeeType,
                TargetYear = targetYear,
                TargetSort = targetSort,
            },
            cancellationToken: ct));

        var list = new List<PrepaySourceRow>();
        foreach (var r in rows)
        {
            var d = (IDictionary<string, object?>)r;
            list.Add(new PrepaySourceRow(
                SignupId: (Guid)d["SignupID"]!,
                BelieverId: (Guid)d["BelieverID"]!,
                SignupType: (int)d["SignupType"]!,
                NumberTitle: d["NumberTitle"] as string,
                Number: d["Number"] as int?,
                Fee: d["Fee"] as int?,
                Name: d["Name"] as string,
                Phone: d["Phone"] as string,
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
                MailZipcodeId: d["MailZipcodeID"] as int?,
                MailZipcode: d["MailZipcode"] as string,
                MailAddress: d["MailAddress"] as string,
                TextZipcodeId: d["TextZipcodeID"] as int?,
                TextZipcode: d["TextZipcode"] as string,
                TextAddress: d["TextAddress"] as string,
                Remark: d["Remark"] as string,
                PrepayYear: d["PrepayYear"] as int?,
                PrepayCeremonyCategoryId: d["PrepayCeremonyCategoryID"] as Guid?,
                PrepayCeremonySort: d["PrepayCeremonySort"] as int?,
                PrepayCeremonyTitle: d["PrepayCeremonyTitle"] as string,
                IsFixedNumber: d["IsFixedNumber"] is bool b && b,
                EmployeeType: d["EmployeeType"] as int?));
        }
        return list;
    }

    public async Task InsertBatchAsync(
        IReadOnlyList<(SignupWriteModel Signup, SignupLogWriteModel Log, int Number)> batch,
        CancellationToken ct = default)
    {
        if (batch.Count == 0) return;

        await using var conn = await factory.CreateOpenAsync(ct);
        using var tx = await ((SqlConnection)conn).BeginTransactionAsync(ct);

        try
        {
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

            foreach (var (s, l, number) in batch)
            {
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
            }

            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }
}
