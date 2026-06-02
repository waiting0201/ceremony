using System.Data.Common;

namespace Ceremony.Infrastructure.Persistence;

public interface IDbConnectionFactory
{
    Task<DbConnection> CreateOpenAsync(CancellationToken ct = default);
}
