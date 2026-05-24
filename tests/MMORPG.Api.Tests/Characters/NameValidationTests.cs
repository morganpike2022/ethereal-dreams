using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MMORPG.Api.Configuration;
using MMORPG.Api.Data;
using MMORPG.Api.Models;
using MMORPG.Api.Services;
using MMORPG.Api.Tests.Helpers;

namespace MMORPG.Api.Tests.Characters;

public class NameValidationTests
{
    // ── happy path ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ValidName_ReturnsAvailable()
    {
        var (service, _) = CharacterServiceFactory.Create();
        var result = await service.ValidateNameAsync("Aragorn");
        Assert.True(result.Available);
        Assert.Null(result.Reason);
    }

    // ── length rules — ETH-7: 3–20 characters ───────────────────────────────

    [Fact]
    public async Task NameOf1Char_ReturnsFalse()
    {
        var (service, _) = CharacterServiceFactory.Create();
        var result = await service.ValidateNameAsync("A");
        Assert.False(result.Available);
        Assert.Contains("3 and 20", result.Reason);
    }

    [Fact]
    public async Task NameOf2Chars_ReturnsFalse()
    {
        var (service, _) = CharacterServiceFactory.Create();
        var result = await service.ValidateNameAsync("Ab");
        Assert.False(result.Available);
        Assert.Contains("3 and 20", result.Reason);
    }

    [Fact]
    public async Task NameExactly3Chars_ReturnsAvailable()
    {
        var (service, _) = CharacterServiceFactory.Create();
        var result = await service.ValidateNameAsync("Abc");
        Assert.True(result.Available);
    }

    [Fact]
    public async Task NameExactly20Chars_ReturnsAvailable()
    {
        var (service, _) = CharacterServiceFactory.Create();
        var result = await service.ValidateNameAsync("A" + new string('b', 19));
        Assert.True(result.Available);
    }

    [Fact]
    public async Task NameOf21Chars_ReturnsFalse()
    {
        var (service, _) = CharacterServiceFactory.Create();
        var result = await service.ValidateNameAsync("A" + new string('b', 20));
        Assert.False(result.Available);
        Assert.Contains("3 and 20", result.Reason);
    }

    // ── character set rules — ETH-7: letters + single specials, no digits ───

    [Fact]
    public async Task NameWithDigit_ReturnsFalse()
    {
        var (service, _) = CharacterServiceFactory.Create();
        var result = await service.ValidateNameAsync("Her2o");
        Assert.False(result.Available);
        Assert.NotNull(result.Reason);
    }

    [Fact]
    public async Task NameWithSpace_ReturnsFalse()
    {
        var (service, _) = CharacterServiceFactory.Create();
        var result = await service.ValidateNameAsync("Dark Knight");
        Assert.False(result.Available);
        Assert.NotNull(result.Reason);
    }

    [Fact]
    public async Task NameWithSpecialChar_ReturnsFalse()
    {
        var (service, _) = CharacterServiceFactory.Create();
        var result = await service.ValidateNameAsync("Hero!");
        Assert.False(result.Available);
        Assert.NotNull(result.Reason);
    }

    [Fact]
    public async Task NameWithHyphen_ReturnsAvailable()
    {
        var (service, _) = CharacterServiceFactory.Create();
        var result = await service.ValidateNameAsync("Half-Elf");
        Assert.True(result.Available);
    }

    [Fact]
    public async Task NameWithApostrophe_ReturnsAvailable()
    {
        var (service, _) = CharacterServiceFactory.Create();
        var result = await service.ValidateNameAsync("D'Artagnan");
        Assert.True(result.Available);
    }

    // ── consecutive specials — ETH-7 ────────────────────────────────────────

    [Fact]
    public async Task ConsecutiveHyphens_ReturnsFalse()
    {
        var (service, _) = CharacterServiceFactory.Create();
        var result = await service.ValidateNameAsync("Half--Elf");
        Assert.False(result.Available);
        Assert.NotNull(result.Reason);
    }

    [Fact]
    public async Task ConsecutiveApostrophes_ReturnsFalse()
    {
        var (service, _) = CharacterServiceFactory.Create();
        var result = await service.ValidateNameAsync("D''Art");
        Assert.False(result.Available);
        Assert.NotNull(result.Reason);
    }

    [Fact]
    public async Task TrailingHyphen_ReturnsFalse()
    {
        var (service, _) = CharacterServiceFactory.Create();
        var result = await service.ValidateNameAsync("Hero-");
        Assert.False(result.Available);
        Assert.NotNull(result.Reason);
    }

    [Fact]
    public async Task TrailingApostrophe_ReturnsFalse()
    {
        var (service, _) = CharacterServiceFactory.Create();
        var result = await service.ValidateNameAsync("Hero'");
        Assert.False(result.Available);
        Assert.NotNull(result.Reason);
    }

    // ── must start with letter ──────────────────────────────────────────────

    [Fact]
    public async Task NameStartingWithDigit_ReturnsFalse()
    {
        var (service, _) = CharacterServiceFactory.Create();
        var result = await service.ValidateNameAsync("1stHero");
        Assert.False(result.Available);
        Assert.Contains("start with a letter", result.Reason);
    }

    [Fact]
    public async Task NameStartingWithHyphen_ReturnsFalse()
    {
        var (service, _) = CharacterServiceFactory.Create();
        var result = await service.ValidateNameAsync("-Dash");
        Assert.False(result.Available);
        Assert.Contains("start with a letter", result.Reason);
    }

    // ── blocklist — ETH-7 ───────────────────────────────────────────────────

    [Fact]
    public async Task BlocklistedName_ReturnsFalse()
    {
        var (service, _) = CharacterServiceFactory.Create();
        var result = await service.ValidateNameAsync("Admin");
        Assert.False(result.Available);
        Assert.Contains("reserved", result.Reason);
    }

    [Fact]
    public async Task BlocklistedName_CaseInsensitive_ReturnsFalse()
    {
        var (service, _) = CharacterServiceFactory.Create();
        var result = await service.ValidateNameAsync("admin");
        Assert.False(result.Available);
        Assert.Contains("reserved", result.Reason);
    }

    [Fact]
    public async Task BlocklistedName_MixedCase_ReturnsFalse()
    {
        var (service, _) = CharacterServiceFactory.Create();
        var result = await service.ValidateNameAsync("GameMaster");
        Assert.False(result.Available);
        Assert.Contains("reserved", result.Reason);
    }

    [Fact]
    public async Task CustomBlocklistedName_ReturnsFalse()
    {
        // Verify the reserved list can be overridden via options
        var opts = Options.Create(new NameValidationOptions
        {
            ReservedNames = ["Shadowbane", "Darknight"]
        });
        var sc = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        sc.AddDistributedMemoryCache();
        var cache = new CacheService(sc.BuildServiceProvider().GetRequiredService<Microsoft.Extensions.Caching.Distributed.IDistributedCache>());

        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new ApplicationDbContext(options);

        var service = new CharacterService(db, cache, opts);
        var result  = await service.ValidateNameAsync("Shadowbane");
        Assert.False(result.Available);
        Assert.Contains("reserved", result.Reason);
    }

    // ── uniqueness (case-insensitive) ────────────────────────────────────────

    [Fact]
    public async Task DuplicateName_ExactCase_ReturnsFalse()
    {
        var (service, db) = CharacterServiceFactory.Create();
        db.Characters.Add(new Character { Id = Guid.NewGuid(), Name = "Legolas", PlayerId = Guid.NewGuid() });
        await db.SaveChangesAsync();

        var result = await service.ValidateNameAsync("Legolas");
        Assert.False(result.Available);
        Assert.Contains("already taken", result.Reason);
    }

    [Fact]
    public async Task DuplicateName_DifferentCase_ReturnsFalse()
    {
        var (service, db) = CharacterServiceFactory.Create();
        db.Characters.Add(new Character { Id = Guid.NewGuid(), Name = "Legolas", PlayerId = Guid.NewGuid() });
        await db.SaveChangesAsync();

        var result = await service.ValidateNameAsync("LEGOLAS");
        Assert.False(result.Available);
        Assert.Contains("already taken", result.Reason);
    }

    [Fact]
    public async Task DeletedCharacterName_IsAvailable()
    {
        var (service, db) = CharacterServiceFactory.Create();
        db.Characters.Add(new Character
        {
            Id = Guid.NewGuid(), Name = "Boromir",
            PlayerId = Guid.NewGuid(), IsDeleted = true
        });
        await db.SaveChangesAsync();

        var result = await service.ValidateNameAsync("Boromir");
        Assert.True(result.Available);
    }
}
