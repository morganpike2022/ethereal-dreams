using System.Text.Json;

namespace MMORPG.Api.Models;

public class Achievement
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = string.Empty;
    public short Points { get; set; } = 10;
    public bool IsAccountWide { get; set; }
    public string? IconUrl { get; set; }
    public JsonDocument Criteria { get; set; } = JsonDocument.Parse("{}");
    public string? RewardTitle { get; set; }
    public int? RewardItemId { get; set; }
}

public class CharacterAchievement
{
    public Guid CharacterId { get; set; }
    public int AchievementId { get; set; }
    public JsonDocument Progress { get; set; } = JsonDocument.Parse("{}");
    public DateTimeOffset? EarnedAt { get; set; }

    public Character Character { get; set; } = null!;
    public Achievement Achievement { get; set; } = null!;
}
