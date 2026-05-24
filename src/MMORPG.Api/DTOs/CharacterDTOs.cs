using System.ComponentModel.DataAnnotations;

namespace MMORPG.Api.DTOs;

public record CreateCharacterRequest(
    [Required, MinLength(3), MaxLength(20)] string Name,
    [Required] int ClassId,
    AppearanceData? Appearance
);

public record AppearanceData(
    string SkinTone,
    string HairStyle,
    string HairColor,
    string FaceType
);

public record CharacterSelectDto(
    Guid Id,
    string Name,
    string ClassName,
    short Level,
    long Gold,
    int CurrentHp,
    int MaxHp,
    int CurrentMana,
    int MaxMana,
    AttributesDto Attributes,
    int? ZoneId,
    string? ZoneName,
    bool IsOnline,
    string AppearanceData,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public record CharacterSummaryDto(
    Guid Id,
    string Name,
    string ClassName,
    short Level,
    long Gold,
    int? ZoneId,
    string? ZoneName,
    bool IsOnline,
    DateTimeOffset CreatedAt
);

public record CharacterSheetDto(
    Guid Id,
    string Name,
    string ClassName,
    short Level,
    long Experience,
    long Gold,
    int CurrentHp,
    int MaxHp,
    int CurrentMana,
    int MaxMana,
    AttributesDto Attributes,
    CombatStatsDto CombatStats,
    LocationDto? Location
);

public record AttributesDto(
    short Strength,
    short Agility,
    short Intelligence,
    short Endurance,
    short Spirit
);

public record CombatStatsDto(
    int AttackPower,
    int SpellPower,
    int Armor,
    decimal CritChance,
    decimal DodgeChance,
    decimal Haste
);

public record LocationDto(
    int ZoneId,
    string ZoneName,
    decimal PosX,
    decimal PosY
);
