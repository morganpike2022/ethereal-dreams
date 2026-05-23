using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MMORPG.Api.Data;
using MMORPG.Api.DTOs;
using MMORPG.Api.Models;

namespace MMORPG.Api.Services;

public class CharacterService(ApplicationDbContext db) : ICharacterService
{
    private const int MaxCharactersPerPlayer = 5;

    public async Task<IReadOnlyList<CharacterSummaryDto>> GetByPlayerAsync(Guid playerId)
    {
        return await db.Characters
            .Where(c => c.PlayerId == playerId && !c.IsDeleted)
            .Include(c => c.Class)
            .Include(c => c.Zone)
            .Select(c => ToSummary(c))
            .ToListAsync();
    }

    public async Task<CharacterSheetDto> GetSheetAsync(Guid characterId, Guid playerId)
    {
        var character = await db.Characters
            .Include(c => c.Class)
            .Include(c => c.Zone)
            .FirstOrDefaultAsync(c => c.Id == characterId && c.PlayerId == playerId && !c.IsDeleted)
            ?? throw new KeyNotFoundException("Character not found.");

        return ToSheet(character);
    }

    public async Task<CharacterSummaryDto> CreateAsync(Guid playerId, CreateCharacterRequest request)
    {
        // Global name uniqueness (case-insensitive, excludes soft-deleted)
        var nameTaken = await db.Characters
            .AnyAsync(c => !c.IsDeleted && c.Name.ToLower() == request.Name.ToLower());
        if (nameTaken)
            throw new InvalidOperationException("That name is already taken.");

        var count = await db.Characters.CountAsync(c => c.PlayerId == playerId && !c.IsDeleted);
        if (count >= MaxCharactersPerPlayer)
            throw new InvalidOperationException($"Maximum of {MaxCharactersPerPlayer} characters per account.");

        var charClass = await db.CharacterClasses.FindAsync(request.ClassId)
            ?? throw new KeyNotFoundException("Invalid class.");

        // Load starting skills for the class before opening the transaction
        var startingSkills = await db.Skills
            .Where(s => s.ClassId == request.ClassId && s.MinLevel == 1)
            .ToListAsync();

        // Load starter item for slot 0 (Novice Health Potion, id=1) if it exists
        var starterItem = await db.Items.FindAsync(1);

        // Atomic transaction: characters + character_skills + inventory
        // IsRelational() guard lets InMemory unit tests run without transaction support
        await using var tx = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync()
            : null;
        try
        {
            var character = new Character
            {
                Id           = Guid.NewGuid(),
                PlayerId     = playerId,
                ClassId      = charClass.Id,
                Name         = request.Name,
                Level        = 1,
                CurrentHp    = charClass.BaseHp,
                MaxHp        = charClass.BaseHp,
                CurrentMana  = charClass.BaseMana,
                MaxMana      = charClass.BaseMana,
                Strength     = charClass.BaseStrength,
                Agility      = charClass.BaseAgility,
                Intelligence = charClass.BaseIntelligence,
                Endurance    = charClass.BaseEndurance,
                Spirit       = charClass.BaseSpirit,
                CreatedAt    = DateTimeOffset.UtcNow,
                UpdatedAt    = DateTimeOffset.UtcNow
            };
            db.Characters.Add(character);

            // Table 2: character_skills — seed all class skills at rank 1
            foreach (var skill in startingSkills)
            {
                db.CharacterSkills.Add(new CharacterSkill
                {
                    CharacterId = character.Id,
                    SkillId     = skill.Id,
                    CurrentRank = 1
                });
            }

            // Table 3: inventory — slot 0 gets 5x Novice Health Potions if item exists
            if (starterItem is not null)
            {
                db.Inventory.Add(new InventorySlot
                {
                    Id          = Guid.NewGuid(),
                    CharacterId = character.Id,
                    ItemId      = starterItem.Id,
                    Quantity    = 5,
                    SlotIndex   = 0,
                    AcquiredAt  = DateTimeOffset.UtcNow
                });
            }

            await db.SaveChangesAsync();
            if (tx is not null) await tx.CommitAsync();

            character.Class = charClass;
            return ToSummary(character);
        }
        catch
        {
            if (tx is not null) await tx.RollbackAsync();
            throw;
        }
    }

    private static readonly Regex NameRegex =
        new(@"^[A-Za-z][A-Za-z0-9\-']{1,31}$", RegexOptions.Compiled);

    public async Task<NameValidationResponse> ValidateNameAsync(string name)
    {
        if (name.Length < 2 || name.Length > 32)
            return new NameValidationResponse(false, "Name must be between 2 and 32 characters.");

        if (!NameRegex.IsMatch(name))
        {
            if (!char.IsLetter(name[0]))
                return new NameValidationResponse(false, "Name must start with a letter.");
            return new NameValidationResponse(false, "Name may only contain letters, digits, hyphens, and apostrophes.");
        }

        var taken = await db.Characters
            .AnyAsync(c => !c.IsDeleted && c.Name.ToLower() == name.ToLower());

        return taken
            ? new NameValidationResponse(false, "That name is already taken.")
            : new NameValidationResponse(true);
    }

    public async Task DeleteAsync(Guid characterId, Guid playerId)
    {
        var character = await db.Characters
            .FirstOrDefaultAsync(c => c.Id == characterId && c.PlayerId == playerId && !c.IsDeleted)
            ?? throw new KeyNotFoundException("Character not found.");

        character.IsDeleted = true;
        character.DeleteAt = DateTimeOffset.UtcNow.AddHours(24);
        character.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
    }

    private static CharacterSummaryDto ToSummary(Character c) => new(
        c.Id, c.Name, c.Class?.DisplayName ?? string.Empty,
        c.Level, c.Gold, c.ZoneId, c.Zone?.Name,
        c.IsOnline, c.CreatedAt
    );

    private static CharacterSheetDto ToSheet(Character c) => new(
        c.Id, c.Name, c.Class?.DisplayName ?? string.Empty,
        c.Level, c.Experience, c.Gold,
        c.CurrentHp, c.MaxHp, c.CurrentMana, c.MaxMana,
        new AttributesDto(c.Strength, c.Agility, c.Intelligence, c.Endurance, c.Spirit),
        new CombatStatsDto(c.AttackPower, c.SpellPower, c.Armor, c.CritChance, c.DodgeChance, c.Haste),
        c.Zone is null ? null : new LocationDto(c.ZoneId!.Value, c.Zone.Name, c.PosX, c.PosY)
    );
}
