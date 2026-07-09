using AuditX.Domain.Audits;
using AuditX.Domain.Enums;
using AuditX.Domain.Reports;
using AuditX.Infrastructure.Persistence.Conversions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuditX.Infrastructure.Persistence.Configurations;

public sealed class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> builder)
    {
        builder.ToTable("reports");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Status).HasConversion(new SnakeCaseEnumConverter<ReportStatus>()).HasMaxLength(20).IsRequired();
        builder.Property(r => r.Kind).HasConversion(new SnakeCaseEnumConverter<ReportKind>()).HasMaxLength(30).IsRequired();
        builder.Property(r => r.Sha256Hash).HasMaxLength(64);
        builder.Property(r => r.TemplateDefinitionSnapshotJson).IsRequired();
        builder.Property(r => r.RequestedFormatsJson).IsRequired();
        builder.Property(r => r.ProducedArtefactsJson).IsRequired();
        builder.Property(r => r.FailureReason).HasMaxLength(1000);
        builder.Property(r => r.DeletionReason);
        builder.Property(r => r.Version).IsRowVersion();

        // FK to the audit, Restrict — generating/deleting a report never cascades into the audit. Optional: standalone
        // (cross-audit) reports have no audit.
        builder.HasOne<Audit>().WithMany().HasForeignKey(r => r.AuditId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(r => r.Distributions).WithOne().HasForeignKey(d => d.ReportId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(r => r.Distributions).HasField("_distributions").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(r => r.AuditId);
        builder.HasIndex(r => r.Status);
        // Engagement reports: per-audit integer versioning is unique (no two engagement rows share an (audit, version)).
        builder.HasIndex(r => new { r.AuditId, r.VersionNumber }).IsUnique().HasFilter("[audit_id] IS NOT NULL");
        // Standalone reports: per-kind integer versioning is unique (no audit).
        builder.HasIndex(r => new { r.Kind, r.VersionNumber }).IsUnique().HasFilter("[audit_id] IS NULL");

        builder.HasQueryFilter(r => !r.IsDeleted);
    }
}

public sealed class ReportDistributionConfiguration : IEntityTypeConfiguration<ReportDistribution>
{
    public void Configure(EntityTypeBuilder<ReportDistribution> builder)
    {
        builder.ToTable("report_distributions");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.RecipientEmail).HasMaxLength(320);
        builder.Property(d => d.Outcome).HasConversion(new SnakeCaseEnumConverter<DeliveryOutcome>()).HasMaxLength(20).IsRequired();

        builder.HasIndex(d => d.ReportId);
        builder.HasIndex(d => new { d.ReportId, d.DispatchedAt });
    }
}

public sealed class ReportTemplateConfiguration : IEntityTypeConfiguration<ReportTemplate>
{
    public void Configure(EntityTypeBuilder<ReportTemplate> builder)
    {
        builder.ToTable("report_templates");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.Name).HasMaxLength(200).IsRequired();
        builder.Property(t => t.TemplateDefinitionJson).IsRequired();
        builder.Property(t => t.ActivationReason);
        builder.Property(t => t.Version).IsRowVersion();

        builder.HasIndex(t => t.VersionNumber).IsUnique();
        // Exactly one active, live template bank-wide (US-M8-007): filtered unique on is_active = 1 AND is_deleted = 0,
        // so a soft-deleted active template does not block promoting a replacement to active.
        builder.HasIndex(t => t.IsActive).IsUnique().HasFilter("[is_active] = 1 AND [is_deleted] = 0");

        builder.HasQueryFilter(t => !t.IsDeleted);
    }
}
