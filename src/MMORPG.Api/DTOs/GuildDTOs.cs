using System.ComponentModel.DataAnnotations;

namespace MMORPG.Api.DTOs;

public record CreateGuildRequest(
    [Required, MinLength(3), MaxLength(64)] string Name,
    [Required, MinLength(2), MaxLength(6)] string Tag,
    string? Description
);

public record GuildDto(
    Guid Id,
    string Name,
    string Tag,
    string? Description,
    short Level,
    string? Motd,
    int MemberCount,
    DateTimeOffset CreatedAt
);

public record GuildMemberDto(
    Guid CharacterId,
    string CharacterName,
    string ClassName,
    short CharacterLevel,
    string Rank,
    long ContributionXp,
    DateTimeOffset JoinedAt,
    DateTimeOffset LastActiveAt
);
