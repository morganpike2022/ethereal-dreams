using MMORPG.Api.Data;
using MMORPG.Api.DTOs;
using MMORPG.Api.Models;
using MMORPG.Api.Services;
using MMORPG.Api.Tests.Helpers;

namespace MMORPG.Api.Tests.Cache;

/// <summary>
/// Unit tests for the cache behaviour layered inside CharacterService.
/// Uses InMemory EF + MemoryDistributedCache — no real DB or Redis required.
/// </summary>
public class CharacterCacheTests
{
    private static CharacterClass Warrior() => new()
    {
        Id = 1, Name = "warrior", DisplayName = "Warrior",
        BaseHp = 500, BaseMana = 100,
        BaseStrength = 20, BaseAgility = 10, BaseIntelligence = 5,
        BaseEndurance = 18, BaseSpirit = 8,
        HpPerLevel = 50, ManaPerLevel = 10
    };

    private static async Task<(CharacterService svc, ApplicationDbContext db, ICacheService cache)>
        SetupAsync(Action<ApplicationDbContext>? seed = null)
    {
        var (svc, db, cache) = CharacterServiceFactory.CreateAll();
        seed?.Invoke(db);
        if (db.ChangeTracker.HasChanges())
            await db.SaveChangesAsync();
        return (svc, db, cache);
    }

    // ── select screen: cache miss populates ───────────────────────────────────

    [Fact]
    public async Task GetSelectScreen_CacheMiss_PopulatesCache()
    {
        var (svc, _, cache) = await SetupAsync(db => db.CharacterClasses.Add(Warrior()));
        var playerId = Guid.NewGuid();
        await svc.CreateAsync(playerId, new CreateCharacterRequest("CacheHero", 1, null));

        await svc.GetSelectScreenAsync(playerId);

        Assert.True(await cache.ExistsAsync(CacheKeys.CharacterSelect(playerId)));
    }

    // ── select screen: cache hit returns stale data ───────────────────────────

    [Fact]
    public async Task GetSelectScreen_CacheHit_ReturnsStaleData()
    {
        var (svc, db, _) = await SetupAsync(db => db.CharacterClasses.Add(Warrior()));
        var playerId = Guid.NewGuid();
        await svc.CreateAsync(playerId, new CreateCharacterRequest("HeroA", 1, null));

        // First call: populates cache with 1 character
        var result1 = await svc.GetSelectScreenAsync(playerId);
        Assert.Single(result1);

        // Insert a second character directly into DB — bypasses service so cache stays
        db.Characters.Add(new Character
        {
            Id        = Guid.NewGuid(), PlayerId = playerId, ClassId = 1,
            Name      = "StealthB", Level = 1,
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        // Second call should return the cached 1-character list, not the DB's 2
        var result2 = await svc.GetSelectScreenAsync(playerId);
        Assert.Single(result2);
    }

    // ── select screen: force refresh bypasses cache ───────────────────────────

    [Fact]
    public async Task GetSelectScreen_ForceRefresh_BypassesCache()
    {
        var (svc, db, _) = await SetupAsync(db => db.CharacterClasses.Add(Warrior()));
        var playerId = Guid.NewGuid();
        await svc.CreateAsync(playerId, new CreateCharacterRequest("HeroA", 1, null));

        // Populate cache with 1 char
        await svc.GetSelectScreenAsync(playerId);

        // Directly add a second character without invalidating cache
        db.Characters.Add(new Character
        {
            Id        = Guid.NewGuid(), PlayerId = playerId, ClassId = 1,
            Name      = "StealthB", Level = 1,
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        // Force refresh must bypass cache and return both chars from DB
        var result = await svc.GetSelectScreenAsync(playerId, forceRefresh: true);
        Assert.Equal(2, result.Count);
    }

    // ── select screen: invalidated on create ─────────────────────────────────

    [Fact]
    public async Task GetSelectScreen_InvalidatesOnCreate()
    {
        var (svc, _, cache) = await SetupAsync(db => db.CharacterClasses.Add(Warrior()));
        var playerId = Guid.NewGuid();
        await svc.CreateAsync(playerId, new CreateCharacterRequest("HeroA", 1, null));

        // Populate cache
        await svc.GetSelectScreenAsync(playerId);
        Assert.True(await cache.ExistsAsync(CacheKeys.CharacterSelect(playerId)));

        // Creating another character must invalidate the cache
        await svc.CreateAsync(playerId, new CreateCharacterRequest("HeroB", 1, null));

        Assert.False(await cache.ExistsAsync(CacheKeys.CharacterSelect(playerId)));
    }

    // ── select screen: invalidated on delete ─────────────────────────────────

    [Fact]
    public async Task GetSelectScreen_InvalidatesOnDelete()
    {
        var (svc, _, cache) = await SetupAsync(db => db.CharacterClasses.Add(Warrior()));
        var playerId = Guid.NewGuid();
        var char1 = await svc.CreateAsync(playerId, new CreateCharacterRequest("HeroA", 1, null));
        await svc.CreateAsync(playerId, new CreateCharacterRequest("HeroB", 1, null));

        // Populate cache
        await svc.GetSelectScreenAsync(playerId);
        Assert.True(await cache.ExistsAsync(CacheKeys.CharacterSelect(playerId)));

        // Deleting must invalidate the cache
        await svc.DeleteAsync(char1.Id, playerId);

        Assert.False(await cache.ExistsAsync(CacheKeys.CharacterSelect(playerId)));
    }

    // ── name validation: cache miss populates ────────────────────────────────

    [Fact]
    public async Task ValidateName_CacheMiss_PopulatesCache()
    {
        var (svc, _, cache) = await SetupAsync();

        await svc.ValidateNameAsync("Legolas");

        // Key uses lowercase normalisation
        Assert.True(await cache.ExistsAsync(CacheKeys.NameAvailable("legolas")));
    }

    // ── name validation: cache hit returns stale data ────────────────────────

    [Fact]
    public async Task ValidateName_CacheHit_ReturnsCachedResult()
    {
        var (svc, db, _) = await SetupAsync();

        // First call: name is available → cached as available
        var result1 = await svc.ValidateNameAsync("Legolas");
        Assert.True(result1.Available);

        // Add character with that name directly in DB (no service = no cache invalidation)
        db.Characters.Add(new Character
        {
            Id        = Guid.NewGuid(), PlayerId = Guid.NewGuid(),
            Name      = "Legolas", Level = 1,
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        // Second call should still return the cached "available" result
        var result2 = await svc.ValidateNameAsync("Legolas");
        Assert.True(result2.Available);
    }

    // ── name validation: invalidated on create ───────────────────────────────

    [Fact]
    public async Task ValidateName_InvalidatesOnCreate()
    {
        var (svc, _, cache) = await SetupAsync(db => db.CharacterClasses.Add(Warrior()));
        var playerId = Guid.NewGuid();

        // Cache the "available" result
        await svc.ValidateNameAsync("Gandalf");
        Assert.True(await cache.ExistsAsync(CacheKeys.NameAvailable("gandalf")));

        // CreateAsync must remove the name from cache so next lookup hits DB
        await svc.CreateAsync(playerId, new CreateCharacterRequest("Gandalf", 1, null));

        Assert.False(await cache.ExistsAsync(CacheKeys.NameAvailable("gandalf")));
    }

    // ── name validation: format errors skip cache ────────────────────────────

    [Fact]
    public async Task ValidateName_FormatError_DoesNotPopulateCache()
    {
        var (svc, _, cache) = await SetupAsync();

        await svc.ValidateNameAsync("A"); // too short — format error, no DB hit
        await svc.ValidateNameAsync("1stHero"); // starts with digit — format error

        Assert.False(await cache.ExistsAsync(CacheKeys.NameAvailable("a")));
        Assert.False(await cache.ExistsAsync(CacheKeys.NameAvailable("1sthero")));
    }
}
