using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MMORPG.Api.Configuration;
using MMORPG.Api.Data;
using MMORPG.Api.DTOs;
using MMORPG.Api.Models;

namespace MMORPG.Api.Services;

public class CharacterService(
    ApplicationDbContext db,
    ICacheService cache,
    IOptions<NameValidationOptions> nameOpts) : ICharacterService
{
    private const int MaxCharactersPerPlayer = 5;

    private static readonly TimeSpan SelectScreenTtl   = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan NameValidationTtl = TimeSpan.FromSeconds(10);

    // Letters only; each hyphen/apostrophe must be immediately followed by a letter
    // so consecutive specials and trailing specials are rejected automatically.
    private static readonly Regex NameRegex =
        new(@"^[A-Za-z]([A-Za-z]|[-'][A-Za-z])*$", RegexOptions.Compiled);

    public async Task<IReadOnlyList<CharacterSummaryDto>> GetByPlayerAsync(Guid playerId)
    {
        return await db.Characters
            .Where(c => c.PlayerId == playerId && !c.IsDeleted)
            .Include(c => c.Class)
            .Include(c => c.Zone)
            .Select(c => ToSummary(c))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<CharacterSelectDto>> GetSelectScreenAsync(Guid playerId, bool forceRefresh = false)
    {
        var key = CacheKeys.CharacterSelect(playerId);

        if (!forceRefresh)
        {
            var cached = await cache.GetAsync<List<CharacterSelectDto>>(key);
            if (cached is not null)
                return cached;
        }

        var result = await db.Characters
            .Where(c => c.PlayerId == playerId && !c.IsDeleted)
            .Include(c => c.Class)
            .Include(c => c.Zone)
            .OrderBy(c => c.CreatedAt)
            .Select(c => ToSelectDto(c))
            .ToListAsync();

        await cache.SetAsync(key, result, SelectScreenTtl);
        return result;
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
        // Blocklist guard (duplicates the validate-name check so direct POSTs are also safe)
        if (IsReserved(request.Name))
            throw new InvalidOperationException("That name is reserved.");

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

        var startingSkills = await db.Skills
            .Where(s => s.ClassId == request.ClassId && s.MinLevel == 1)
            .ToListAsync();

        var starterItem = await db.Items.FindAsync(1);

        await using var tx = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync()
            : null;
        try
        {
            var character = new Character
            {
                Id             = Guid.NewGuid(),
                PlayerId       = playerId,
                ClassId        = charClass.Id,
                Name           = request.Name,
                AppearanceData = request.Appearance is not null
                    ? JsonSerializer.Serialize(request.Appearance)
                    : "{}",
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

            foreach (var skill in startingSkills)
            {
                db.CharacterSkills.Add(new CharacterSkill
                {
                    CharacterId = character.Id,
                    SkillId     = skill.Id,
                    CurrentRank = 1
                });
            }

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

            await cache.RemoveAsync(CacheKeys.CharacterSelect(playerId));
            await cache.RemoveAsync(CacheKeys.NameAvailable(request.Name));

            character.Class = charClass;
            return ToSummary(character);
        }
        catch
        {
            if (tx is not null) await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<NameValidationResponse> ValidateNameAsync(string name)
    {
        // Length: 3–20 characters
        if (name.Length < 3 || name.Length > 20)
            return new NameValidationResponse(false, "Name must be between 3 and 20 characters.");

        // Format: letters, single hyphens/apostrophes (no consecutive specials)
        if (!NameRegex.IsMatch(name))
        {
            if (!char.IsLetter(name[0]))
                return new NameValidationResponse(false, "Name must start with a letter.");
            return new NameValidationResponse(false,
                "Name may only contain letters, hyphens, and apostrophes (no consecutive specials).");
        }

        // Blocklist: reserved/profanity names
        if (IsReserved(name))
            return new NameValidationResponse(false, "That name is reserved.");

        // Cache: spare the DB hit for recently-checked names (10 s TTL)
        var key = CacheKeys.NameAvailable(name);
        var cached = await cache.GetAsync<NameValidationResponse>(key);
        if (cached is not null)
            return cached;

        // DB uniqueness (case-insensitive)
        var taken = await db.Characters
            .AnyAsync(c => !c.IsDeleted && c.Name.ToLower() == name.ToLower());

        var response = taken
            ? new NameValidationResponse(false, "That name is already taken.")
            : new NameValidationResponse(true);

        await cache.SetAsync(key, response, NameValidationTtl);
        return response;
    }

    public async Task DeleteAsync(Guid characterId, Guid playerId)
    {
        var character = await db.Characters
            .FirstOrDefaultAsync(c => c.Id == characterId && c.PlayerId == playerId && !c.IsDeleted)
            ?? throw new KeyNotFoundException("Character not found.");

        character.IsDeleted = true;
        character.DeleteAt  = DateTimeOffset.UtcNow.AddHours(24);
        character.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        await cache.RemoveAsync(CacheKeys.CharacterSelect(playerId));
    }

    private bool IsReserved(string name) =>
        nameOpts.Value.ReservedNames.Any(r =>
            r.Equals(name, StringComparison.OrdinalIgnoreCase));

    private static CharacterSelectDto ToSelectDto(Character c) => new(
        c.Id, c.Name, c.Class?.DisplayName ?? string.Empty,
        c.Level, c.Gold,
        c.CurrentHp, c.MaxHp, c.CurrentMana, c.MaxMana,
        new AttributesDto(c.Strength, c.Agility, c.Intelligence, c.Endurance, c.Spirit),
        c.ZoneId, c.Zone?.Name,
        c.IsOnline, c.AppearanceData,
        c.CreatedAt, c.UpdatedAt
    );

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
