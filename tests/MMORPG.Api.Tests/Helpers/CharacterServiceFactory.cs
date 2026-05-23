using Microsoft.EntityFrameworkCore;
using MMORPG.Api.Data;
using MMORPG.Api.Services;

namespace MMORPG.Api.Tests.Helpers;

public static class CharacterServiceFactory
{
    public static (CharacterService service, ApplicationDbContext db) Create(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;

        var db = new ApplicationDbContext(options);
        return (new CharacterService(db), db);
    }
}
