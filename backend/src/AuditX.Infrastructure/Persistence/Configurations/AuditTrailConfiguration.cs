using AuditX.Domain.AuditTrail;
using AuditX.Domain.Enums;
using AuditX.Infrastructure.Persistence.Conversions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuditX.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps the append-only audit trail (M11). Append-only enforcement (rejecting UPDATE/DELETE) is added
/// by a SQL trigger in the migration; this configuration provides the schema and query indexes.
/// </summary>
public sealed class AuditTrailEntryConfiguration : IEntityTypeConfiguration<AuditTrailEntry>
{
    public void Configure(EntityTypeBuilder<AuditTrailEntry> builder)
    {
        builder.ToTable("audit_trail");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.EventType).HasMaxLength(100).IsRequired();
        builder.Property(e => e.TargetObjectType).HasMaxLength(100).IsRequired();
        builder.Property(e => e.ActorSystemLabel).HasMaxLength(200);
        builder.Property(e => e.OriginatingTimezone).HasMaxLength(64);
        builder.Property(e => e.BeforeStateJson);
        builder.Property(e => e.AfterStateJson);
        builder.Property(e => e.RequestContextJson);
        builder.Property(e => e.EventPayloadJson);
        builder.Property(e => e.ActorType)
            .HasConversion(new SnakeCaseEnumConverter<ActorType>())
            .HasMaxLength(40)
            .IsRequired();

        builder.HasIndex(e => new { e.TargetObjectType, e.TargetObjectId });
        builder.HasIndex(e => e.EventType);
        builder.HasIndex(e => e.OccurredAtUtc);
        builder.HasIndex(e => e.ActorUserId);
    }
}
