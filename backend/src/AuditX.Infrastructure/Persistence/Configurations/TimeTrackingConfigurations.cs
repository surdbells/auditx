using AuditX.Domain.Audits;
using AuditX.Domain.Enums;
using AuditX.Domain.TimeTracking;
using AuditX.Infrastructure.Persistence.Conversions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuditX.Infrastructure.Persistence.Configurations;

public sealed class TimeEntryConfiguration : IEntityTypeConfiguration<TimeEntry>
{
    public void Configure(EntityTypeBuilder<TimeEntry> builder)
    {
        builder.ToTable("time_entries");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.Hours).HasPrecision(6, 2).IsRequired();
        builder.Property(t => t.Notes).HasMaxLength(1000);
        builder.Property(t => t.Version).IsRowVersion();
        builder.Property(t => t.Category)
            .HasConversion(new SnakeCaseEnumConverter<TimeEntryCategory>())
            .HasMaxLength(30)
            .IsRequired();

        // Soft-delete: live rows only in normal reads.
        builder.HasQueryFilter(t => !t.IsDeleted);

        // FK to the owning audit (Restrict — audits are not hard-deleted, and this avoids cascade-path clashes).
        builder.HasOne<Audit>().WithMany().HasForeignKey(t => t.AuditId).OnDelete(DeleteBehavior.Restrict);
        // Optional FK to a specific checklist item. SetNull: a checklist item can be hard-deleted while a Draft
        // audit (after reopen) still has time logged against it — detach the historical link rather than block the
        // delete with an FK violation. The audit_id FK is Restrict, so there is no multiple-cascade-path conflict.
        builder.HasOne<AuditChecklistItem>().WithMany().HasForeignKey(t => t.ChecklistItemId).OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(t => t.AuditId);
        builder.HasIndex(t => t.UserId);
        builder.HasIndex(t => new { t.AuditId, t.UserId });
    }
}
