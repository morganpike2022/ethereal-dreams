namespace MMORPG.Api.Models;

public class Skill
{
    public int Id { get; set; }
    public int? ClassId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string SkillType { get; set; } = string.Empty;
    public int ManaCost { get; set; }
    public decimal CooldownSecs { get; set; }
    public short MinLevel { get; set; } = 1;
    public short MaxRank { get; set; } = 1;
    public int? ParentSkillId { get; set; }
    public int? DamageBase { get; set; }
    public decimal? DamageScaling { get; set; }
    public int? HealBase { get; set; }
    public string EffectData { get; set; } = "{}";

    public CharacterClass? Class { get; set; }
    public ICollection<CharacterSkill> CharacterSkills { get; set; } = [];
}

public class CharacterSkill
{
    public Guid CharacterId { get; set; }
    public int SkillId { get; set; }
    public short CurrentRank { get; set; } = 1;
    public bool IsOnCooldown { get; set; }
    public DateTimeOffset? CooldownEnds { get; set; }

    public Character Character { get; set; } = null!;
    public Skill Skill { get; set; } = null!;
}
