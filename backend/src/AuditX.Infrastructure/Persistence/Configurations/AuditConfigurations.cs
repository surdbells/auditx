using AuditX.Domain.Audits;
using AuditX.Domain.Enums;
using AuditX.Infrastructure.Persistence.Conversions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuditX.Infrastructure.Persistence.Configurations;

public sealed class AuditConfiguration : IEntityTypeConfiguration<Audit>
{
    public void Configure(EntityTypeBuilder<Audit> builder)
    {
        builder.ToTable("audits");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.Name).HasMaxLength(255).IsRequired();
        builder.Property(a => a.ScopeDescription);
        builder.Property(a => a.AuditType).HasMaxLength(100).IsRequired();
        builder.Property(a => a.ConfigurationVersionsJson).IsRequired().HasDefaultValue("{}");
        builder.Property(a => a.CancellationReason);
        builder.Property(a => a.LastTransitionReason);
        builder.Property(a => a.Version).IsRowVersion();
        builder.Property(a => a.Status)
            .HasConversion(new SnakeCaseEnumConverter<AuditStatus>())
            .HasMaxLength(30)
            .IsRequired();

        builder.HasIndex(a => a.Status);
        builder.HasIndex(a => a.AuditType);
        builder.HasIndex(a => a.LeadUserId);
        builder.HasIndex(a => a.PlanItemId);
        builder.HasIndex(a => new { a.Status, a.StartDate });

        builder.HasMany(a => a.TeamMembers).WithOne().HasForeignKey(m => m.AuditId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(a => a.ChecklistItems).WithOne().HasForeignKey(i => i.AuditId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(a => a.TeamMembers).HasField("_teamMembers").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(a => a.ChecklistItems).HasField("_checklistItems").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class AuditTeamMemberConfiguration : IEntityTypeConfiguration<AuditTeamMember>
{
    public void Configure(EntityTypeBuilder<AuditTeamMember> builder)
    {
        builder.ToTable("audit_team_members");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.TeamRole)
            .HasConversion(new SnakeCaseEnumConverter<TeamRole>())
            .HasMaxLength(30)
            .IsRequired();

        builder.HasIndex(m => m.AuditId);
        builder.HasIndex(m => new { m.AuditId, m.UserId });
    }
}

public sealed class AuditChecklistItemConfiguration : IEntityTypeConfiguration<AuditChecklistItem>
{
    public void Configure(EntityTypeBuilder<AuditChecklistItem> builder)
    {
        builder.ToTable("audit_checklist_items");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();

        builder.Property(i => i.SectionName).HasMaxLength(100);
        builder.Property(i => i.Prompt).IsRequired();
        builder.Property(i => i.ResponseType)
            .HasConversion(new SnakeCaseEnumConverter<ResponseType>())
            .HasMaxLength(30)
            .IsRequired();
        builder.Property(i => i.ItemState)
            .HasConversion(new SnakeCaseEnumConverter<ChecklistItemState>())
            .HasMaxLength(30)
            .IsRequired();

        builder.HasIndex(i => new { i.AuditId, i.OrderIndex });
        builder.HasIndex(i => i.AssignedUserId);
    }
}
