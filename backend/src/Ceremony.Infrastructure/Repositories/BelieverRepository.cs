using System.Text;
using Ceremony.Application.Believers;
using Ceremony.Infrastructure.Persistence;
using Dapper;

namespace Ceremony.Infrastructure.Repositories;

/// <summary>
/// Dapper-based Believers 搜尋。動態組合 WHERE 條件，所有值走參數化避免 SQL injection。
/// </summary>
/// <remarks>
/// Legacy schema: dbo.Believers (BelieverID uniqueidentifier PK + 6×LivingName + 6×DeadName + 2 個 ZipcodeID 外鍵).
/// 對應舊系統：BelieverForm.cs:353-409 LoadBelievers
/// </remarks>
public sealed class BelieverRepository(IDbConnectionFactory factory) : IBelieverRepository
{
    public async Task<IReadOnlyList<BelieverListItem>> SearchAsync(BelieverSearchQuery query, CancellationToken ct = default)
    {
        var sql = new StringBuilder("""
            SELECT
              b.BelieverID, b.EmployeeType, b.HallName, b.Name, b.Phone, b.IsFixedNumber,
              b.MailZipcodeID, mz.City AS MailCity, mz.Area AS MailArea, b.MailAddress,
              b.TextZipcodeID, tz.City AS TextCity, tz.Area AS TextArea, b.TextAddress,
              b.LivingNameOne, b.LivingNameTwo, b.LivingNameThree, b.LivingNameFour, b.LivingNameFive, b.LivingNameSix,
              b.DeadNameOne,   b.DeadNameTwo,   b.DeadNameThree,   b.DeadNameFour,   b.DeadNameFive,   b.DeadNameSix
            FROM dbo.Believers b
            LEFT JOIN dbo.Zipcodes mz ON mz.ZipcodeID = b.MailZipcodeID
            LEFT JOIN dbo.Zipcodes tz ON tz.ZipcodeID = b.TextZipcodeID
            WHERE 1=1
            """);

        var p = new DynamicParameters();

        if (!string.IsNullOrEmpty(query.Name))
        {
            sql.AppendLine(" AND b.Name LIKE @Name");
            p.Add("@Name", Like(query.Name));
        }
        if (!string.IsNullOrEmpty(query.Phone))
        {
            sql.AppendLine(" AND b.Phone LIKE @Phone");
            p.Add("@Phone", Like(query.Phone));
        }
        if (!string.IsNullOrEmpty(query.HallName))
        {
            sql.AppendLine(" AND b.HallName LIKE @HallName");
            p.Add("@HallName", Like(query.HallName));
        }
        if (!string.IsNullOrEmpty(query.LivingName))
        {
            sql.AppendLine(" AND (b.LivingNameOne LIKE @LivingName OR b.LivingNameTwo LIKE @LivingName OR b.LivingNameThree LIKE @LivingName OR b.LivingNameFour LIKE @LivingName OR b.LivingNameFive LIKE @LivingName OR b.LivingNameSix LIKE @LivingName)");
            p.Add("@LivingName", Like(query.LivingName));
        }
        if (!string.IsNullOrEmpty(query.DeadName))
        {
            sql.AppendLine(" AND (b.DeadNameOne LIKE @DeadName OR b.DeadNameTwo LIKE @DeadName OR b.DeadNameThree LIKE @DeadName OR b.DeadNameFour LIKE @DeadName OR b.DeadNameFive LIKE @DeadName OR b.DeadNameSix LIKE @DeadName)");
            p.Add("@DeadName", Like(query.DeadName));
        }

        sql.AppendLine(" ORDER BY b.Name");

        await using var conn = await factory.CreateOpenAsync(ct);
        var rows = await conn.QueryAsync(new CommandDefinition(sql.ToString(), p, cancellationToken: ct));

        var list = new List<BelieverListItem>();
        foreach (var r in rows)
        {
            var d = (IDictionary<string, object?>)r;
            var employeeType = (int)(d["EmployeeType"] ?? 0);
            list.Add(new BelieverListItem(
                Id: (Guid)d["BelieverID"]!,
                EmployeeType: employeeType,
                EmployeeTypeTitle: EmployeeTitle(employeeType),
                HallName: d["HallName"] as string,
                Name: (string)d["Name"]!,
                Phone: d["Phone"] as string,
                IsFixedNumber: (bool)(d["IsFixedNumber"] ?? false),
                MailZipcodeId: d["MailZipcodeID"] as int?,
                MailCity: d["MailCity"] as string,
                MailArea: d["MailArea"] as string,
                MailAddress: d["MailAddress"] as string,
                TextZipcodeId: d["TextZipcodeID"] as int?,
                TextCity: d["TextCity"] as string,
                TextArea: d["TextArea"] as string,
                TextAddress: d["TextAddress"] as string,
                LivingNames:
                [
                    d["LivingNameOne"] as string, d["LivingNameTwo"] as string, d["LivingNameThree"] as string,
                    d["LivingNameFour"] as string, d["LivingNameFive"] as string, d["LivingNameSix"] as string,
                ],
                DeadNames:
                [
                    d["DeadNameOne"] as string, d["DeadNameTwo"] as string, d["DeadNameThree"] as string,
                    d["DeadNameFour"] as string, d["DeadNameFive"] as string, d["DeadNameSix"] as string,
                ]));
        }
        return list;
    }

    private static string Like(string value)
    {
        // Escape SQL LIKE wildcards % and _ in user input; 用 [] 包圍 [LIKE pattern ESCAPE '\\']。
        var escaped = value
            .Replace("[", "[[]")
            .Replace("%", "[%]")
            .Replace("_", "[_]");
        return $"%{escaped}%";
    }

    private static string EmployeeTitle(int type) => type switch
    {
        1 => "非員工",
        2 => "大殿",
        3 => "地藏殿",
        _ => string.Empty,
    };

    public async Task<BelieverListItem?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = """
            SELECT TOP 1
              b.BelieverID, b.EmployeeType, b.HallName, b.Name, b.Phone, b.IsFixedNumber,
              b.MailZipcodeID, mz.City AS MailCity, mz.Area AS MailArea, b.MailAddress,
              b.TextZipcodeID, tz.City AS TextCity, tz.Area AS TextArea, b.TextAddress,
              b.LivingNameOne, b.LivingNameTwo, b.LivingNameThree, b.LivingNameFour, b.LivingNameFive, b.LivingNameSix,
              b.DeadNameOne,   b.DeadNameTwo,   b.DeadNameThree,   b.DeadNameFour,   b.DeadNameFive,   b.DeadNameSix
            FROM dbo.Believers b
            LEFT JOIN dbo.Zipcodes mz ON mz.ZipcodeID = b.MailZipcodeID
            LEFT JOIN dbo.Zipcodes tz ON tz.ZipcodeID = b.TextZipcodeID
            WHERE b.BelieverID = @Id
            """;

        await using var conn = await factory.CreateOpenAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
        if (row is null) return null;
        var d = (IDictionary<string, object?>)row;
        var employeeType = (int)(d["EmployeeType"] ?? 0);
        return new BelieverListItem(
            Id: (Guid)d["BelieverID"]!,
            EmployeeType: employeeType,
            EmployeeTypeTitle: EmployeeTitle(employeeType),
            HallName: d["HallName"] as string,
            Name: (string)d["Name"]!,
            Phone: d["Phone"] as string,
            IsFixedNumber: (bool)(d["IsFixedNumber"] ?? false),
            MailZipcodeId: d["MailZipcodeID"] as int?,
            MailCity: d["MailCity"] as string,
            MailArea: d["MailArea"] as string,
            MailAddress: d["MailAddress"] as string,
            TextZipcodeId: d["TextZipcodeID"] as int?,
            TextCity: d["TextCity"] as string,
            TextArea: d["TextArea"] as string,
            TextAddress: d["TextAddress"] as string,
            LivingNames:
            [
                d["LivingNameOne"] as string, d["LivingNameTwo"] as string, d["LivingNameThree"] as string,
                d["LivingNameFour"] as string, d["LivingNameFive"] as string, d["LivingNameSix"] as string,
            ],
            DeadNames:
            [
                d["DeadNameOne"] as string, d["DeadNameTwo"] as string, d["DeadNameThree"] as string,
                d["DeadNameFour"] as string, d["DeadNameFive"] as string, d["DeadNameSix"] as string,
            ]);
    }

    public async Task<Guid> InsertAsync(BelieverWriteModel data, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO dbo.Believers (
              BelieverID, EmployeeType, HallName, Name, Phone, IsFixedNumber,
              MailZipcodeID, MailAddress, TextZipcodeID, TextAddress,
              LivingNameOne, LivingNameTwo, LivingNameThree, LivingNameFour, LivingNameFive, LivingNameSix,
              DeadNameOne, DeadNameTwo, DeadNameThree, DeadNameFour, DeadNameFive, DeadNameSix
            )
            OUTPUT INSERTED.BelieverID
            VALUES (
              @Id, @EmployeeType, @HallName, @Name, @Phone, @IsFixedNumber,
              @MailZipcodeID, @MailAddress, @TextZipcodeID, @TextAddress,
              @L1, @L2, @L3, @L4, @L5, @L6,
              @D1, @D2, @D3, @D4, @D5, @D6
            )
            """;

        var id = Guid.NewGuid();
        await using var conn = await factory.CreateOpenAsync(ct);
        return await conn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, BuildParams(id, data), cancellationToken: ct));
    }

    public async Task<bool> UpdateAsync(Guid id, BelieverWriteModel data, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE dbo.Believers SET
              EmployeeType=@EmployeeType, HallName=@HallName, Name=@Name, Phone=@Phone, IsFixedNumber=@IsFixedNumber,
              MailZipcodeID=@MailZipcodeID, MailAddress=@MailAddress,
              TextZipcodeID=@TextZipcodeID, TextAddress=@TextAddress,
              LivingNameOne=@L1, LivingNameTwo=@L2, LivingNameThree=@L3, LivingNameFour=@L4, LivingNameFive=@L5, LivingNameSix=@L6,
              DeadNameOne=@D1, DeadNameTwo=@D2, DeadNameThree=@D3, DeadNameFour=@D4, DeadNameFive=@D5, DeadNameSix=@D6
            WHERE BelieverID = @Id
            """;

        await using var conn = await factory.CreateOpenAsync(ct);
        var rows = await conn.ExecuteAsync(new CommandDefinition(sql, BuildParams(id, data), cancellationToken: ct));
        return rows > 0;
    }

    public async Task<bool> HasSignupsAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = "SELECT COUNT(1) FROM dbo.Signups WHERE BelieverID = @Id";
        await using var conn = await factory.CreateOpenAsync(ct);
        var n = await conn.ExecuteScalarAsync<int>(new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
        return n > 0;
    }

    public async Task<string?> GetNameAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = "SELECT TOP 1 Name FROM dbo.Believers WHERE BelieverID = @Id";
        await using var conn = await factory.CreateOpenAsync(ct);
        return await conn.ExecuteScalarAsync<string?>(new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM dbo.Believers WHERE BelieverID = @Id";
        await using var conn = await factory.CreateOpenAsync(ct);
        var rows = await conn.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
        return rows > 0;
    }

    private static object BuildParams(Guid id, BelieverWriteModel d) => new
    {
        Id = id,
        d.EmployeeType,
        d.HallName,
        d.Name,
        d.Phone,
        d.IsFixedNumber,
        MailZipcodeID = d.MailZipcodeId,
        d.MailAddress,
        TextZipcodeID = d.TextZipcodeId,
        d.TextAddress,
        L1 = d.LivingNames[0], L2 = d.LivingNames[1], L3 = d.LivingNames[2],
        L4 = d.LivingNames[3], L5 = d.LivingNames[4], L6 = d.LivingNames[5],
        D1 = d.DeadNames[0], D2 = d.DeadNames[1], D3 = d.DeadNames[2],
        D4 = d.DeadNames[3], D5 = d.DeadNames[4], D6 = d.DeadNames[5],
    };
}
