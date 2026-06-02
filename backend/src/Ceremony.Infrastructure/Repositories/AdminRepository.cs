using Ceremony.Application.Admins;
using Ceremony.Application.Auth;
using Ceremony.Domain.Entities;
using Ceremony.Infrastructure.Persistence;
using Dapper;

namespace Ceremony.Infrastructure.Repositories;

/// <summary>
/// Dapper-based Admins 表存取。
/// </summary>
/// <remarks>
/// Legacy schema: dbo.Admins (AdminID, Name, Username, Password (plaintext), IsEnabled).
/// 對應舊系統：reference/old/Ceremony.Models/Repository/GenericRepository.cs
/// </remarks>
public sealed class AdminRepository(IDbConnectionFactory factory) : IAdminRepository
{
    public async Task<Admin?> GetByUsernameAsync(string username, CancellationToken ct = default)
    {
        const string sql = """
            SELECT TOP 1 AdminID AS AdminId, Name, Username, Password, IsEnabled
            FROM dbo.Admins
            WHERE Username = @Username AND IsEnabled = 1
            """;

        await using var conn = await factory.CreateOpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<Admin>(
            new CommandDefinition(sql, new { Username = username }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<AdminListItem>> GetAllEnabledAsync(CancellationToken ct = default)
    {
        // 不選 Password 欄位——明文密碼禁止離開 Repository 層（即使後端內部）。
        const string sql = """
            SELECT AdminID AS Id, Username, Name
            FROM dbo.Admins
            WHERE IsEnabled = 1
            ORDER BY Username
            """;

        await using var conn = await factory.CreateOpenAsync(ct);
        var rows = await conn.QueryAsync<AdminListItem>(new CommandDefinition(sql, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<bool> UsernameExistsAsync(string username, int? excludeId, CancellationToken ct = default)
    {
        // DB 無 UNIQUE constraint，應用層 enforce。
        const string sql = """
            SELECT COUNT(1) FROM dbo.Admins
            WHERE Username = @Username AND (@ExcludeId IS NULL OR AdminID <> @ExcludeId)
            """;

        await using var conn = await factory.CreateOpenAsync(ct);
        var count = await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { Username = username, ExcludeId = excludeId }, cancellationToken: ct));
        return count > 0;
    }

    public async Task<int> InsertAsync(string username, string password, string? name, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO dbo.Admins (Name, Username, Password, IsEnabled)
            OUTPUT INSERTED.AdminID
            VALUES (@Name, @Username, @Password, 1)
            """;

        await using var conn = await factory.CreateOpenAsync(ct);
        return await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { Name = name, Username = username, Password = password }, cancellationToken: ct));
    }

    public async Task<bool> UpdateAsync(int id, string? password, string? name, CancellationToken ct = default)
    {
        // password 為 null 視為「不變更密碼」（對應 PUT body 沒帶 password 的情況）；name 直接覆寫
        var sql = password is null
            ? "UPDATE dbo.Admins SET Name = @Name WHERE AdminID = @Id AND IsEnabled = 1"
            : "UPDATE dbo.Admins SET Name = @Name, Password = @Password WHERE AdminID = @Id AND IsEnabled = 1";

        await using var conn = await factory.CreateOpenAsync(ct);
        var affected = await conn.ExecuteAsync(
            new CommandDefinition(sql, new { Id = id, Name = name, Password = password }, cancellationToken: ct));
        return affected > 0;
    }

    public async Task<bool> SoftDeleteAsync(int id, CancellationToken ct = default)
    {
        // 對齊 AdminsForm.cs:143-146 — set IsEnabled = false
        const string sql = "UPDATE dbo.Admins SET IsEnabled = 0 WHERE AdminID = @Id AND IsEnabled = 1";

        await using var conn = await factory.CreateOpenAsync(ct);
        var affected = await conn.ExecuteAsync(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
        return affected > 0;
    }

    public async Task<AdminListItem?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        const string sql = """
            SELECT TOP 1 AdminID AS Id, Username, Name
            FROM dbo.Admins
            WHERE AdminID = @Id AND IsEnabled = 1
            """;

        await using var conn = await factory.CreateOpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<AdminListItem>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
    }
}
