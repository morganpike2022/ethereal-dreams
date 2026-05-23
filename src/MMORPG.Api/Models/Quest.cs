using System.Text.Json;

namespace MMORPG.Api.Models;

public class Quest
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string QuestType { get; set; } = string.Empty;
    public short MinLevel { get; set; } = 1;
    public short? MaxLevel { get; set; }
    public string? RequiredClass { get; set; }
    public int? PrerequisiteQuestId { get; set; }
    public int? ZoneId { get; set; }
    public bool IsDaily { get; set; }
    public bool IsWeekly { get; set; }
    public bool IsRepeatable { get; set; }
    public int XpReward { get; set; }
    public int GoldReward { get; set; }
    public JsonDocument Objectives { get; set; } = JsonDocument.Parse("[]");
    public JsonDocument RewardItems { get; set; } = JsonDocument.Parse("[]");
    public DateTimeOffset CreatedAt { get; set; }
}

public class CharacterQuest
{
    public Guid Id { get; set; }
    public Guid CharacterId { get; set; }
    public int QuestId { get; set; }
    public string Status { get; set; } = "in_progress";
    public JsonDocument Progress { get; set; } = JsonDocument.Parse("{}");
    public DateTimeOffset AcceptedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public Character Character { get; set; } = null!;
    public Quest Quest { get; set; } = null!;
}
