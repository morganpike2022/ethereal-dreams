using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MMORPG.Api.Models;

namespace MMORPG.Api.Data.Configurations;

public class CharacterAchievementConfiguration : IEntityTypeConfiguration<CharacterAchievement>
{
    public void Configure(EntityTypeBuilder<CharacterAchievement> builder)
    {
        builder.ToTable("character_achievements");
        builder.HasKey(ca => new { ca.CharacterId, ca.AchievementId });
        builder.Property(ca => ca.CharacterId).HasColumnName("character_id");
        builder.Property(ca => ca.AchievementId).HasColumnName("achievement_id");
        builder.Property(ca => ca.Progress).HasColumnName("progress");
        builder.Property(ca => ca.EarnedAt).HasColumnName("earned_at");

        builder.HasOne(ca => ca.Character).WithMany(c => c.Achievements).HasForeignKey(ca => ca.CharacterId);
        builder.HasOne(ca => ca.Achievement).WithMany().HasForeignKey(ca => ca.AchievementId);
    }
}

public class GuildMemberConfiguration : IEntityTypeConfiguration<GuildMember>
{
    public void Configure(EntityTypeBuilder<GuildMember> builder)
    {
        builder.ToTable("guild_members");
        builder.HasKey(gm => new { gm.GuildId, gm.CharacterId });
        builder.Property(gm => gm.GuildId).HasColumnName("guild_id");
        builder.Property(gm => gm.CharacterId).HasColumnName("character_id");
        builder.Property(gm => gm.Rank).HasColumnName("rank").HasMaxLength(20);
        builder.Property(gm => gm.ContributionXp).HasColumnName("contribution_xp").HasDefaultValue(0L);
        builder.Property(gm => gm.JoinedAt).HasColumnName("joined_at").HasDefaultValueSql("NOW()");
        builder.Property(gm => gm.LastActiveAt).HasColumnName("last_active_at").HasDefaultValueSql("NOW()");

        builder.HasOne(gm => gm.Guild).WithMany(g => g.Members).HasForeignKey(gm => gm.GuildId);
        builder.HasOne(gm => gm.Character).WithMany().HasForeignKey(gm => gm.CharacterId);
    }
}

public class CharacterEquipmentConfiguration : IEntityTypeConfiguration<CharacterEquipment>
{
    public void Configure(EntityTypeBuilder<CharacterEquipment> builder)
    {
        builder.ToTable("character_equipment");
        builder.HasKey(ce => new { ce.CharacterId, ce.Slot });
        builder.Property(ce => ce.CharacterId).HasColumnName("character_id");
        builder.Property(ce => ce.Slot).HasColumnName("slot").HasMaxLength(30);
        builder.Property(ce => ce.ItemId).HasColumnName("item_id");
        builder.Property(ce => ce.EquippedAt).HasColumnName("equipped_at").HasDefaultValueSql("NOW()");

        builder.HasOne(ce => ce.Character).WithMany(c => c.Equipment).HasForeignKey(ce => ce.CharacterId);
        builder.HasOne(ce => ce.Item).WithMany().HasForeignKey(ce => ce.ItemId);
    }
}

public class AchievementConfiguration : IEntityTypeConfiguration<Achievement>
{
    public void Configure(EntityTypeBuilder<Achievement> builder)
    {
        builder.ToTable("achievements");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(a => a.Description).HasColumnName("description");
        builder.Property(a => a.Category).HasColumnName("category").HasMaxLength(50).IsRequired();
        builder.Property(a => a.Points).HasColumnName("points").HasDefaultValue((short)10);
        builder.Property(a => a.IsAccountWide).HasColumnName("is_account_wide").HasDefaultValue(false);
        builder.Property(a => a.IconUrl).HasColumnName("icon_url");
        builder.Property(a => a.Criteria).HasColumnName("criteria");
        builder.Property(a => a.RewardTitle).HasColumnName("reward_title");
        builder.Property(a => a.RewardItemId).HasColumnName("reward_item_id");
    }
}

public class QuestConfiguration : IEntityTypeConfiguration<Quest>
{
    public void Configure(EntityTypeBuilder<Quest> builder)
    {
        builder.ToTable("quests");
        builder.HasKey(q => q.Id);
        builder.Property(q => q.Id).HasColumnName("id");
        builder.Property(q => q.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(q => q.Description).HasColumnName("description");
        builder.Property(q => q.QuestType).HasColumnName("quest_type").HasMaxLength(30).IsRequired();
        builder.Property(q => q.MinLevel).HasColumnName("min_level").HasDefaultValue((short)1);
        builder.Property(q => q.MaxLevel).HasColumnName("max_level");
        builder.Property(q => q.RequiredClass).HasColumnName("required_class");
        builder.Property(q => q.PrerequisiteQuestId).HasColumnName("prerequisite_quest_id");
        builder.Property(q => q.ZoneId).HasColumnName("zone_id");
        builder.Property(q => q.IsDaily).HasColumnName("is_daily").HasDefaultValue(false);
        builder.Property(q => q.IsWeekly).HasColumnName("is_weekly").HasDefaultValue(false);
        builder.Property(q => q.IsRepeatable).HasColumnName("is_repeatable").HasDefaultValue(false);
        builder.Property(q => q.XpReward).HasColumnName("xp_reward").HasDefaultValue(0);
        builder.Property(q => q.GoldReward).HasColumnName("gold_reward").HasDefaultValue(0);
        builder.Property(q => q.Objectives).HasColumnName("objectives");
        builder.Property(q => q.RewardItems).HasColumnName("reward_items");
        builder.Property(q => q.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
    }
}

public class CharacterQuestConfiguration : IEntityTypeConfiguration<CharacterQuest>
{
    public void Configure(EntityTypeBuilder<CharacterQuest> builder)
    {
        builder.ToTable("character_quests");
        builder.HasKey(cq => cq.Id);
        builder.Property(cq => cq.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
        builder.Property(cq => cq.CharacterId).HasColumnName("character_id");
        builder.Property(cq => cq.QuestId).HasColumnName("quest_id");
        builder.Property(cq => cq.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("in_progress");
        builder.Property(cq => cq.Progress).HasColumnName("progress");
        builder.Property(cq => cq.AcceptedAt).HasColumnName("accepted_at").HasDefaultValueSql("NOW()");
        builder.Property(cq => cq.CompletedAt).HasColumnName("completed_at");

        builder.HasOne(cq => cq.Character).WithMany(c => c.Quests).HasForeignKey(cq => cq.CharacterId);
        builder.HasOne(cq => cq.Quest).WithMany().HasForeignKey(cq => cq.QuestId);
    }
}

public class GuildConfiguration : IEntityTypeConfiguration<Guild>
{
    public void Configure(EntityTypeBuilder<Guild> builder)
    {
        builder.ToTable("guilds");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
        builder.Property(g => g.Name).HasColumnName("name").HasMaxLength(64).IsRequired();
        builder.Property(g => g.Tag).HasColumnName("tag").HasMaxLength(6).IsRequired();
        builder.Property(g => g.Description).HasColumnName("description");
        builder.Property(g => g.Level).HasColumnName("level").HasDefaultValue((short)1);
        builder.Property(g => g.Experience).HasColumnName("experience").HasDefaultValue(0L);
        builder.Property(g => g.Gold).HasColumnName("gold").HasDefaultValue(0L);
        builder.Property(g => g.Motd).HasColumnName("motd");
        builder.Property(g => g.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
        builder.Property(g => g.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");
        builder.HasIndex(g => g.Name).IsUnique();
        builder.HasIndex(g => g.Tag).IsUnique();
    }
}

public class ZoneConfiguration : IEntityTypeConfiguration<Zone>
{
    public void Configure(EntityTypeBuilder<Zone> builder)
    {
        builder.ToTable("zones");
        builder.HasKey(z => z.Id);
        builder.Property(z => z.Id).HasColumnName("id");
        builder.Property(z => z.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(z => z.Description).HasColumnName("description");
        builder.Property(z => z.ZoneType).HasColumnName("zone_type").HasMaxLength(30).HasDefaultValue("open_world");
        builder.Property(z => z.MinLevel).HasColumnName("min_level").HasDefaultValue((short)1);
        builder.Property(z => z.MaxLevel).HasColumnName("max_level").HasDefaultValue((short)60);
        builder.Property(z => z.IsPvp).HasColumnName("is_pvp").HasDefaultValue(false);
        builder.Property(z => z.IsInstanced).HasColumnName("is_instanced").HasDefaultValue(false);
        builder.Property(z => z.MaxPlayers).HasColumnName("max_players");
        builder.Property(z => z.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
    }
}

public class ItemConfiguration : IEntityTypeConfiguration<Item>
{
    public void Configure(EntityTypeBuilder<Item> builder)
    {
        builder.ToTable("items");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnName("id");
        builder.Property(i => i.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
        builder.Property(i => i.Description).HasColumnName("description");
        builder.Property(i => i.ItemType).HasColumnName("item_type").HasMaxLength(30).IsRequired();
        builder.Property(i => i.Rarity).HasColumnName("rarity").HasMaxLength(20).HasDefaultValue("common");
        builder.Property(i => i.RequiredLevel).HasColumnName("required_level").HasDefaultValue((short)1);
        builder.Property(i => i.RequiredClass).HasColumnName("required_class");
        builder.Property(i => i.IsStackable).HasColumnName("is_stackable").HasDefaultValue(false);
        builder.Property(i => i.MaxStack).HasColumnName("max_stack").HasDefaultValue(1);
        builder.Property(i => i.Weight).HasColumnName("weight").HasPrecision(6, 2);
        builder.Property(i => i.SellPrice).HasColumnName("sell_price");
        builder.Property(i => i.BuyPrice).HasColumnName("buy_price");
        builder.Property(i => i.IconUrl).HasColumnName("icon_url");
        builder.Property(i => i.BonusStrength).HasColumnName("bonus_strength");
        builder.Property(i => i.BonusAgility).HasColumnName("bonus_agility");
        builder.Property(i => i.BonusIntelligence).HasColumnName("bonus_intelligence");
        builder.Property(i => i.BonusEndurance).HasColumnName("bonus_endurance");
        builder.Property(i => i.BonusSpirit).HasColumnName("bonus_spirit");
        builder.Property(i => i.BonusHp).HasColumnName("bonus_hp");
        builder.Property(i => i.BonusMana).HasColumnName("bonus_mana");
        builder.Property(i => i.BonusAttackPower).HasColumnName("bonus_attack_power");
        builder.Property(i => i.BonusSpellPower).HasColumnName("bonus_spell_power");
        builder.Property(i => i.BonusArmor).HasColumnName("bonus_armor");
        builder.Property(i => i.BonusCrit).HasColumnName("bonus_crit").HasPrecision(5, 2);
        builder.Property(i => i.EquipmentSlot).HasColumnName("equipment_slot");
    }
}

public class InventorySlotConfiguration : IEntityTypeConfiguration<InventorySlot>
{
    public void Configure(EntityTypeBuilder<InventorySlot> builder)
    {
        builder.ToTable("inventory");
        builder.HasKey(inv => inv.Id);
        builder.Property(inv => inv.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
        builder.Property(inv => inv.CharacterId).HasColumnName("character_id");
        builder.Property(inv => inv.ItemId).HasColumnName("item_id");
        builder.Property(inv => inv.Quantity).HasColumnName("quantity").HasDefaultValue(1);
        builder.Property(inv => inv.SlotIndex).HasColumnName("slot_index");
        builder.Property(inv => inv.AcquiredAt).HasColumnName("acquired_at").HasDefaultValueSql("NOW()");

        builder.HasOne(inv => inv.Character).WithMany(c => c.Inventory).HasForeignKey(inv => inv.CharacterId);
        builder.HasOne(inv => inv.Item).WithMany().HasForeignKey(inv => inv.ItemId);
    }
}

public class AuctionListingConfiguration : IEntityTypeConfiguration<AuctionListing>
{
    public void Configure(EntityTypeBuilder<AuctionListing> builder)
    {
        builder.ToTable("auction_listings");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
        builder.Property(a => a.SellerId).HasColumnName("seller_id");
        builder.Property(a => a.ItemId).HasColumnName("item_id");
        builder.Property(a => a.Quantity).HasColumnName("quantity").HasDefaultValue(1);
        builder.Property(a => a.StartingBid).HasColumnName("starting_bid");
        builder.Property(a => a.BuyoutPrice).HasColumnName("buyout_price");
        builder.Property(a => a.CurrentBid).HasColumnName("current_bid");
        builder.Property(a => a.BidderId).HasColumnName("bidder_id");
        builder.Property(a => a.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("active");
        builder.Property(a => a.DurationHours).HasColumnName("duration_hours").HasDefaultValue((short)48);
        builder.Property(a => a.ListedAt).HasColumnName("listed_at").HasDefaultValueSql("NOW()");
        builder.Property(a => a.ExpiresAt).HasColumnName("expires_at");
        builder.Property(a => a.SoldAt).HasColumnName("sold_at");

        builder.HasOne(a => a.Seller).WithMany().HasForeignKey(a => a.SellerId);
        builder.HasOne(a => a.Item).WithMany().HasForeignKey(a => a.ItemId);
        builder.HasOne(a => a.Bidder).WithMany().HasForeignKey(a => a.BidderId).IsRequired(false);
    }
}

public class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.ToTable("chat_messages");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.Channel).HasColumnName("channel").HasMaxLength(20).IsRequired();
        builder.Property(m => m.SenderId).HasColumnName("sender_id");
        builder.Property(m => m.RecipientId).HasColumnName("recipient_id");
        builder.Property(m => m.ZoneId).HasColumnName("zone_id");
        builder.Property(m => m.GuildId).HasColumnName("guild_id");
        builder.Property(m => m.Content).HasColumnName("content").IsRequired();
        builder.Property(m => m.SentAt).HasColumnName("sent_at").HasDefaultValueSql("NOW()");

        builder.HasOne(m => m.Sender).WithMany().HasForeignKey(m => m.SenderId);
    }
}
