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
        var count = await db.Characters.CountAsync(c => c.PlayerId == playerId && !c.IsDeleted);
        if (count >= MaxCharactersPerPlayer)
            throw new InvalidOperationException($"Maximum of {MaxCharactersPerPlayer} characters per account.");

        var charClass = await db.CharacterClasses.FindAsync(request.ClassId)
            ?? throw new KeyNotFoundException("Invalid class.");

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
        await db.SaveChangesAsync();

        character.Class = charClass;
        return ToSummary(character);
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
