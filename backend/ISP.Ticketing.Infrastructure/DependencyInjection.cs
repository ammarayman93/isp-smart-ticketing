using ISP.Ticketing.Application.Common.Interfaces;
using ISP.Ticketing.Infrastructure.Persistence;
using ISP.Ticketing.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ISP.Ticketing.Infrastructure.Identity;

namespace ISP.Ticketing.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseMySql(
                connectionString,
                ServerVersion.AutoDetect(connectionString)));

        // Services
        services.AddScoped<IDateTimeService, DateTimeService>();
        services.AddScoped<ICategoryLookupService, CategoryLookupService>();
        services.AddScoped<IRoutingService, RoutingService>();
        services.AddScoped<IOutageDetectionService, OutageDetectionService>();
        services.AddScoped<IKnowledgeBaseService, KnowledgeBaseService>();
        services.AddScoped<IClassificationService, ISP.Ticketing.ML.Services.MlClassificationService>();
        services.AddScoped<ISP.Ticketing.ML.Training.MlTrainingService>();
        services.AddScoped<ISP.Ticketing.ML.Training.MlComparisonService>();
        services.AddHostedService<SlaMonitoringService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddHttpContextAccessor();
        services.AddHttpClient("PowerBI");
        services.AddScoped<IPowerBiService, PowerBiService>();

        return services;
    }
}