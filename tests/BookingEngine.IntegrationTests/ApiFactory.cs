using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BookingEngine.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

[assembly: AssemblyFixture(typeof(BookingEngine.IntegrationTests.ApiFactory))]

namespace BookingEngine.IntegrationTests;

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string ApiKey = "test-management-key";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine").Build();
    private readonly RedisContainer _redis = new RedisBuilder("redis:8-alpine").Build();

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());

        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
        builder.UseSetting("ConnectionStrings:Redis", _redis.GetConnectionString());
        builder.UseSetting("Management:ApiKey", ApiKey);
        builder.UseSetting("RateLimiting:StorefrontPermitsPerMinute", "100000");
    }

    public BookingDbContext CreateDbContext() =>
        Services.CreateScope().ServiceProvider.GetRequiredService<BookingDbContext>();

    public HttpClient CreateManagementClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", ApiKey);
        return client;
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
        await _redis.DisposeAsync();
    }
}

public static class HttpJsonExtensions
{
    public static Task<HttpResponseMessage> PostJsonAsync(this HttpClient client, string url, object body) =>
        client.PostAsJsonAsync(url, body, ApiFactory.Json, TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> PutJsonAsync(this HttpClient client, string url, object body) =>
        client.PutAsJsonAsync(url, body, ApiFactory.Json, TestContext.Current.CancellationToken);

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(ApiFactory.Json, TestContext.Current.CancellationToken))!;
}
