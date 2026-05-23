namespace MMORPG.Api.Models;

public class Zone
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ZoneType { get; set; } = "open_world";
    public short MinLevel { get; set; } = 1;
    public short MaxLevel { get; set; } = 60;
    public bool IsPvp { get; set; }
    public bool IsInstanced { get; set; }
    public int? MaxPlayers { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<Character> Characters { get; set; } = [];
}
