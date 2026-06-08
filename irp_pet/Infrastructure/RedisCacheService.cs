using Microsoft.Extensions.Caching.Distributed;

namespace irp_pet.Infrastructure;

public class RedisCacheService
{
    private readonly IDistributedCache _cache;

    public RedisCacheService(IDistributedCache cache) => _cache = cache;

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        var value = await _cache.GetStringAsync(key, ct);
        return value is not null;
    }

    public Task<string?> GetAsync(string key, CancellationToken ct = default) =>
        _cache.GetStringAsync(key, ct);

    public Task SetAsync(string key, string value, TimeSpan ttl, CancellationToken ct = default) =>
        _cache.SetStringAsync(key, value, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl }, ct);
}
