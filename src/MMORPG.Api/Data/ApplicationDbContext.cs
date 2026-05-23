using Microsoft.EntityFrameworkCore;
using MMORPG.Api.Models;

namespace MMORPG.Api.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Player> Players => Set<Player>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Character> Characters => Set<Character>();
    public DbSet<CharacterClass> CharacterClasses => Set<CharacterClass>();
    public DbSet<Zone> Zones => Set<Zone>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<InventorySlot> Inventory => Set<InventorySlot>();
    public DbSet<CharacterEquipment> CharacterEquipment => Set<CharacterEquipment>();
    public DbSet<Quest> Quests => Set<Quest>();
    public DbSet<CharacterQuest> CharacterQuests => Set<CharacterQuest>();
    public DbSet<Guild> Guilds => Set<Guild>();
    public DbSet<GuildMember> GuildMembers => Set<GuildMember>();
    public DbSet<AuctionListing> AuctionListings => Set<AuctionListing>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<Achievement> Achievements => Set<Achievement>();
    public DbSet<CharacterAchievement> CharacterAchievements => Set<CharacterAchievement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        modelBuilder.HasPostgresExtension("uuid-ossp");
        modelBuilder.HasPostgresExtension("pgcrypto");
    }
}
