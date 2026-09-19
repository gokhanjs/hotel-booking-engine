using System.Text.Json;
using BookingEngine.Application.Abstractions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace BookingEngine.Infrastructure.Caching;

// Entries live under a per-property version number; invalidation bumps the version so stale keys are never read and expire by TTL.
internal sealed partial class RedisPropertyCache(IConnectionMultiplexer redis, ILogger<RedisPropertyCache> logger) : IPropertyCache
{
    private static readonly TimeSpan _ttl = TimeSpan.FromMinutes(5);

    public async Task<T> GetOrCreateAsync<T>(Guid propertyId, string key, Func<CancellationToken, Task<T>> factory, CancellationToken ct)
    {
        string? entryKey = null;
        try
        {
            var db = redis.GetDatabase();
            var version = (long?)await db.StringGetAsync(VersionKey(propertyId)) ?? 0;
            entryKey = $"property:{propertyId}:v{version}:{key}";

            var cached = await db.StringGetAsync(entryKey);
            if (cached.HasValue)
            {
                return JsonSerializer.Deserialize<T>(cached.ToString())!;
            }
        }
        catch (RedisException ex)
        {
            LogCacheUnavailable(logger, ex);
        }

        var value = await factory(ct);

        if (entryKey is not null)
        {
            try
            {
                await redis.GetDatabase().StringSetAsync(entryKey, JsonSerializer.Serialize(value), _ttl);
            }
            catch (RedisException ex)
            {
                LogCacheUnavailable(logger, ex);
            }
        }

        return value;
    }

    public async Task InvalidateAsync(Guid propertyId, CancellationToken ct)
    {
        try
        {
            await redis.GetDatabase().StringIncrementAsync(VersionKey(propertyId));
        }
        catch (RedisException ex)
        {
            LogInvalidationFailed(logger, propertyId, ex);
        }
    }

    private static string VersionKey(Guid propertyId) => $"property:{propertyId}:version";

    [LoggerMessage(Level = LogLevel.Warning, Message = "Redis unavailable; serving uncached response.")]
    private static partial void LogCacheUnavailable(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Cache invalidation failed for property {PropertyId}; entries may be stale until TTL expiry.")]
    private static partial void LogInvalidationFailed(ILogger logger, Guid propertyId, Exception exception);
}
