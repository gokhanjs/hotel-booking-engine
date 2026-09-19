using BookingEngine.Infrastructure;
using BookingEngine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(BookingEngine.IntegrationTests.PostgresFixture))]

namespace BookingEngine.IntegrationTests;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18-alpine").Build();
    private ServiceProvider _services = null!;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = _container.GetConnectionString(),
            })
            .Build();

        _services = new ServiceCollection().AddInfrastructure(configuration).BuildServiceProvider();

        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
    }

    public BookingDbContext CreateDbContext() =>
        _services.CreateScope().ServiceProvider.GetRequiredService<BookingDbContext>();

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        await _container.DisposeAsync();
    }
}
