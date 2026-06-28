using AuditX.Domain.Ac;
using AuditX.Domain.Enums;
using AuditX.Infrastructure.Persistence.Conversions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuditX.Infrastructure.Persistence.Configurations;

public sealed class AcPackConfiguration : IEntityTypeConfiguration<AcPack>
{
    public void Configure(EntityTypeBuilder<AcPack> builder)
    {
        builder.ToTable("ac_packs");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Status).HasConversion(new SnakeCaseEnumConverter<AcPackStatus>()).HasMaxLength(20).IsRequired();
        builder.Property(p => p.AcMeetingLabel).HasMaxLength(200);
        builder.Property(p => p.ContentSnapshotJson);
        builder.Property(p => p.CiaSupplementaryText);
        builder.Property(p => p.ArtefactStoragePath).HasMaxLength(512);
        builder.Property(p => p.Sha256Hash).HasMaxLength(64);
        builder.Property(p => p.ProducedArtefactsJson).IsRequired();
        builder.Property(p => p.RequestedFormatsJson).IsRequired();
        builder.Property(p => p.FailureReason).HasMaxLength(1000);
        builder.Property(p => p.DeletionReason);
        builder.Property(p => p.Version).IsRowVersion();

        builder.HasMany(p => p.Distributions).WithOne().HasForeignKey(d => d.AcPackId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.Distributions).HasField("_distributions").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(p => p.Status);
        // Per-bank integer versioning is unique (no two AC pack rows share a version).
        builder.HasIndex(p => p.VersionNumber).IsUnique();

        builder.HasQueryFilter(p => !p.IsDeleted);
    }
}

public sealed class AcPackDistributionConfiguration : IEntityTypeConfiguration<AcPackDistribution>
{
    public void Configure(EntityTypeBuilder<AcPackDistribution> builder)
    {
        builder.ToTable("ac_pack_distributions");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.Outcome).HasConversion(new SnakeCaseEnumConverter<AcDeliveryOutcome>()).HasMaxLength(20).IsRequired();

        builder.HasIndex(d => d.AcPackId);
        builder.HasIndex(d => new { d.AcPackId, d.DispatchedAt });
    }
}

public sealed class AcActionItemConfiguration : IEntityTypeConfiguration<AcActionItem>
{
    public void Configure(EntityTypeBuilder<AcActionItem> builder)
    {
        builder.ToTable("ac_action_items");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();

        builder.Property(i => i.Title).HasMaxLength(300).IsRequired();
        builder.Property(i => i.Description);
        builder.Property(i => i.Status).HasConversion(new SnakeCaseEnumConverter<AcActionItemStatus>()).HasMaxLength(30).IsRequired();
        builder.Property(i => i.ClosureResponse);
        builder.Property(i => i.DeletionReason);
        builder.Property(i => i.Version).IsRowVersion();

        builder.HasIndex(i => i.Status);

        builder.HasQueryFilter(i => !i.IsDeleted);
    }
}

public sealed class AcCommentConfiguration : IEntityTypeConfiguration<AcComment>
{
    public void Configure(EntityTypeBuilder<AcComment> builder)
    {
        builder.ToTable("ac_comments");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.TargetType).HasConversion(new SnakeCaseEnumConverter<AcCommentTargetType>()).HasMaxLength(20).IsRequired();
        builder.Property(c => c.CommentText).IsRequired();

        builder.HasIndex(c => new { c.TargetType, c.TargetId });
    }
}

public sealed class FindingVisibilityRestrictionConfiguration : IEntityTypeConfiguration<FindingVisibilityRestriction>
{
    public void Configure(EntityTypeBuilder<FindingVisibilityRestriction> builder)
    {
        builder.ToTable("finding_visibility_restrictions");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.FindingType).HasConversion(new SnakeCaseEnumConverter<FindingType>()).HasMaxLength(30).IsRequired();
        builder.Property(r => r.AllowedUserIdsJson).IsRequired();
        builder.Property(r => r.Reason).HasMaxLength(2000);
        builder.Property(r => r.Version).IsRowVersion();

        // One restriction per finding (idempotent set-the-allow-list).
        builder.HasIndex(r => new { r.FindingType, r.FindingId }).IsUnique();
    }
}
