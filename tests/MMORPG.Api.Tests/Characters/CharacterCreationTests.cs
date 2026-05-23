using Microsoft.EntityFrameworkCore;
using MMORPG.Api.DTOs;
using MMORPG.Api.Models;
using MMORPG.Api.Tests.Helpers;

namespace MMORPG.Api.Tests.Characters;

/// <summary>
/// Unit tests for CharacterService.CreateAsync using the InMemory provider.
/// Each test seeds its own isolated database so there are no ordering dependencies.
/// </summary>
public class CharacterCreationTests
{
    // ── helpers ───────────────────────────────────────────────────────────────

    private static CharacterClass WarriorClass() => new()
    {
        Id = 1, Name = "warrior", DisplayName = "Warrior",
        BaseHp = 500, BaseMana = 100,
        BaseStrength = 20, BaseAgility = 10, BaseIntelligence = 5,
        BaseEndurance = 18, BaseSpirit = 8,
        HpPerLevel = 50, ManaPerLevel = 10
    };

    private static CharacterClass MageClass() => new()
    {
        Id = 2, Name = "mage", DisplayName = "Mage",
        BaseHp = 200, BaseMana = 400,
        BaseStrength = 5, BaseAgility = 10, BaseIntelligence = 25,
        BaseEndurance = 7, BaseSpirit = 15,
        HpPerLevel = 20, ManaPerLevel = 50
    };

    private static Skill WarriorSkill(int id = 101) => new()
    {
        Id = id, ClassId = 1, Name = "Shield Bash",
        SkillType = "active", MinLevel = 1, MaxRank = 5
    };

    private static Item StarterPotion() => new()
    {
        Id = 1, Name = "Novice Health Potion",
        ItemType = "consumable", Rarity = "common",
        IsStackable = true, MaxStack = 99, Weight = 0.1m
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

    // ── happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_HappyPath_Returns201Dto()
    {
        var (svc, _) = await SetupAsync(db =>
        {
            db.CharacterClasses.Add(WarriorClass());
            db.Players.Add(new Player { Id = Guid.NewGuid(), Username = "u1", Email = "u1@x.com", PasswordHash = "x" });
        });
        var playerId = Guid.NewGuid();

        var result = await svc.CreateAsync(playerId, new CreateCharacterRequest("Arthas", 1, null));

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("Arthas", result.Name);
        Assert.Equal("Warrior", result.ClassName);
        Assert.Equal(1, result.Level);
    }

    // ── stats are seeded from the selected class ──────────────────────────────

    [Fact]
    public async Task Create_WarriorClass_SeedsWarriorStats()
    {
        var warrior = WarriorClass();
        var (svc, db) = await SetupAsync(db => db.CharacterClasses.Add(warrior));
        var playerId = Guid.NewGuid();

        var result = await svc.CreateAsync(playerId, new CreateCharacterRequest("Bolvar", 1, null));

        var character = await db.Characters.FirstAsync(c => c.Id == result.Id);
        Assert.Equal(warrior.BaseHp,           character.MaxHp);
        Assert.Equal(warrior.BaseHp,           character.CurrentHp);
        Assert.Equal(warrior.BaseMana,         character.MaxMana);
        Assert.Equal(warrior.BaseStrength,     character.Strength);
        Assert.Equal(warrior.BaseAgility,      character.Agility);
        Assert.Equal(warrior.BaseIntelligence, character.Intelligence);
        Assert.Equal(warrior.BaseEndurance,    character.Endurance);
        Assert.Equal(warrior.BaseSpirit,       character.Spirit);
    }

    [Fact]
    public async Task Create_MageClass_SeedsMageStats_NotWarriorStats()
    {
        var warrior = WarriorClass();
        var mage    = MageClass();
        var (svc, db) = await SetupAsync(db =>
        {
            db.CharacterClasses.AddRange(warrior, mage);
        });
        var playerId = Guid.NewGuid();

        var result = await svc.CreateAsync(playerId, new CreateCharacterRequest("Jaina", 2, null));

        var character = await db.Characters.FirstAsync(c => c.Id == result.Id);
        Assert.Equal(mage.BaseHp,           character.MaxHp);
        Assert.Equal(mage.BaseMana,         character.MaxMana);
        Assert.Equal(mage.BaseIntelligence, character.Intelligence);
        Assert.NotEqual(warrior.BaseHp,     character.MaxHp);
    }

    // ── starting skills are seeded for the class ──────────────────────────────

    [Fact]
    public async Task Create_SeedsStartingSkillForClass()
    {
        var skill = WarriorSkill();
        var (svc, db) = await SetupAsync(db =>
        {
            db.CharacterClasses.Add(WarriorClass());
            db.Skills.Add(skill);
        });
        var playerId = Guid.NewGuid();

        var result = await svc.CreateAsync(playerId, new CreateCharacterRequest("Garrosh", 1, null));

        var characterSkills = await db.CharacterSkills
            .Where(cs => cs.CharacterId == result.Id)
            .ToListAsync();
        Assert.Single(characterSkills);
        Assert.Equal(skill.Id, characterSkills[0].SkillId);
        Assert.Equal(1, characterSkills[0].CurrentRank);
    }

    [Fact]
    public async Task Create_SkillsForOtherClass_NotSeeded()
    {
        var (svc, db) = await SetupAsync(db =>
        {
            db.CharacterClasses.AddRange(WarriorClass(), MageClass());
            db.Skills.Add(new Skill
            {
                Id = 200, ClassId = 2, Name = "Fireball",
                SkillType = "active", MinLevel = 1, MaxRank = 5
            });
        });
        var playerId = Guid.NewGuid();

        // Creating a Warrior should NOT receive the Mage Fireball skill
        var result = await svc.CreateAsync(playerId, new CreateCharacterRequest("Thrall", 1, null));

        var characterSkills = await db.CharacterSkills
            .Where(cs => cs.CharacterId == result.Id)
            .ToListAsync();
        Assert.Empty(characterSkills);
    }

    [Fact]
    public async Task Create_HighLevelSkills_NotSeeded()
    {
        var (svc, db) = await SetupAsync(db =>
        {
            db.CharacterClasses.Add(WarriorClass());
            db.Skills.AddRange(
                WarriorSkill(id: 101),                                            // min_level = 1 — seeded
                new Skill { Id = 102, ClassId = 1, Name = "Whirlwind",
                            SkillType = "active", MinLevel = 20, MaxRank = 5 }   // min_level = 20 — NOT seeded
            );
        });
        var playerId = Guid.NewGuid();

        var result = await svc.CreateAsync(playerId, new CreateCharacterRequest("Varian", 1, null));

        var characterSkills = await db.CharacterSkills
            .Where(cs => cs.CharacterId == result.Id)
            .ToListAsync();
        Assert.Single(characterSkills);
        Assert.Equal(101, characterSkills[0].SkillId);
    }

    // ── starter inventory ─────────────────────────────────────────────────────

    [Fact]
    public async Task Create_WithStarterItem_SeedsInventorySlot0()
    {
        var (svc, db) = await SetupAsync(db =>
        {
            db.CharacterClasses.Add(WarriorClass());
            db.Items.Add(StarterPotion());
        });
        var playerId = Guid.NewGuid();

        var result = await svc.CreateAsync(playerId, new CreateCharacterRequest("Sylvanas", 1, null));

        var slot = await db.Inventory.FirstOrDefaultAsync(i => i.CharacterId == result.Id);
        Assert.NotNull(slot);
        Assert.Equal(0, slot!.SlotIndex);
        Assert.Equal(5, slot.Quantity);
        Assert.Equal(1, slot.ItemId);
    }

    [Fact]
    public async Task Create_NoStarterItem_InventoryIsEmpty()
    {
        var (svc, db) = await SetupAsync(db => db.CharacterClasses.Add(WarriorClass()));
        var playerId = Guid.NewGuid();

        var result = await svc.CreateAsync(playerId, new CreateCharacterRequest("Uther", 1, null));

        var inventory = await db.Inventory.Where(i => i.CharacterId == result.Id).ToListAsync();
        Assert.Empty(inventory);
    }

    // ── name uniqueness ───────────────────────────────────────────────────────

    [Fact]
    public async Task Create_DuplicateName_ThrowsInvalidOperation()
    {
        var (svc, _) = await SetupAsync(db => db.CharacterClasses.Add(WarriorClass()));
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();

        await svc.CreateAsync(p1, new CreateCharacterRequest("Anduin", 1, null));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CreateAsync(p2, new CreateCharacterRequest("Anduin", 1, null)));
    }

    [Fact]
    public async Task Create_DuplicateName_CaseInsensitive_ThrowsInvalidOperation()
    {
        var (svc, _) = await SetupAsync(db => db.CharacterClasses.Add(WarriorClass()));
        var playerId = Guid.NewGuid();

        await svc.CreateAsync(playerId, new CreateCharacterRequest("Lothar", 1, null));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CreateAsync(Guid.NewGuid(), new CreateCharacterRequest("LOTHAR", 1, null)));
    }

    [Fact]
    public async Task Create_SoftDeletedName_IsAvailableAgain()
    {
        var (svc, db) = await SetupAsync(db => db.CharacterClasses.Add(WarriorClass()));
        var playerId = Guid.NewGuid();

        var first = await svc.CreateAsync(playerId, new CreateCharacterRequest("Medivh", 1, null));
        await svc.DeleteAsync(first.Id, playerId);

        // Same name should now be allowed
        var second = await svc.CreateAsync(Guid.NewGuid(), new CreateCharacterRequest("Medivh", 1, null));
        Assert.NotEqual(first.Id, second.Id);
    }

    // ── character slot limit ──────────────────────────────────────────────────

    [Fact]
    public async Task Create_ExceedsMaxCharacters_ThrowsInvalidOperation()
    {
        var (svc, _) = await SetupAsync(db => db.CharacterClasses.Add(WarriorClass()));
        var playerId = Guid.NewGuid();

        for (int i = 0; i < 5; i++)
            await svc.CreateAsync(playerId, new CreateCharacterRequest($"Hero{i:D2}", 1, null));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CreateAsync(playerId, new CreateCharacterRequest("Hero06", 1, null)));
        Assert.Contains("Maximum", ex.Message);
    }

    [Fact]
    public async Task Create_At5Characters_Succeeds()
    {
        var (svc, _) = await SetupAsync(db => db.CharacterClasses.Add(WarriorClass()));
        var playerId = Guid.NewGuid();

        for (int i = 0; i < 4; i++)
            await svc.CreateAsync(playerId, new CreateCharacterRequest($"Alt{i:D2}", 1, null));

        // 5th should be fine
        var fifth = await svc.CreateAsync(playerId, new CreateCharacterRequest("Alt04", 1, null));
        Assert.NotEqual(Guid.Empty, fifth.Id);
    }

    // ── invalid class ────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_InvalidClassId_ThrowsKeyNotFound()
    {
        var (svc, _) = await SetupAsync(db => db.CharacterClasses.Add(WarriorClass()));

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            svc.CreateAsync(Guid.NewGuid(), new CreateCharacterRequest("Orphan", 99, null)));
    }
}
