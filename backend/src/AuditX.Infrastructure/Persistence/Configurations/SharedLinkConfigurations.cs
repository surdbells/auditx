using AuditX.Domain.Enums;
using AuditX.Domain.Sharing;
using AuditX.Infrastructure.Persistence.Conversions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuditX.Infrastructure.Persistence.Configurations;

public sealed class SharedLinkConfiguration : IEntityTypeConfiguration<SharedLink>
{
    public void Configure(EntityTypeBuilder<SharedLink> builder)
    {
        builder.ToTable("shared_links");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();

        // base64url slugs are case-sensitive; a case-sensitive collation keeps lookups exact-match and preserves the
        // slug's full entropy (the DB default is case-insensitive, which would fold e.g. 'aB' and 'Ab' together).
        builder.Property(l => l.Slug).HasMaxLength(64).IsRequired().UseCollation("Latin1_General_100_BIN2");
        builder.Property(l => l.TargetType)
            .HasConversion(new SnakeCaseEnumConverter<SharedLinkTargetType>())
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(l => l.Version).IsRowVersion();

        // Soft-delete: live rows only in normal reads.
        builder.HasQueryFilter(l => !l.IsDeleted);

        // Slug is the lookup key and must be globally unique among live links.
        builder.HasIndex(l => l.Slug).IsUnique().HasFilter("[is_deleted] = 0");
        builder.HasIndex(l => new { l.TargetType, l.TargetId });
    }
}
