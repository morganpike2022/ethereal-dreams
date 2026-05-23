namespace MMORPG.Api.Models;

public class InventorySlot
{
    public Guid Id { get; set; }
    public Guid CharacterId { get; set; }
    public int ItemId { get; set; }
    public int Quantity { get; set; } = 1;
    public short SlotIndex { get; set; }
    public DateTimeOffset AcquiredAt { get; set; }

    public Character Character { get; set; } = null!;
    public Item Item { get; set; } = null!;
}

public class CharacterEquipment
{
    public Guid CharacterId { get; set; }
    public string Slot { get; set; } = string.Empty;
    public int ItemId { get; set; }
    public DateTimeOffset EquippedAt { get; set; }

    public Character Character { get; set; } = null!;
    public Item Item { get; set; } = null!;
}
