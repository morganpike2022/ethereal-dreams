namespace MMORPG.Api.Models;

public class ChatMessage
{
    public long Id { get; set; }
    public string Channel { get; set; } = string.Empty;
    public Guid SenderId { get; set; }
    public Guid? RecipientId { get; set; }
    public int? ZoneId { get; set; }
    public Guid? GuildId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset SentAt { get; set; }

    public Character Sender { get; set; } = null!;
}
