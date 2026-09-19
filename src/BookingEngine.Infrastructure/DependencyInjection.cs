using BookingEngine.Application.Abstractions;
using BookingEngine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

        services.AddHealthChecks().AddDbContextCheck<BookingDbContext>(tags: ["ready"]);

        return services;
    }
}
