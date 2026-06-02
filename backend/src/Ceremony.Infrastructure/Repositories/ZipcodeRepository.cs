using Ceremony.Application.Zipcodes;
using Ceremony.Infrastructure.Persistence;
using Dapper;

namespace Ceremony.Infrastructure.Repositories;

/// <summary>
/// Zipcodes 唯讀查詢（Dapper）。對齊舊 ZipcodesService 的 LoadCity / by-city 查詢。
/// 注意：舊 NewSignupForm/BelieverForm 載入城市/區域時皆未過濾 IsDisplay，這裡保持一致（不過濾）。
/// </summary>
public sealed class ZipcodeRepository(IDbConnectionFactory factory) : IZipcodeRepository
{
    public async Task<IReadOnlyList<string>> GetCitiesAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT City
            FROM dbo.Zipcodes
            GROUP BY City
            ORDER BY City
            """;
        await using var conn = await factory.CreateOpenAsync(ct);
        var rows = await conn.QueryAsync<string>(new CommandDefinition(sql, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<ZipcodeRow>> GetByCityAsync(string city, CancellationToken ct = default)
    {
        const string sql = """
            SELECT ZipcodeID AS ZipcodeId, City, Area, Zipcode
            FROM dbo.Zipcodes
            WHERE City = @City
            ORDER BY Zipcode
            """;
        await using var conn = await factory.CreateOpenAsync(ct);
        var rows = await conn.QueryAsync<ZipcodeRow>(
            new CommandDefinition(sql, new { City = city }, cancellationToken: ct));
        return rows.AsList();
    }
}
