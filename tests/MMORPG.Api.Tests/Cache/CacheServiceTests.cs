using MMORPG.Api.Tests.Helpers;

namespace MMORPG.Api.Tests.Cache;

/// <summary>
/// Unit tests for CacheService using an in-memory IDistributedCache.
/// Covers get, set, overwrite, remove, and exists semantics.
/// </summary>
public class CacheServiceTests
{
    // ── get ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Get_ReturnsNull_OnMiss()
    {
        var cache = CharacterServiceFactory.BuildCache();
        var result = await cache.GetAsync<string>("missing-key");
        Assert.Null(result);
    }

    [Fact]
    public async Task Get_ReturnsValue_AfterSet()
    {
        var cache = CharacterServiceFactory.BuildCache();
        await cache.SetAsync("k1", "hello", TimeSpan.FromMinutes(1));
        Assert.Equal("hello", await cache.GetAsync<string>("k1"));
    }

    [Fact]
    public async Task Get_DeserializesComplexType()
    {
        var cache = CharacterServiceFactory.BuildCache();
        var obj   = new { Name = "Legolas", Level = 42 };
        await cache.SetAsync("k2", obj, TimeSpan.FromMinutes(1));
        // System.Text.Json preserves Pascal-case property names by default
        var result = await cache.GetAsync<System.Text.Json.JsonElement>("k2");
        Assert.Equal("Legolas", result.GetProperty("Name").GetString());
    }

    // ── set ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Set_OverwritesExistingValue()
    {
        var cache = CharacterServiceFactory.BuildCache();
        await cache.SetAsync("k3", "original", TimeSpan.FromMinutes(1));
        await cache.SetAsync("k3", "updated",  TimeSpan.FromMinutes(1));
        Assert.Equal("updated", await cache.GetAsync<string>("k3"));
    }

    // ── remove ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Remove_DeletesKey()
    {
        var cache = CharacterServiceFactory.BuildCache();
        await cache.SetAsync("k4", "value", TimeSpan.FromMinutes(1));
        await cache.RemoveAsync("k4");
        Assert.Null(await cache.GetAsync<string>("k4"));
    }

    [Fact]
    public async Task Remove_IsNoOp_WhenKeyAbsent()
    {
        var cache = CharacterServiceFactory.BuildCache();
        // Should not throw
        await cache.RemoveAsync("nonexistent");
    }

    // ── exists ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Exists_ReturnsFalse_WhenNotSet()
    {
        var cache = CharacterServiceFactory.BuildCache();
        Assert.False(await cache.ExistsAsync("nope"));
    }

    [Fact]
    public async Task Exists_ReturnsTrue_WhenSet()
    {
        var cache = CharacterServiceFactory.BuildCache();
        await cache.SetAsync("k5", 42, TimeSpan.FromMinutes(1));
        Assert.True(await cache.ExistsAsync("k5"));
    }

    [Fact]
    public async Task Exists_ReturnsFalse_AfterRemove()
    {
        var cache = CharacterServiceFactory.BuildCache();
        await cache.SetAsync("k6", true, TimeSpan.FromMinutes(1));
        await cache.RemoveAsync("k6");
        Assert.False(await cache.ExistsAsync("k6"));
    }
}
