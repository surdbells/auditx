using AuditX.Domain.Enums;
using AuditX.Domain.Planning;
using AuditX.Domain.Universe;
using AuditX.Infrastructure.Persistence.Conversions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuditX.Infrastructure.Persistence.Configurations;

public sealed class AuditableEntityConfiguration : IEntityTypeConfiguration<AuditableEntity>
{
    public void Configure(EntityTypeBuilder<AuditableEntity> builder)
    {
        builder.ToTable("audit_universe_entities");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.EntityType).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(255).IsRequired();
        builder.Property(e => e.Description);
        builder.Property(e => e.InherentRiskScoresJson).IsRequired().HasDefaultValue("{}");
        builder.Property(e => e.ResidualRiskScoresJson).IsRequired().HasDefaultValue("{}");
        builder.Property(e => e.CompositeInherentScore).HasColumnType("decimal(6,3)");
        builder.Property(e => e.CompositeResidualScore).HasColumnType("decimal(6,3)");
        builder.Property(e => e.Version).IsRowVersion();

        builder.HasIndex(e => new { e.EntityType, e.IsDeleted });
        builder.HasIndex(e => new { e.EntityType, e.LastAuditedAt });
        builder.HasIndex(e => e.CompositeResidualScore); // high-risk-gaps ordering (coverage analytics)
        builder.HasIndex(e => e.Name);
        builder.HasIndex(e => e.ParentEntityId);
        builder.HasIndex(e => e.OrgUnitId); // department / business-unit rollups (analytics)

        builder.HasOne<AuditableEntity>().WithMany().HasForeignKey(e => e.ParentEntityId).OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

public sealed class RiskDimensionConfiguration : IEntityTypeConfiguration<RiskDimension>
{
    public void Configure(EntityTypeBuilder<RiskDimension> builder)
    {
        builder.ToTable("risk_dimensions");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.Name).HasMaxLength(100).IsRequired();
        builder.Property(d => d.Weight).HasColumnType("decimal(5,2)");
        builder.Property(d => d.ScaleLabelOverridesJson);
        builder.HasIndex(d => d.Name).IsUnique();
    }
}

public sealed class EntityTypeTaxonomyConfiguration : IEntityTypeConfiguration<EntityTypeTaxonomy>
{
    public void Configure(EntityTypeBuilder<EntityTypeTaxonomy> builder)
    {
        builder.ToTable("entity_type_taxonomy");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(t => t.Name).IsUnique();
    }
}

public sealed class AnnualPlanConfiguration : IEntityTypeConfiguration<AnnualPlan>
{
    public void Configure(EntityTypeBuilder<AnnualPlan> builder)
    {
        builder.ToTable("annual_plans");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.PeriodLabel).HasMaxLength(50).IsRequired();
        builder.Property(p => p.ApprovalDecisionJson);
        builder.Property(p => p.Status)
            .HasConversion(new SnakeCaseEnumConverter<PlanStatus>())
            .HasMaxLength(30)
            .IsRequired();

        builder.HasIndex(p => p.Status);
        builder.HasIndex(p => new { p.PeriodStart, p.PeriodEnd });

        builder.HasMany(p => p.Items).WithOne().HasForeignKey(i => i.AnnualPlanId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.Items).HasField("_items").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class PlanItemConfiguration : IEntityTypeConfiguration<PlanItem>
{
    public void Configure(EntityTypeBuilder<PlanItem> builder)
    {
        builder.ToTable("plan_items");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();

        builder.Property(i => i.AuditType).HasMaxLength(100).IsRequired();
        builder.Property(i => i.EstimatedEffortDays).HasColumnType("decimal(6,1)");
        builder.Property(i => i.Status)
            .HasConversion(new SnakeCaseEnumConverter<PlanItemStatus>())
            .HasMaxLength(30)
            .IsRequired();

        builder.HasIndex(i => new { i.AnnualPlanId, i.PlannedStartDate });
        builder.HasIndex(i => i.EntityId);

        // 1:1 integrity: a live audit link belongs to exactly one plan item (nulls excluded so a deferred/
        // unlaunched item — whose link is cleared — never collides).
        builder.HasIndex(i => i.LinkedAuditId).IsUnique().HasFilter("[linked_audit_id] IS NOT NULL");
    }
}
