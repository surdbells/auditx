using AuditX.Domain.Audits;
using AuditX.Domain.Enums;
using AuditX.Domain.Exceptions;
using AuditX.Infrastructure.Persistence.Conversions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuditX.Infrastructure.Persistence.Configurations;

public sealed class AuditExceptionConfiguration : IEntityTypeConfiguration<AuditException>
{
    public void Configure(EntityTypeBuilder<AuditException> builder)
    {
        builder.ToTable("exceptions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.Title).HasMaxLength(255).IsRequired();
        builder.Property(e => e.RootCause).IsRequired();
        builder.Property(e => e.Recommendation).IsRequired();
        builder.Property(e => e.Category).HasMaxLength(100);
        builder.Property(e => e.ConfigurationVersionsJson).IsRequired().HasDefaultValue("{}");
        builder.Property(e => e.Version).IsRowVersion();
        builder.Property(e => e.Severity).HasConversion(new SnakeCaseEnumConverter<ExceptionSeverity>()).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Status).HasConversion(new SnakeCaseEnumConverter<ExceptionStatus>()).HasMaxLength(30).IsRequired();

        // Referential integrity to the originating checklist item (Restrict — avoid multiple cascade paths).
        builder.HasOne<AuditChecklistItem>().WithMany().HasForeignKey(e => e.ChecklistItemId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.MapActions).WithOne().HasForeignKey(a => a.ExceptionId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(e => e.MapActions).HasField("_mapActions").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(e => new { e.Status, e.Severity, e.TargetDate });
        builder.HasIndex(e => new { e.AuditableEntityId, e.Category, e.Status });
        builder.HasIndex(e => e.AuditId);
        builder.HasIndex(e => e.OwnerUserId);
    }
}

public sealed class MapActionConfiguration : IEntityTypeConfiguration<MapAction>
{
    public void Configure(EntityTypeBuilder<MapAction> builder)
    {
        builder.ToTable("map_actions");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.Description).IsRequired();
        builder.Property(a => a.ExpectedEvidenceType).HasMaxLength(255);
        builder.Property(a => a.Status).HasConversion(new SnakeCaseEnumConverter<MapActionStatus>()).HasMaxLength(20).IsRequired();

        builder.HasIndex(a => a.ExceptionId);
    }
}
