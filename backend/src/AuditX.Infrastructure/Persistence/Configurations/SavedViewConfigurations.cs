using AuditX.Domain.SavedViews;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuditX.Infrastructure.Persistence.Configurations;

public sealed class SavedViewConfiguration : IEntityTypeConfiguration<SavedView>
{
    public void Configure(EntityTypeBuilder<SavedView> builder)
    {
        builder.ToTable("saved_views");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();

        builder.Property(v => v.ViewKey).HasMaxLength(60).IsRequired();
        builder.Property(v => v.Name).HasMaxLength(120).IsRequired();
        builder.Property(v => v.ParametersJson).HasMaxLength(8000).IsRequired();
        builder.Property(v => v.Version).IsRowVersion();

        // Soft-delete: live rows only in normal reads.
        builder.HasQueryFilter(v => !v.IsDeleted);

        // The primary listing predicate: an owner's views for a screen, and the shared views for a screen.
        builder.HasIndex(v => new { v.OwnerUserId, v.ViewKey });
        builder.HasIndex(v => new { v.ViewKey, v.IsShared });

        // A user cannot have two live views with the same name on the same screen.
        builder.HasIndex(v => new { v.OwnerUserId, v.ViewKey, v.Name })
            .IsUnique()
            .HasFilter("[is_deleted] = 0");
    }
}
