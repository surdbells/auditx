using AuditX.Domain.Analytics;
using AuditX.Domain.Enums;
using AuditX.Infrastructure.Persistence.Conversions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuditX.Infrastructure.Persistence.Configurations;

public sealed class DashboardConfiguration : IEntityTypeConfiguration<Dashboard>
{
    public void Configure(EntityTypeBuilder<Dashboard> builder)
    {
        builder.ToTable("dashboards");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.Slug).HasMaxLength(100).IsRequired();
        builder.Property(d => d.Name).HasMaxLength(200).IsRequired();
        builder.Property(d => d.Description).HasMaxLength(1000);
        builder.Property(d => d.PermissionRequired).HasMaxLength(100);
        builder.Property(d => d.ConfigurationVersion).IsRequired();
        builder.Property(d => d.Version).IsRowVersion();

        builder.HasMany(d => d.Widgets).WithOne().HasForeignKey(w => w.DashboardId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(d => d.Widgets).HasField("_widgets").UsePropertyAccessMode(PropertyAccessMode.Field);

        // Slug is unique among live (non-deleted) dashboards.
        builder.HasIndex(d => d.Slug).IsUnique();

        builder.HasQueryFilter(d => !d.IsDeleted);
    }
}

public sealed class DashboardWidgetConfiguration : IEntityTypeConfiguration<DashboardWidget>
{
    public void Configure(EntityTypeBuilder<DashboardWidget> builder)
    {
        builder.ToTable("dashboard_widgets");
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).ValueGeneratedNever();

        builder.Property(w => w.WidgetType).HasConversion(new SnakeCaseEnumConverter<WidgetType>()).HasMaxLength(20).IsRequired();
        builder.Property(w => w.MetricKey).HasMaxLength(100).IsRequired();
        builder.Property(w => w.Title).HasMaxLength(200).IsRequired();
        builder.Property(w => w.ConfigJson);
        builder.Property(w => w.Version).IsRowVersion();

        builder.HasIndex(w => new { w.DashboardId, w.Position });
    }
}

public sealed class RecurrenceClusterConfiguration : IEntityTypeConfiguration<RecurrenceCluster>
{
    public void Configure(EntityTypeBuilder<RecurrenceCluster> builder)
    {
        builder.ToTable("recurrence_clusters");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Category).HasMaxLength(100);
        builder.Property(c => c.MemberExceptionIdsJson).IsRequired().HasDefaultValue("[]");
        builder.Property(c => c.Version).IsRowVersion();

        // One tracked cluster per (entity, category). Category can be NULL; SQL Server's filtered unique
        // index over a nullable column treats NULLs as distinct, so a category-less cluster is upserted by
        // the service (which keys on the normalised category) rather than relying on the unique index there.
        builder.HasIndex(c => new { c.AuditableEntityId, c.Category }).IsUnique();
    }
}
