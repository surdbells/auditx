using AuditX.Domain.Enums;
using AuditX.Domain.Evidence;
using AuditX.Infrastructure.Persistence.Conversions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuditX.Infrastructure.Persistence.Configurations;

public sealed class EvidenceFileConfiguration : IEntityTypeConfiguration<EvidenceFile>
{
    public void Configure(EntityTypeBuilder<EvidenceFile> builder)
    {
        builder.ToTable("evidence_files");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.StoragePath).HasMaxLength(1024).IsRequired();
        builder.Property(e => e.OriginalFilename).HasMaxLength(512).IsRequired();
        builder.Property(e => e.MimeType).HasMaxLength(255).IsRequired();
        builder.Property(e => e.Sha256Hash).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(e => e.DeletionReason);
        builder.Property(e => e.Version).IsRowVersion();
        builder.Property(e => e.ContextType)
            .HasConversion(new SnakeCaseEnumConverter<EvidenceContextType>())
            .HasMaxLength(30)
            .IsRequired();

        builder.HasIndex(e => new { e.AuditId, e.ContextType, e.ContextId });
        builder.HasIndex(e => e.AuditId);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
