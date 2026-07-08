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

        // 1:1 integrity: at most one audit per plan item (nulls excluded so ad-hoc audits without a plan are unaffected).
        builder.HasIndex(a => a.PlanItemId).IsUnique().HasFilter("[plan_item_id] IS NOT NULL");
        builder.HasIndex(a => new { a.Status, a.StartDate });

        builder.HasMany(a => a.TeamMembers).WithOne().HasForeignKey(m => m.AuditId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(a => a.ChecklistItems).WithOne().HasForeignKey(i => i.AuditId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(a => a.Responses).WithOne().HasForeignKey(r => r.AuditId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(a => a.TeamMembers).HasField("_teamMembers").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(a => a.ChecklistItems).HasField("_checklistItems").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(a => a.Responses).HasField("_responses").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class ChecklistResponseConfiguration : IEntityTypeConfiguration<ChecklistResponse>
{
    public void Configure(EntityTypeBuilder<ChecklistResponse> builder)
    {
        builder.ToTable("checklist_responses");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Verdict)
            .HasConversion(new SnakeCaseEnumConverter<ResponseVerdict>())
            .HasMaxLength(10);
        builder.Property(r => r.Comment);

        // Referential integrity to the owning checklist item (Restrict avoids multiple cascade paths from audits).
        builder.HasOne<AuditChecklistItem>().WithMany().HasForeignKey(r => r.ChecklistItemId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.AuditId, r.ChecklistItemId }).IsUnique();
        builder.HasIndex(r => r.AuditId);
        builder.HasIndex(r => r.ChecklistItemId);
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

        builder.Property(i => i.HasException).HasDefaultValue(false);
        builder.Property(i => i.FailJustification);

        builder.HasIndex(i => new { i.AuditId, i.OrderIndex });
        builder.HasIndex(i => i.AssignedUserId);
    }
}
