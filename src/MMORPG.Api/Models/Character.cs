namespace MMORPG.Api.Models;

public class Character
{
    public Guid Id { get; set; }
    public Guid PlayerId { get; set; }
    public int ClassId { get; set; }
    public string Name { get; set; } = string.Empty;
    public short Level { get; set; } = 1;
    public long Experience { get; set; }
    public long Gold { get; set; }

    public int CurrentHp { get; set; }
    public int MaxHp { get; set; }
    public int CurrentMana { get; set; }
    public int MaxMana { get; set; }

    public short Strength { get; set; }
    public short Agility { get; set; }
    public short Intelligence { get; set; }
    public short Endurance { get; set; }
    public short Spirit { get; set; }

    public int AttackPower { get; set; }
    public int SpellPower { get; set; }
    public int Armor { get; set; }
    public decimal CritChance { get; set; } = 5.00m;
    public decimal DodgeChance { get; set; } = 5.00m;
    public decimal Haste { get; set; }

    public int? ZoneId { get; set; }
    public decimal PosX { get; set; }
    public decimal PosY { get; set; }

    public string AppearanceData { get; set; } = "{}";
    public bool IsOnline { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeleteAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Player Player { get; set; } = null!;
    public CharacterClass Class { get; set; } = null!;
    public Zone? Zone { get; set; }
    public ICollection<InventorySlot> Inventory { get; set; } = [];
    public ICollection<CharacterEquipment> Equipment { get; set; } = [];
    public ICollection<CharacterSkill> Skills { get; set; } = [];
    public ICollection<CharacterQuest> Quests { get; set; } = [];
    public ICollection<CharacterAchievement> Achievements { get; set; } = [];
}
