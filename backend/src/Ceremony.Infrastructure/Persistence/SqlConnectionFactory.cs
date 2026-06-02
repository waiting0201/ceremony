using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Ceremony.Infrastructure.Persistence;

public sealed class SqlConnectionFactory(IConfiguration config) : IDbConnectionFactory
{
    private readonly string _connectionString = config.GetConnectionString("Ceremony")
        ?? throw new InvalidOperationException(
            "ConnectionStrings:Ceremony missing. Set via dotnet user-secrets (dev) or " +
            "ENV var ConnectionStrings__Ceremony (prod). See docs/design/infrastructure.md.");

    public async Task<DbConnection> CreateOpenAsync(CancellationToken ct = default)
    {
        var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        return conn;
    }
}
