using MMORPG.Api.Models;
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

    // ── length rules ────────────────────────────────────────────────────────

    [Fact]
    public async Task NameTooShort_ReturnsFalse()
    {
        var (service, _) = CharacterServiceFactory.Create();
        var result = await service.ValidateNameAsync("A");
        Assert.False(result.Available);
        Assert.Contains("2 and 32", result.Reason);
    }

    [Fact]
    public async Task NameTooLong_ReturnsFalse()
    {
        var (service, _) = CharacterServiceFactory.Create();
        var result = await service.ValidateNameAsync(new string('A', 33));
        Assert.False(result.Available);
        Assert.Contains("2 and 32", result.Reason);
    }

    [Fact]
    public async Task NameExactly32Chars_ReturnsAvailable()
    {
        var (service, _) = CharacterServiceFactory.Create();
        var result = await service.ValidateNameAsync("A" + new string('b', 31));
        Assert.True(result.Available);
    }

    // ── character set rules ─────────────────────────────────────────────────

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

    // ── must start with letter ───────────────────────────────────────────────

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
