using AuditX.Domain.Enums;
using AuditX.Domain.Risks;
using AuditX.Domain.Universe;
using AuditX.Infrastructure.Persistence.Conversions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuditX.Infrastructure.Persistence.Configurations;

public sealed class RiskConfiguration : IEntityTypeConfiguration<Risk>
{
    public void Configure(EntityTypeBuilder<Risk> builder)
    {
        builder.ToTable("risks");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Title).HasMaxLength(300).IsRequired();
        builder.Property(r => r.Category).HasMaxLength(100).IsRequired();
        builder.Property(r => r.TreatmentPlan);
        builder.Property(r => r.ClosureRationale);
        builder.Property(r => r.Version).IsRowVersion();

        builder.Property(r => r.Status)
            .HasConversion(new SnakeCaseEnumConverter<RiskStatus>())
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(r => r.TreatmentStrategy)
            .HasConversion(new SnakeCaseEnumConverter<RiskTreatmentStrategy>())
            .HasMaxLength(20);

        // Soft-delete: live rows only in normal reads.
        builder.HasQueryFilter(r => !r.IsDeleted);

        // Optional link to a universe entity; SetNull so archiving/removing the entity detaches the risk.
        builder.HasOne<AuditableEntity>().WithMany().HasForeignKey(r => r.AuditableEntityId).OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(r => r.Status);
        builder.HasIndex(r => r.OwnerUserId);
        builder.HasIndex(r => r.Category);
        builder.HasIndex(r => r.AuditableEntityId);
    }
}
