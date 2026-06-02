using Ceremony.Application.Categories;
using Ceremony.Infrastructure.Persistence;
using Dapper;

namespace Ceremony.Infrastructure.Repositories;

public sealed class CategoryRepository(IDbConnectionFactory factory) : ICategoryRepository
{
    public async Task<IReadOnlyList<CategoryRow>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT CeremonyCategoryID AS CeremonyCategoryId, Title, ParentID AS ParentId, Sort
            FROM dbo.CeremonyCategorys
            ORDER BY Sort
            """;

        await using var conn = await factory.CreateOpenAsync(ct);
        var rows = await conn.QueryAsync<CategoryRow>(new CommandDefinition(sql, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<CategoryRow?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = """
            SELECT TOP 1 CeremonyCategoryID AS CeremonyCategoryId, Title, ParentID AS ParentId, Sort
            FROM dbo.CeremonyCategorys WHERE CeremonyCategoryID = @Id
            """;
        await using var conn = await factory.CreateOpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<CategoryRow>(new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
    }

    public async Task<Guid> InsertAsync(string title, int sort, Guid? parentId, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO dbo.CeremonyCategorys (CeremonyCategoryID, Title, ParentID, Sort)
            OUTPUT INSERTED.CeremonyCategoryID
            VALUES (@Id, @Title, @ParentId, @Sort)
            """;
        var id = Guid.NewGuid();
        await using var conn = await factory.CreateOpenAsync(ct);
        return await conn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql,
            new { Id = id, Title = title, ParentId = parentId, Sort = sort }, cancellationToken: ct));
    }

    public async Task<bool> UpdateAsync(Guid id, string title, int sort, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE dbo.CeremonyCategorys SET Title = @Title, Sort = @Sort
            WHERE CeremonyCategoryID = @Id
            """;
        await using var conn = await factory.CreateOpenAsync(ct);
        var rows = await conn.ExecuteAsync(new CommandDefinition(sql,
            new { Id = id, Title = title, Sort = sort }, cancellationToken: ct));
        return rows > 0;
    }

    public async Task<bool> HasDependencyAsync(Guid id, CancellationToken ct = default)
    {
        // 對齊舊行為：Signups OR 子分類 都算依賴
        const string sql = """
            SELECT
              (SELECT COUNT(1) FROM dbo.Signups WHERE CeremonyCategoryID = @Id) AS SignupCount,
              (SELECT COUNT(1) FROM dbo.CeremonyCategorys WHERE ParentID = @Id) AS ChildCount
            """;
        await using var conn = await factory.CreateOpenAsync(ct);
        var row = await conn.QuerySingleAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
        var d = (IDictionary<string, object?>)row;
        return ((int)d["SignupCount"]! > 0) || ((int)d["ChildCount"]! > 0);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM dbo.CeremonyCategorys WHERE CeremonyCategoryID = @Id";
        await using var conn = await factory.CreateOpenAsync(ct);
        var rows = await conn.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
        return rows > 0;
    }
}
