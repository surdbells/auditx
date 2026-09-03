using AuditX.Domain.Audits;
using AuditX.Domain.Enums;
using AuditX.Domain.Evidence;
using AuditX.Infrastructure.Persistence.Conversions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuditX.Infrastructure.Persistence.Configurations;

public sealed class EvidenceRequestConfiguration : IEntityTypeConfiguration<EvidenceRequest>
{
    public void Configure(EntityTypeBuilder<EvidenceRequest> builder)
    {
        builder.ToTable("evidence_requests");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Title).HasMaxLength(300).IsRequired();
        builder.Property(r => r.DocumentType).HasMaxLength(100);
        builder.Property(r => r.WaiveReason).HasMaxLength(2000);
        builder.Property(r => r.Notes).HasMaxLength(2000);
        builder.Property(r => r.Version).IsRowVersion();
        builder.Property(r => r.Status)
            .HasConversion(new SnakeCaseEnumConverter<EvidenceRequestStatus>())
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(r => r.Purpose)
            .HasConversion(new SnakeCaseEnumConverter<EvidenceRequestPurpose>())
            .HasMaxLength(20)
            .IsRequired()
            .HasDefaultValue(EvidenceRequestPurpose.ReviewDocument);
        builder.HasIndex(r => r.RequestedFromUserId);

        // Soft-delete: live rows only in normal reads.
        builder.HasQueryFilter(r => !r.IsDeleted);

        // FK to the owning audit (Restrict) + optional checklist-item link (SetNull).
        builder.HasOne<Audit>().WithMany().HasForeignKey(r => r.AuditId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AuditChecklistItem>().WithMany().HasForeignKey(r => r.ChecklistItemId).OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(r => r.AuditId);
        builder.HasIndex(r => new { r.AuditId, r.Status });
    }
}
