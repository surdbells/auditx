using AuditX.Domain.Enums;
using AuditX.Domain.Identity;
using AuditX.Infrastructure.Persistence.Conversions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuditX.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();

        builder.Property(u => u.AdSamAccountName).HasMaxLength(256).IsRequired();
        builder.Property(u => u.AdUserPrincipalName).HasMaxLength(320).IsRequired();
        builder.Property(u => u.AdObjectSid).HasMaxLength(200).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(320).IsRequired();
        builder.Property(u => u.FirstName).HasMaxLength(200).IsRequired();
        builder.Property(u => u.LastName).HasMaxLength(200).IsRequired();
        builder.Property(u => u.DisplayName).HasMaxLength(400).IsRequired();
        builder.Property(u => u.Timezone).HasMaxLength(64).IsRequired();
        builder.Property(u => u.Locale).HasMaxLength(16).IsRequired();
        builder.Property(u => u.NotificationPreferencesJson);
        builder.Property(u => u.Status)
            .HasConversion(new SnakeCaseEnumConverter<UserStatus>())
            .HasMaxLength(40)
            .IsRequired();

        // AD natural keys are unique among LIVE users only, so off-boarding (soft-delete) frees the keys for
        // a returning principal to be re-provisioned with the same objectSID / sAMAccountName / UPN.
        builder.HasIndex(u => u.AdObjectSid).IsUnique().HasFilter("[is_deleted] = 0");
        builder.HasIndex(u => u.AdSamAccountName).IsUnique().HasFilter("[is_deleted] = 0");
        builder.HasIndex(u => u.AdUserPrincipalName).IsUnique().HasFilter("[is_deleted] = 0");
        builder.HasIndex(u => u.Status);

        builder.HasQueryFilter(u => !u.IsDeleted);
    }
}

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Name).HasMaxLength(200).IsRequired();
        builder.Property(r => r.Description).HasMaxLength(1000).IsRequired();
        builder.HasIndex(r => r.Name).IsUnique();

        builder.PrimitiveCollection(r => r.ParentRoleIds).HasColumnName("parent_role_ids");

        builder.HasMany(r => r.Permissions)
            .WithOne()
            .HasForeignKey(p => p.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Metadata.FindNavigation(nameof(Role.Permissions))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("role_permissions");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.PermissionKey).HasMaxLength(100).IsRequired();
        builder.Property(p => p.ScopePredicateJson);
        builder.Property(p => p.ScopeType)
            .HasConversion(new SnakeCaseEnumConverter<PermissionScopeType>())
            .HasMaxLength(40)
            .IsRequired();

        builder.HasIndex(p => p.RoleId);
        builder.HasIndex(p => new { p.RoleId, p.PermissionKey });
    }
}

public sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("user_roles");
        builder.HasKey(ur => ur.Id);
        builder.Property(ur => ur.Id).ValueGeneratedNever();

        builder.Property(ur => ur.ScopeValue).HasMaxLength(256);

        builder.HasIndex(ur => ur.UserId);
        builder.HasIndex(ur => ur.RoleId);
        builder.HasIndex(ur => new { ur.DelegatedFromUserId, ur.IsActive });

        builder.HasOne<User>().WithMany().HasForeignKey(ur => ur.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Role>().WithMany().HasForeignKey(ur => ur.RoleId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MakerCheckerActionConfiguration : IEntityTypeConfiguration<MakerCheckerAction>
{
    public void Configure(EntityTypeBuilder<MakerCheckerAction> builder)
    {
        builder.ToTable("maker_checker_actions");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.ActionType).HasMaxLength(100).IsRequired();
        builder.Property(a => a.TargetObjectType).HasMaxLength(100).IsRequired();
        builder.Property(a => a.PendingPayloadJson).IsRequired();
        builder.Property(a => a.ResolutionComment).HasMaxLength(2000);
        builder.Property(a => a.Status)
            .HasConversion(new SnakeCaseEnumConverter<MakerCheckerStatus>())
            .HasMaxLength(40)
            .IsRequired();

        builder.HasIndex(a => a.Status);
        builder.HasIndex(a => new { a.ActionType, a.Status });
    }
}

public sealed class MakerCheckerGateConfiguration : IEntityTypeConfiguration<MakerCheckerGate>
{
    public void Configure(EntityTypeBuilder<MakerCheckerGate> builder)
    {
        builder.ToTable("maker_checker_gates");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).ValueGeneratedNever();

        builder.Property(g => g.ActionType).HasMaxLength(100).IsRequired();
        builder.Property(g => g.CheckerRoleName).HasMaxLength(200);
        builder.HasIndex(g => g.ActionType).IsUnique();
    }
}

public sealed class BankSettingsConfiguration : IEntityTypeConfiguration<BankSettings>
{
    public void Configure(EntityTypeBuilder<BankSettings> builder)
    {
        builder.ToTable("bank_settings");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();

        builder.Property(b => b.BankDisplayName).HasMaxLength(200).IsRequired();
        builder.Property(b => b.Timezone).HasMaxLength(64).IsRequired();
        builder.Property(b => b.LocaleDefault).HasMaxLength(16).IsRequired();
        builder.Property(b => b.AdProvisioningFilterOuDn).HasMaxLength(512);
        builder.Property(b => b.AdProvisioningFilterGroupSid).HasMaxLength(200);
    }
}
