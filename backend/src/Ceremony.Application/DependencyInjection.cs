using Ceremony.Application.Admins;
using Ceremony.Application.Auth;
using Ceremony.Application.Backup;
using Ceremony.Application.Believers;
using Ceremony.Application.Categories;
using Ceremony.Application.Prepay;
using Ceremony.Application.Reports;
using Ceremony.Application.Signups;
using Ceremony.Application.Zipcodes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ceremony.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddCeremonyApplication(this IServiceCollection services, IConfiguration config)
    {
        services.AddMemoryCache();
        services.Configure<AuthOptions>(config.GetSection(AuthOptions.SectionName));
        services.Configure<JwtOptions>(config.GetSection(JwtOptions.SectionName));

        services.AddSingleton<JwtTokenService>();
        services.AddSingleton<LoginFailureTracker>();
        services.AddSingleton<IJwtBlacklist, MemoryJwtBlacklist>();
        services.AddScoped<LoginHandler>();
        services.AddScoped<LogoutHandler>();
        services.AddScoped<ListAdminsHandler>();
        services.AddScoped<CreateAdminHandler>();
        services.AddScoped<UpdateAdminHandler>();
        services.AddScoped<DeleteAdminHandler>();
        services.AddScoped<SearchBelieversHandler>();
        services.AddScoped<GetBelieverHandler>();
        services.AddScoped<CreateBelieverHandler>();
        services.AddScoped<UpdateBelieverHandler>();
        services.AddScoped<DeleteBelieverHandler>();
        services.AddScoped<ListCategoriesHandler>();
        services.AddScoped<SearchSignupsHandler>();
        services.AddScoped<GetSignupHandler>();
        services.AddScoped<ListSignupLogsHandler>();
        services.AddScoped<CreateSignupHandler>();
        services.AddScoped<UpdateSignupHandler>();
        services.AddScoped<DeleteSignupHandler>();
        services.AddScoped<ExportSignupsHandler>();
        services.AddScoped<CreateCategoryHandler>();
        services.AddScoped<UpdateCategoryHandler>();
        services.AddScoped<DeleteCategoryHandler>();
        services.AddScoped<PrepayLoadHandler>();
        services.AddScoped<GetBelieverLatestPrepayHandler>();
        services.AddScoped<GenerateDataCardHandler>();
        services.AddScoped<GenerateReceiptHandler>();
        services.AddScoped<GenerateTabletHandler>();
        services.AddScoped<GenerateTextHandler>();
        services.AddScoped<GenerateWorshipHandler>();
        services.AddScoped<BatchReportHandler>();
        services.AddScoped<BackupHandler>();
        services.AddScoped<ListZipcodeCitiesHandler>();
        services.AddScoped<ListZipcodeAreasHandler>();
        return services;
    }
}
