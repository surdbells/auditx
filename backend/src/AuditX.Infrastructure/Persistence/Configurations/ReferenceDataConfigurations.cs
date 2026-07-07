using AuditX.Domain.ReferenceData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuditX.Infrastructure.Persistence.Configurations;

public sealed class ReferenceDataItemConfiguration : IEntityTypeConfiguration<ReferenceDataItem>
{
    public void Configure(EntityTypeBuilder<ReferenceDataItem> builder)
    {
        builder.ToTable("reference_data_items");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();

        builder.Property(i => i.Category).HasMaxLength(64).IsRequired();
        builder.Property(i => i.Code).HasMaxLength(64).IsRequired();
        builder.Property(i => i.Label).HasMaxLength(160).IsRequired();
        builder.Property(i => i.Description).HasMaxLength(500);
        builder.Property(i => i.RowVersion).IsRowVersion();

        // A code is unique within its category among live rows: filtered unique on (category, code) WHERE not
        // soft-deleted, so archiving (soft-delete) a code frees it to be re-created later.
        builder.HasIndex(i => new { i.Category, i.Code }).IsUnique().HasFilter("[is_deleted] = 0");
        // Category-scoped list reads order by (sort_order, label); index the category for the common list query.
        builder.HasIndex(i => new { i.Category, i.SortOrder });

        builder.HasQueryFilter(i => !i.IsDeleted);
    }
}
