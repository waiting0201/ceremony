using Ceremony.Application.Auth;
using Ceremony.Application.Backup;
using Ceremony.Application.Believers;
using Ceremony.Application.Categories;
using Ceremony.Application.Prepay;
using Ceremony.Application.Reports;
using Ceremony.Application.Signups;
using Ceremony.Application.Zipcodes;
using Ceremony.Infrastructure.Backup;
using Ceremony.Infrastructure.Persistence;
using Ceremony.Infrastructure.Reporting;
using Ceremony.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;
using QuestPDF.Infrastructure;

namespace Ceremony.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCeremonyInfrastructure(this IServiceCollection services)
    {
        // QuestPDF Community license (revenue < $1M USD/yr — for寶覺寺 適用)
        QuestPDF.Settings.License = LicenseType.Community;

        // 註冊標楷體為 QuestPDF family "BiauKai"，避免 silently fallback 系統 CJK 字型
        // （fallback 會使薦牌/文牒直書字寬與位置全跑掉）。見 Reporting/ReportFonts.cs。
        Reporting.ReportFonts.EnsureRegistered();

        services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
        services.AddScoped<IAdminRepository, AdminRepository>();
        services.AddScoped<IBelieverRepository, BelieverRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<ISignupRepository, SignupRepository>();
        services.AddScoped<ISignupLogRepository, SignupLogRepository>();
        services.AddScoped<IPrepayRepository, PrepayRepository>();
        services.AddScoped<IZipcodeRepository, ZipcodeRepository>();
        services.AddSingleton<DataCardRenderer>();
        services.AddSingleton<ReceiptRenderer>();
        services.AddSingleton<TabletRenderer>();
        services.AddSingleton<TextRenderer>();
        services.AddSingleton<WorshipRenderer>();
        services.AddSingleton<WorshipCardRenderer>();
        services.AddSingleton<IReportRenderer, QuestPdfReportRenderer>();
        services.AddSingleton<IPdfMerger, PdfSharpMerger>();
        services.AddScoped<IBackupService, SqlBackupService>();
        return services;
    }
}
