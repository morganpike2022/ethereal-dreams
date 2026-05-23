using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MMORPG.Api.Models;

namespace MMORPG.Api.Data.Configurations;

public class CharacterConfiguration : IEntityTypeConfiguration<Character>
{
    public void Configure(EntityTypeBuilder<Character> builder)
    {
        builder.ToTable("characters");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
        builder.Property(c => c.PlayerId).HasColumnName("player_id").IsRequired();
        builder.Property(c => c.ClassId).HasColumnName("class_id").IsRequired();
        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(32).IsRequired();
        builder.Property(c => c.Level).HasColumnName("level").HasDefaultValue((short)1);
        builder.Property(c => c.Experience).HasColumnName("experience").HasDefaultValue(0L);
        builder.Property(c => c.Gold).HasColumnName("gold").HasDefaultValue(0L);
        builder.Property(c => c.CurrentHp).HasColumnName("current_hp");
        builder.Property(c => c.MaxHp).HasColumnName("max_hp");
        builder.Property(c => c.CurrentMana).HasColumnName("current_mana");
        builder.Property(c => c.MaxMana).HasColumnName("max_mana");
        builder.Property(c => c.Strength).HasColumnName("strength");
        builder.Property(c => c.Agility).HasColumnName("agility");
        builder.Property(c => c.Intelligence).HasColumnName("intelligence");
        builder.Property(c => c.Endurance).HasColumnName("endurance");
        builder.Property(c => c.Spirit).HasColumnName("spirit");
        builder.Property(c => c.AttackPower).HasColumnName("attack_power").HasDefaultValue(0);
        builder.Property(c => c.SpellPower).HasColumnName("spell_power").HasDefaultValue(0);
        builder.Property(c => c.Armor).HasColumnName("armor").HasDefaultValue(0);
        builder.Property(c => c.CritChance).HasColumnName("crit_chance").HasPrecision(5, 2).HasDefaultValue(5.00m);
        builder.Property(c => c.DodgeChance).HasColumnName("dodge_chance").HasPrecision(5, 2).HasDefaultValue(5.00m);
        builder.Property(c => c.Haste).HasColumnName("haste").HasPrecision(5, 2).HasDefaultValue(0.00m);
        builder.Property(c => c.ZoneId).HasColumnName("zone_id");
        builder.Property(c => c.PosX).HasColumnName("pos_x").HasPrecision(10, 2).HasDefaultValue(0m);
        builder.Property(c => c.PosY).HasColumnName("pos_y").HasPrecision(10, 2).HasDefaultValue(0m);
        builder.Property(c => c.AppearanceData).HasColumnName("appearance_data")
               .HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb");
        builder.Property(c => c.IsOnline).HasColumnName("is_online").HasDefaultValue(false);
        builder.Property(c => c.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(c => c.DeleteAt).HasColumnName("delete_at");
        builder.Property(c => c.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");

        builder.HasIndex(c => c.PlayerId);
        builder.HasIndex(c => c.ZoneId);
        builder.HasQueryFilter(c => !c.IsDeleted);

        builder.HasOne(c => c.Class)
               .WithMany(cc => cc.Characters)
               .HasForeignKey(c => c.ClassId);

        builder.HasOne(c => c.Zone)
               .WithMany(z => z.Characters)
               .HasForeignKey(c => c.ZoneId)
               .IsRequired(false);
    }
}

public class CharacterClassConfiguration : IEntityTypeConfiguration<CharacterClass>
{
    public void Configure(EntityTypeBuilder<CharacterClass> builder)
    {
        builder.ToTable("character_classes");

        builder.HasKey(cc => cc.Id);
        builder.Property(cc => cc.Id).HasColumnName("id");
        builder.Property(cc => cc.Name).HasColumnName("name").HasMaxLength(50).IsRequired();
        builder.Property(cc => cc.DisplayName).HasColumnName("display_name").HasMaxLength(50).IsRequired();
        builder.Property(cc => cc.Description).HasColumnName("description");
        builder.Property(cc => cc.BaseHp).HasColumnName("base_hp");
        builder.Property(cc => cc.BaseMana).HasColumnName("base_mana");
        builder.Property(cc => cc.BaseStrength).HasColumnName("base_strength");
        builder.Property(cc => cc.BaseAgility).HasColumnName("base_agility");
        builder.Property(cc => cc.BaseIntelligence).HasColumnName("base_intelligence");
        builder.Property(cc => cc.BaseEndurance).HasColumnName("base_endurance");
        builder.Property(cc => cc.BaseSpirit).HasColumnName("base_spirit");
        builder.Property(cc => cc.HpPerLevel).HasColumnName("hp_per_level");
        builder.Property(cc => cc.ManaPerLevel).HasColumnName("mana_per_level");
    }
}
