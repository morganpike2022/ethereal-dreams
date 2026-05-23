namespace MMORPG.Api.Models;

public class Item
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ItemType { get; set; } = string.Empty;
    public string Rarity { get; set; } = "common";
    public short RequiredLevel { get; set; } = 1;
    public string? RequiredClass { get; set; }
    public bool IsStackable { get; set; }
    public int MaxStack { get; set; } = 1;
    public decimal Weight { get; set; }
    public int SellPrice { get; set; }
    public int BuyPrice { get; set; }
    public string? IconUrl { get; set; }
    public short BonusStrength { get; set; }
    public short BonusAgility { get; set; }
    public short BonusIntelligence { get; set; }
    public short BonusEndurance { get; set; }
    public short BonusSpirit { get; set; }
    public int BonusHp { get; set; }
    public int BonusMana { get; set; }
    public int BonusAttackPower { get; set; }
    public int BonusSpellPower { get; set; }
    public int BonusArmor { get; set; }
    public decimal BonusCrit { get; set; }
    public string? EquipmentSlot { get; set; }
}
