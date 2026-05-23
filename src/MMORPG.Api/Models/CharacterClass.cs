namespace MMORPG.Api.Models;

public class CharacterClass
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int BaseHp { get; set; }
    public int BaseMana { get; set; }
    public short BaseStrength { get; set; }
    public short BaseAgility { get; set; }
    public short BaseIntelligence { get; set; }
    public short BaseEndurance { get; set; }
    public short BaseSpirit { get; set; }
    public int HpPerLevel { get; set; }
    public int ManaPerLevel { get; set; }

    public ICollection<Character> Characters { get; set; } = [];
}
