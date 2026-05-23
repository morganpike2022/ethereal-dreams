namespace MMORPG.Api.Models;

public class Guild
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public string? Description { get; set; }
    public short Level { get; set; } = 1;
    public long Experience { get; set; }
    public long Gold { get; set; }
    public string? Motd { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<GuildMember> Members { get; set; } = [];
}

public class GuildMember
{
    public Guid GuildId { get; set; }
    public Guid CharacterId { get; set; }
    public string Rank { get; set; } = "recruit";
    public long ContributionXp { get; set; }
    public DateTimeOffset JoinedAt { get; set; }
    public DateTimeOffset LastActiveAt { get; set; }

    public Guild Guild { get; set; } = null!;
    public Character Character { get; set; } = null!;
}
