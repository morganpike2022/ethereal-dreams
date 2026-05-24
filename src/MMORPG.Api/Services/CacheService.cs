using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace MMORPG.Api.Services;

public class CacheService(IDistributedCache cache) : ICacheService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        var bytes = await cache.GetAsync(key, ct);
        return bytes is null ? default : JsonSerializer.Deserialize<T>(bytes, JsonOpts);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonOpts);
        await cache.SetAsync(key, bytes,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl }, ct);
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
        => cache.RemoveAsync(key, ct);

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
        => await cache.GetAsync(key, ct) is not null;
}

public static class CacheKeys
{
    public static string CharacterSelect(Guid playerId) => $"char-select:{playerId}";
    public static string NameAvailable(string name)    => $"name-valid:{name.ToLower()}";
    public static string JtiRevoked(string jti)        => $"jti-revoked:{jti}";
}
