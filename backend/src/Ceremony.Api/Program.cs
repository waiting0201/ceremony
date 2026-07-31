using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Ceremony.Api.Middleware;
using Ceremony.Application;
using Ceremony.Application.Auth;
using Ceremony.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddCeremonyApplication(builder.Configuration);
builder.Services.AddCeremonyInfrastructure();

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = string.IsNullOrWhiteSpace(jwt.SigningKey)
                ? new SymmetricSecurityKey(Encoding.UTF8.GetBytes(new string('0', 32)))
                : new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
        };

        // 撤銷檢查：jti 命中黑名單 → 拒絕（POST /api/v1/auth/logout 寫入）
        o.Events = new JwtBearerEvents
        {
            OnTokenValidated = ctx =>
            {
                var jti = ctx.Principal?.FindFirstValue(JwtRegisteredClaimNames.Jti);
                if (!string.IsNullOrEmpty(jti))
                {
                    var blacklist = ctx.HttpContext.RequestServices.GetRequiredService<IJwtBlacklist>();
                    if (blacklist.IsRevoked(jti))
                        ctx.Fail("Token revoked");
                }
                return Task.CompletedTask;
            },
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:4200"])
     .AllowAnyHeader()
     .AllowAnyMethod()
     // 這三個不在 CORS safelist，不明示 expose 前端就讀不到（檔名會退回 fallback、筆數會是 undefined、
     // 紙張尺寸會退回 Electron 端的 fallback 表）。
     // dev 是 :4200→:5050 跨源，prod Electron renderer 走 file:// Origin=null 也算跨源，兩邊都需要。
     // 註：Electron 主行程送印時走 net.request（非瀏覽器 fetch），不受 CORS 限制，一定讀得到 header；
     // 這裡的 expose 是給 renderer 直接讀的路徑（報表預覽頁、dev :4200）。
     .WithExposedHeaders("Content-Disposition", "X-Signup-Count", "X-Report-Page-Size")));

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// 啟動時自動執行 DbUp schema migration（客戶端 Electron sidecar：安裝新版開 App 即自動就緒，免手動 CLU）。
// 冪等（journal dbo.SchemaVersions）→ 每次啟動只補未套用的腳本。失敗即 fail-fast 中止啟動（避免用殘缺 schema 服務）。
// 可用 config "Migration:RunOnStartup"=false 關閉；appsettings placeholder（未設真實連線）自動跳過。
{
    var runMigration = app.Configuration.GetValue("Migration:RunOnStartup", true);
    var migrationConn = app.Configuration.GetConnectionString("Ceremony");
    if (runMigration
        && !string.IsNullOrWhiteSpace(migrationConn)
        && !migrationConn.Contains("__OVERRIDE", StringComparison.Ordinal))
    {
        Log.Information("Schema migration 檢查中…");
        var result = Ceremony.Migrations.MigrationRunner.Run(
            migrationConn, msg => Log.Information("[migration] {Message}", msg));
        if (!result.Successful)
        {
            Log.Fatal("Schema migration 失敗，中止啟動：{Error}", result.Error);
            throw new InvalidOperationException($"Schema migration 失敗：{result.Error}");
        }
        Log.Information("Schema migration 完成（本次套用 {Count} 支腳本）。", result.ScriptsExecuted);
    }
}

app.UseSerilogRequestLogging();
app.UseMiddleware<ExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/", () => Results.Redirect("/health"));

app.Run();

// Expose for WebApplicationFactory in integration tests.
public partial class Program;
