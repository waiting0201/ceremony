using Ceremony.Application.Prepay;
using Ceremony.Application.Signups;
using Ceremony.Domain.Exceptions;
using Ceremony.Domain.Services;
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
              b.IsFixedNumber, b.EmployeeType, b.Name AS BelieverName
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
                EmployeeType: d["EmployeeType"] as int?,
                BelieverName: (string)d["BelieverName"]!));
        }
        return list;
    }

    public async Task<PrepayLoadResponse> InsertPrepayBatchAsync(
        int targetYear,
        Guid targetCeremonyId,
        int signupType,
        IReadOnlyList<PrepayCandidate> fixedCandidates,
        IReadOnlyList<PrepayCandidate> nonFixedCandidates,
        CancellationToken ct = default)
    {
        await using var conn = await factory.CreateOpenAsync(ct);
        using var tx = await ((SqlConnection)conn).BeginTransactionAsync(ct);

        try
        {
            // 群組互斥鎖：序列化「同 (Year, Ceremony, SignupType)」的並發配號作業（預繳載入 / 插入順移共用同一
            // resource 命名空間 "signup-number:"，避免兩者互踩）。Transaction owner → commit/rollback 自動釋放。逾時 30s 回 -1。
            var lockRc = await conn.ExecuteScalarAsync<int>(new CommandDefinition("""
                DECLARE @rc int;
                EXEC @rc = sp_getapplock @Resource = @Resource, @LockMode = 'Exclusive',
                     @LockOwner = 'Transaction', @LockTimeout = 30000;
                SELECT @rc;
                """,
                new { Resource = $"signup-number:{targetYear}:{targetCeremonyId}:{signupType}" },
                transaction: tx, cancellationToken: ct));
            if (lockRc < 0)
                throw new DomainException("PREPAY_BUSY", "另一筆預繳載入進行中，請稍後再試");

            // 已存在信眾（idempotency）：同交易內讀，配合下方 MAX 的範圍鎖。
            const string existingSql = """
                SELECT BelieverID FROM dbo.Signups
                WHERE Year = @Year AND CeremonyCategoryID = @Cat AND SignupType = @Type
                """;
            var existing = (await conn.QueryAsync<Guid>(new CommandDefinition(
                existingSql, new { Year = targetYear, Cat = targetCeremonyId, Type = signupType },
                transaction: tx, cancellationToken: ct))).ToHashSet();

            var loadedFixed = fixedCandidates.Where(c => !existing.Contains(c.BelieverId)).ToList();
            var loadedNonFixed = nonFixedCandidates.Where(c => !existing.Contains(c.BelieverId)).ToList();
            var skipped = (fixedCandidates.Count - loadedFixed.Count)
                        + (nonFixedCandidates.Count - loadedNonFixed.Count);

            // UPDLOCK + HOLDLOCK 範圍鎖：擋住並發的一般報名/預繳插入，讀到的 MAX 到 commit 前不會被搶號。
            const string maxSql = """
                SELECT ISNULL(MAX(Number), 0) FROM dbo.Signups WITH (UPDLOCK, HOLDLOCK)
                WHERE Year = @Year AND CeremonyCategoryID = @Cat AND SignupType = @Type
                """;
            var maxNumber = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
                maxSql, new { Year = targetYear, Cat = targetCeremonyId, Type = signupType },
                transaction: tx, cancellationToken: ct));

            // 配號（純函式，對齊舊 LoadPrepayForm 演算法）。
            var alloc = PrepayNumberAllocator.Allocate(
                maxNumber,
                loadedFixed.Select(c => c.PreservedNumber).ToList(),
                loadedNonFixed.Count);

            var batch = new List<(SignupWriteModel Signup, SignupLogWriteModel Log, int Number)>(
                loadedFixed.Count + loadedNonFixed.Count);
            for (var i = 0; i < loadedFixed.Count; i++)
                batch.Add((loadedFixed[i].Signup, loadedFixed[i].Log, alloc.FixedNumbers[i]));
            for (var i = 0; i < loadedNonFixed.Count; i++)
                batch.Add((loadedNonFixed[i].Signup, loadedNonFixed[i].Log, alloc.NonFixedNumbers[i]));

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

            var carried = loadedFixed.Count(c => c.CarriedForward)
                        + loadedNonFixed.Count(c => c.CarriedForward);

            return new PrepayLoadResponse(
                Loaded: loadedFixed.Count + loadedNonFixed.Count,
                Skipped: skipped,
                Details: new PrepayLoadDetails(
                    FixedLoaded: loadedFixed.Count,
                    NonFixedLoaded: loadedNonFixed.Count,
                    CarriedForwardPrepay: carried,
                    FilledGaps: alloc.FilledGaps));
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }
}
