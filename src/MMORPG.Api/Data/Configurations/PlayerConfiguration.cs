using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MMORPG.Api.Models;

namespace MMORPG.Api.Data.Configurations;

public class PlayerConfiguration : IEntityTypeConfiguration<Player>
{
    public void Configure(EntityTypeBuilder<Player> builder)
    {
        builder.ToTable("players");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
        builder.Property(p => p.Username).HasColumnName("username").HasMaxLength(32).IsRequired();
        builder.Property(p => p.Email).HasColumnName("email").HasMaxLength(255).IsRequired();
        builder.Property(p => p.PasswordHash).HasColumnName("password_hash").IsRequired();
        builder.Property(p => p.EmailVerified).HasColumnName("email_verified").HasDefaultValue(false);
        builder.Property(p => p.IsAdmin).HasColumnName("is_admin").HasDefaultValue(false);
        builder.Property(p => p.LastLoginAt).HasColumnName("last_login_at");
        builder.Property(p => p.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");
        builder.Property(p => p.DeletedAt).HasColumnName("deleted_at");

        builder.HasIndex(p => p.Username).IsUnique();
        builder.HasIndex(p => p.Email).IsUnique();
        builder.HasQueryFilter(p => p.DeletedAt == null);

        builder.HasMany(p => p.Characters)
               .WithOne(c => c.Player)
               .HasForeignKey(c => c.PlayerId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.RefreshTokens)
               .WithOne(rt => rt.Player)
               .HasForeignKey(rt => rt.PlayerId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
