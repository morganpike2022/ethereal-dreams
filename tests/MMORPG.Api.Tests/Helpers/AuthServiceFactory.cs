using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MMORPG.Api.Data;
using MMORPG.Api.Services;

namespace MMORPG.Api.Tests.Helpers;

public static class AuthServiceFactory
{
    public static (AuthService service, ApplicationDbContext db) Create(string? dbName = null)
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

        return (new AuthService(db, config), db);
    }
}
