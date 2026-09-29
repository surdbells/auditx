using AuditX.Domain.Enums;
using AuditX.Domain.Identity;
using AuditX.Infrastructure.Persistence.Conversions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuditX.Infrastructure.Persistence.Configurations;

public sealed class UserCredentialConfiguration : IEntityTypeConfiguration<UserCredential>
{
    public void Configure(EntityTypeBuilder<UserCredential> builder)
    {
        builder.ToTable("user_credentials");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        // One credential per user (soft reference to users.id, mirroring the codebase's no-FK convention).
        builder.HasIndex(c => c.UserId).IsUnique();
        builder.Property(c => c.PasswordHash).HasMaxLength(400).IsRequired();
        builder.Property(c => c.SecurityStamp).IsRequired();
    }
}

public sealed class UserPasswordHistoryConfiguration : IEntityTypeConfiguration<UserPasswordHistory>
{
    public void Configure(EntityTypeBuilder<UserPasswordHistory> builder)
    {
        builder.ToTable("user_password_history");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).ValueGeneratedNever();
        builder.Property(h => h.PasswordHash).HasMaxLength(400).IsRequired();
        builder.HasIndex(h => new { h.UserId, h.SetAt });
    }
}

public sealed class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("password_reset_tokens");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.TokenHash).HasMaxLength(128).IsRequired();
        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasIndex(t => t.UserId);
        builder.Property(t => t.Purpose)
            .HasConversion(new SnakeCaseEnumConverter<CredentialTokenPurpose>())
            .HasMaxLength(20)
            .IsRequired();
    }
}
