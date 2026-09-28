using Hangfire;
using Hangfire.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StockPulse.Domain.Interfaces;
using StockPulse.Infrastructure.Jobs;
using StockPulse.Infrastructure.Persistence;
using StockPulse.Infrastructure.Persistence.Repositories;

namespace StockPulse.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")!;

        services.AddDbContext<InventoryDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IVehicleRepository, VehicleRepository>();
        services.AddScoped<IVehicleActionLogRepository, VehicleActionLogRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<AgingStampJob>();

        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
            {
                CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                QueuePollInterval = TimeSpan.Zero,
                UseRecommendedIsolationLevel = true,
                DisableGlobalLocks = true
            }));

        services.AddHangfireServer();

        return services;
    }

    public static void ConfigureHangfireJobs(IConfiguration configuration, IRecurringJobManager recurringJobManager)
    {
        var cron = configuration["AgingJob:CronExpression"] ?? "0 2 * * *";
        recurringJobManager.AddOrUpdate<AgingStampJob>("aging-stamp", job => job.ExecuteAsync(), cron);
    }
}
