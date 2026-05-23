using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MMORPG.Api.DTOs;
using MMORPG.Api.Models;
using MMORPG.Api.Tests.Helpers;

namespace MMORPG.Api.Tests.Characters;

/// <summary>
/// Unit tests for CharacterService.GetSelectScreenAsync using InMemory provider.
/// Each test creates its own isolated database.
/// </summary>
public class CharacterSelectTests
{
    // ── seed helpers ──────────────────────────────────────────────────────────

    private static CharacterClass Warrior() => new()
    {
        Id = 1, Name = "warrior", DisplayName = "Warrior",
        BaseHp = 500, BaseMana = 100,
        BaseStrength = 20, BaseAgility = 10, BaseIntelligence = 5,
        BaseEndurance = 18, BaseSpirit = 8,
        HpPerLevel = 50, ManaPerLevel = 10
    };

    private static CharacterClass Mage() => new()
    {
        Id = 2, Name = "mage", DisplayName = "Mage",
        BaseHp = 200, BaseMana = 400,
        BaseStrength = 5, BaseAgility = 10, BaseIntelligence = 25,
        BaseEndurance = 7, BaseSpirit = 15,
        HpPerLevel = 20, ManaPerLevel = 50
    };

    private static async Task<(MMORPG.Api.Services.CharacterService svc, MMORPG.Api.Data.ApplicationDbContext db)>
        SetupAsync(Action<MMORPG.Api.Data.ApplicationDbContext>? seed = null)
    {
        var (svc, db) = CharacterServiceFactory.Create();
        seed?.Invoke(db);
        if (db.ChangeTracker.HasChanges())
            await db.SaveChangesAsync();
        return (svc, db);
    }

    // ── returns empty list for player with no characters ──────────────────────

    [Fact]
    public async Task GetSelectScreen_EmptyForNewPlayer()
    {
        var (svc, _) = await SetupAsync(db => db.CharacterClasses.Add(Warrior()));
        var result = await svc.GetSelectScreenAsync(Guid.NewGuid());
        Assert.Empty(result);
    }

    // ── returns only the calling player's characters ──────────────────────────

    [Fact]
    public async Task GetSelectScreen_ReturnsOnlyCallingPlayersChars()
    {
        var (svc, _) = await SetupAsync(db => db.CharacterClasses.Add(Warrior()));
        var player1 = Guid.NewGuid();
        var player2 = Guid.NewGuid();

        await svc.CreateAsync(player1, new CreateCharacterRequest("HeroA", 1, null));
        await svc.CreateAsync(player1, new CreateCharacterRequest("HeroB", 1, null));
        await svc.CreateAsync(player2, new CreateCharacterRequest("HeroC", 1, null));

        var p1Result = await svc.GetSelectScreenAsync(player1);
        var p2Result = await svc.GetSelectScreenAsync(player2);

        Assert.Equal(2, p1Result.Count);
        Assert.Single(p2Result);
        Assert.All(p1Result, c => Assert.Contains(c.Name, new[] { "HeroA", "HeroB" }));
        Assert.Equal("HeroC", p2Result[0].Name);
    }

    // ── soft-deleted characters are excluded ──────────────────────────────────

    [Fact]
    public async Task GetSelectScreen_ExcludesSoftDeletedChars()
    {
        var (svc, _) = await SetupAsync(db => db.CharacterClasses.Add(Warrior()));
        var playerId = Guid.NewGuid();

        var kept    = await svc.CreateAsync(playerId, new CreateCharacterRequest("Keeper", 1, null));
        var deleted = await svc.CreateAsync(playerId, new CreateCharacterRequest("Goner",  1, null));
        await svc.DeleteAsync(deleted.Id, playerId);

        var result = await svc.GetSelectScreenAsync(playerId);

        Assert.Single(result);
        Assert.Equal("Keeper", result[0].Name);
        Assert.DoesNotContain(result, c => c.Id == deleted.Id);
    }

    // ── ordered by createdAt ascending ───────────────────────────────────────

    [Fact]
    public async Task GetSelectScreen_OrderedByCreatedAtAscending()
    {
        var (svc, _) = await SetupAsync(db => db.CharacterClasses.Add(Warrior()));
        var playerId = Guid.NewGuid();

        await svc.CreateAsync(playerId, new CreateCharacterRequest("First",  1, null));
        await svc.CreateAsync(playerId, new CreateCharacterRequest("Second", 1, null));
        await svc.CreateAsync(playerId, new CreateCharacterRequest("Third",  1, null));

        var result = await svc.GetSelectScreenAsync(playerId);

        Assert.Equal(3, result.Count);
        // Each should be created at or after the previous one
        for (int i = 1; i < result.Count; i++)
            Assert.True(result[i].CreatedAt >= result[i - 1].CreatedAt,
                $"Expected ascending order but [{i}] was before [{i-1}]");
    }

    // ── stats match the class template ───────────────────────────────────────

    [Fact]
    public async Task GetSelectScreen_StatsMatchWarriorClass()
    {
        var warrior  = Warrior();
        var (svc, _) = await SetupAsync(db => db.CharacterClasses.Add(warrior));
        var playerId = Guid.NewGuid();

        await svc.CreateAsync(playerId, new CreateCharacterRequest("Bolvar", 1, null));
        var result = await svc.GetSelectScreenAsync(playerId);

        var c = result[0];
        Assert.Equal(warrior.BaseHp,           c.MaxHp);
        Assert.Equal(warrior.BaseHp,           c.CurrentHp);
        Assert.Equal(warrior.BaseMana,         c.MaxMana);
        Assert.Equal(warrior.BaseMana,         c.CurrentMana);
        Assert.Equal(warrior.BaseStrength,     c.Attributes.Strength);
        Assert.Equal(warrior.BaseAgility,      c.Attributes.Agility);
        Assert.Equal(warrior.BaseIntelligence, c.Attributes.Intelligence);
        Assert.Equal(warrior.BaseEndurance,    c.Attributes.Endurance);
        Assert.Equal(warrior.BaseSpirit,       c.Attributes.Spirit);
    }

    [Fact]
    public async Task GetSelectScreen_StatsMatchMageClass_DifferentFromWarrior()
    {
        var warrior = Warrior();
        var mage    = Mage();
        var (svc, _) = await SetupAsync(db => db.CharacterClasses.AddRange(warrior, mage));
        var playerId = Guid.NewGuid();

        await svc.CreateAsync(playerId, new CreateCharacterRequest("Jaina", 2, null));
        var result = await svc.GetSelectScreenAsync(playerId);

        var c = result[0];
        Assert.Equal(mage.BaseHp,   c.MaxHp);
        Assert.Equal(mage.BaseMana, c.MaxMana);
        Assert.NotEqual(warrior.BaseHp,   c.MaxHp);
        Assert.NotEqual(warrior.BaseMana, c.MaxMana);
    }

    // ── class name is included ────────────────────────────────────────────────

    [Fact]
    public async Task GetSelectScreen_IncludesClassName()
    {
        var (svc, _) = await SetupAsync(db => db.CharacterClasses.Add(Warrior()));
        var playerId = Guid.NewGuid();

        await svc.CreateAsync(playerId, new CreateCharacterRequest("Garrosh", 1, null));
        var result = await svc.GetSelectScreenAsync(playerId);

        Assert.Equal("Warrior", result[0].ClassName);
    }

    // ── appearance data roundtrips ────────────────────────────────────────────

    [Fact]
    public async Task GetSelectScreen_AppearanceDataDefaults_ToEmptyJson()
    {
        var (svc, _) = await SetupAsync(db => db.CharacterClasses.Add(Warrior()));
        var playerId = Guid.NewGuid();

        await svc.CreateAsync(playerId, new CreateCharacterRequest("NoAppear", 1, null));
        var result = await svc.GetSelectScreenAsync(playerId);

        Assert.Equal("{}", result[0].AppearanceData);
    }

    [Fact]
    public async Task GetSelectScreen_AppearanceData_Roundtrips()
    {
        var (svc, _) = await SetupAsync(db => db.CharacterClasses.Add(Warrior()));
        var playerId = Guid.NewGuid();
        var appearance = new AppearanceData("light", "long", "auburn", "angular");

        await svc.CreateAsync(playerId,
            new CreateCharacterRequest("Stylish", 1, appearance));
        var result = await svc.GetSelectScreenAsync(playerId);

        // Deserialize back and compare fields
        var stored = JsonSerializer.Deserialize<AppearanceData>(result[0].AppearanceData,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(stored);
        Assert.Equal("light",   stored!.SkinTone);
        Assert.Equal("long",    stored.HairStyle);
        Assert.Equal("auburn",  stored.HairColor);
        Assert.Equal("angular", stored.FaceType);
    }

    // ── multiple classes — select screen returns correct class for each ────────

    [Fact]
    public async Task GetSelectScreen_MultipleCharsWithDifferentClasses()
    {
        var (svc, _) = await SetupAsync(db =>
            db.CharacterClasses.AddRange(Warrior(), Mage()));
        var playerId = Guid.NewGuid();

        await svc.CreateAsync(playerId, new CreateCharacterRequest("Tank",  1, null));
        await svc.CreateAsync(playerId, new CreateCharacterRequest("Caster", 2, null));

        var result = await svc.GetSelectScreenAsync(playerId);

        Assert.Equal(2, result.Count);
        var tank   = result.First(c => c.Name == "Tank");
        var caster = result.First(c => c.Name == "Caster");
        Assert.Equal("Warrior", tank.ClassName);
        Assert.Equal("Mage",    caster.ClassName);
        Assert.NotEqual(tank.MaxHp,   caster.MaxHp);
        Assert.NotEqual(tank.MaxMana, caster.MaxMana);
    }
}
