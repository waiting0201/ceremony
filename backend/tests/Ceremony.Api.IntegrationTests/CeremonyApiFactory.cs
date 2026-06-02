using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Ceremony.Api.IntegrationTests;

/// <summary>
/// WebApplicationFactory for end-to-end HTTP tests against the real API stack.
/// </summary>
/// <remarks>
/// 注意：本工廠使用 ASPNETCORE_ENVIRONMENT=Development，會載入 Api project 的 dotnet user-secrets，
/// 含真實 (local) MSSQL 連線。CI 環境執行時請改用獨立 test DB 或 Testcontainers。
/// 跨平台一致性 trade-off 見 docs/workflows/qa-testing.md「整合測試」段。
/// </remarks>
public sealed class CeremonyApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((ctx, conf) =>
        {
            // 允許在 ENV 變數覆蓋（CI 用）；空時 fall back 到 Api project 的 user-secrets。
            var envConn = Environment.GetEnvironmentVariable("ConnectionStrings__Ceremony");
            var envJwt = Environment.GetEnvironmentVariable("Jwt__SigningKey");
            var overrides = new Dictionary<string, string?>();
            if (!string.IsNullOrWhiteSpace(envConn)) overrides["ConnectionStrings:Ceremony"] = envConn;
            if (!string.IsNullOrWhiteSpace(envJwt)) overrides["Jwt:SigningKey"] = envJwt;
            if (overrides.Count > 0) conf.AddInMemoryCollection(overrides);
        });
    }
}
