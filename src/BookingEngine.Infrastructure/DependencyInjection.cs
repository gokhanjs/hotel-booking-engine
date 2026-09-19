using BookingEngine.Application.Abstractions;
using BookingEngine.Infrastructure.Caching;
using BookingEngine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace BookingEngine.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<AuditInterceptor>();
        services.AddDbContext<BookingDbContext>((sp, options) => options
            .UseNpgsql(configuration.GetConnectionString("Postgres")
                ?? throw new InvalidOperationException("Connection string 'Postgres' is not configured."))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(sp.GetRequiredService<AuditInterceptor>()));
        services.AddScoped<IBookingDbContext>(sp => sp.GetRequiredService<BookingDbContext>());
        services.AddScoped<IAriWriter, AriWriter>();

        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var options = ConfigurationOptions.Parse(configuration.GetConnectionString("Redis")
                ?? throw new InvalidOperationException("Connection string 'Redis' is not configured."));
            options.AbortOnConnectFail = false;
            options.ConnectTimeout = 2000;
            options.AsyncTimeout = 1000;
            return ConnectionMultiplexer.Connect(options);
        });
        services.AddSingleton<IPropertyCache, RedisPropertyCache>();

        // Redis only accelerates reads, so losing it degrades the service instead of taking pods out of rotation.
        services.AddHealthChecks()
            .AddDbContextCheck<BookingDbContext>(tags: ["ready"])
            .AddCheck<RedisHealthCheck>("redis", HealthStatus.Degraded, ["ready"]);

        return services;
    }
}
