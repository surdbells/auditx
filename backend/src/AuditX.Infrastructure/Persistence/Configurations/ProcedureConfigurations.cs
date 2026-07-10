using AuditX.Domain.Audits;
using AuditX.Domain.Enums;
using AuditX.Domain.Execution;
using AuditX.Infrastructure.Persistence.Conversions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuditX.Infrastructure.Persistence.Configurations;

public sealed class AuditProcedureConfiguration : IEntityTypeConfiguration<AuditProcedure>
{
    public void Configure(EntityTypeBuilder<AuditProcedure> builder)
    {
        builder.ToTable("audit_procedures");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Summary).HasMaxLength(2000).IsRequired();
        builder.Property(p => p.Counterparty).HasMaxLength(300);
        builder.Property(p => p.Version).IsRowVersion();
        builder.Property(p => p.Type)
            .HasConversion(new SnakeCaseEnumConverter<ProcedureType>())
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(p => p.Method)
            .HasConversion(new SnakeCaseEnumConverter<SamplingMethod>())
            .HasMaxLength(20);

        // Soft-delete: live rows only in normal reads.
        builder.HasQueryFilter(p => !p.IsDeleted);

        // FK to the owning audit (Restrict — audits are not hard-deleted; avoids cascade-path clashes).
        builder.HasOne<Audit>().WithMany().HasForeignKey(p => p.AuditId).OnDelete(DeleteBehavior.Restrict);
        // Optional FK to a checklist item — SetNull so a Draft-audit item delete detaches the link rather than blocks.
        builder.HasOne<AuditChecklistItem>().WithMany().HasForeignKey(p => p.ChecklistItemId).OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(p => p.AuditId);
        builder.HasIndex(p => p.PerformedByUserId);
    }
}
