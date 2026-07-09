using AuditX.Domain.Compliance;
using AuditX.Domain.Controls;
using AuditX.Domain.Enums;
using AuditX.Domain.Exceptions;
using AuditX.Domain.Universe;
using AuditX.Infrastructure.Persistence.Conversions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuditX.Infrastructure.Persistence.Configurations;

public sealed class ControlConfiguration : IEntityTypeConfiguration<Control>
{
    public void Configure(EntityTypeBuilder<Control> builder)
    {
        builder.ToTable("controls");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Code).HasMaxLength(50).IsRequired();
        builder.Property(c => c.Title).HasMaxLength(300).IsRequired();
        builder.Property(c => c.Description);
        builder.Property(c => c.Version).IsRowVersion();

        builder.Property(c => c.ControlType).HasConversion(new SnakeCaseEnumConverter<ControlType>()).HasMaxLength(20).IsRequired();
        builder.Property(c => c.Frequency).HasConversion(new SnakeCaseEnumConverter<ControlFrequency>()).HasMaxLength(20).IsRequired();
        builder.Property(c => c.Effectiveness).HasConversion(new SnakeCaseEnumConverter<ControlEffectiveness>()).HasMaxLength(30).IsRequired();

        builder.HasQueryFilter(c => !c.IsDeleted);
        builder.HasOne<AuditableEntity>().WithMany().HasForeignKey(c => c.AuditableEntityId).OnDelete(DeleteBehavior.SetNull);

        // Unique code among live rows (soft-deleted rows are filtered, so the index needs the guard too).
        builder.HasIndex(c => c.Code).IsUnique().HasFilter("[is_deleted] = 0");
        builder.HasIndex(c => c.OwnerUserId);
        builder.HasIndex(c => c.Effectiveness);
        builder.HasIndex(c => c.AuditableEntityId);
    }
}

public sealed class RegulationConfiguration : IEntityTypeConfiguration<Regulation>
{
    public void Configure(EntityTypeBuilder<Regulation> builder)
    {
        builder.ToTable("regulations");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Code).HasMaxLength(50).IsRequired();
        builder.Property(r => r.Name).HasMaxLength(300).IsRequired();
        builder.Property(r => r.Authority).HasMaxLength(200);
        builder.Property(r => r.Description);
        builder.Property(r => r.Category).HasMaxLength(100);
        builder.Property(r => r.Version).IsRowVersion();

        builder.HasQueryFilter(r => !r.IsDeleted);

        builder.HasIndex(r => r.Code).IsUnique().HasFilter("[is_deleted] = 0");
        builder.HasIndex(r => r.Category);
    }
}

public sealed class ExceptionControlLinkConfiguration : IEntityTypeConfiguration<ExceptionControlLink>
{
    public void Configure(EntityTypeBuilder<ExceptionControlLink> builder)
    {
        builder.ToTable("exception_control_links");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();

        builder.HasOne<AuditException>().WithMany().HasForeignKey(l => l.ExceptionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Control>().WithMany().HasForeignKey(l => l.ControlId).OnDelete(DeleteBehavior.Restrict);

        // At most one link per (finding, control).
        builder.HasIndex(l => new { l.ExceptionId, l.ControlId }).IsUnique();
        builder.HasIndex(l => l.ControlId);
    }
}

public sealed class ExceptionRegulationLinkConfiguration : IEntityTypeConfiguration<ExceptionRegulationLink>
{
    public void Configure(EntityTypeBuilder<ExceptionRegulationLink> builder)
    {
        builder.ToTable("exception_regulation_links");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();

        builder.HasOne<AuditException>().WithMany().HasForeignKey(l => l.ExceptionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Regulation>().WithMany().HasForeignKey(l => l.RegulationId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => new { l.ExceptionId, l.RegulationId }).IsUnique();
        builder.HasIndex(l => l.RegulationId);
    }
}
