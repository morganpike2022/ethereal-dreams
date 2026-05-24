using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using MMORPG.Api.Data;
using MMORPG.Api.Services;

namespace MMORPG.Api.Tests.Helpers;

public static class CharacterServiceFactory
{
    /// <summary>Returns service + db for tests that don't need direct cache access.</summary>
    public static (CharacterService service, ApplicationDbContext db) Create(string? dbName = null)
    {
        var (svc, db, _) = CreateAll(dbName);
        return (svc, db);
    }

    /// <summary>Returns service + db + cache for cache-behaviour tests.</summary>
    internal static (CharacterService service, ApplicationDbContext db, ICacheService cache) CreateAll(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;

        var db    = new ApplicationDbContext(options);
        var cache = BuildCache();
        return (new CharacterService(db, cache), db, cache);
    }

    internal static ICacheService BuildCache()
    {
        var services = new ServiceCollection();
        services.AddDistributedMemoryCache();
        var provider = services.BuildServiceProvider();
        return new CacheService(provider.GetRequiredService<IDistributedCache>());
    }
}
