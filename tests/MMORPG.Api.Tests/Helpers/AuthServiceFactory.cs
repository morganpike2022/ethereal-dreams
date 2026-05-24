using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MMORPG.Api.Data;
using MMORPG.Api.Services;

namespace MMORPG.Api.Tests.Helpers;

public static class AuthServiceFactory
{
    public static (AuthService service, ApplicationDbContext db) Create(string? dbName = null)
    {
        var (svc, db, _) = CreateAll(dbName);
        return (svc, db);
    }

    internal static (AuthService service, ApplicationDbContext db, ICacheService cache) CreateAll(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;

        var db = new ApplicationDbContext(options);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"]                = "test-secret-key-that-is-long-enough-for-hmacsha256-256bits!",
                ["Jwt:Issuer"]                   = "test-issuer",
                ["Jwt:Audience"]                 = "test-audience",
                ["Jwt:AccessTokenExpiryMinutes"] = "15",
                ["Jwt:RefreshTokenExpiryDays"]   = "7"
            })
            .Build();

        var sc = new ServiceCollection();
        sc.AddDistributedMemoryCache();
        var cache = new CacheService(sc.BuildServiceProvider().GetRequiredService<IDistributedCache>());
        return (new AuthService(db, config, cache), db, cache);
    }
}
