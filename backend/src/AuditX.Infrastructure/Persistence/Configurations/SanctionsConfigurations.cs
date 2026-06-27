using AuditX.Domain.Enums;
using AuditX.Domain.Exceptions;
using AuditX.Domain.Sanctions;
using AuditX.Infrastructure.Persistence.Conversions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuditX.Infrastructure.Persistence.Configurations;

public sealed class SanctionsCaseConfiguration : IEntityTypeConfiguration<SanctionsCase>
{
    public void Configure(EntityTypeBuilder<SanctionsCase> builder)
    {
        builder.ToTable("sanctions_cases");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Status).HasConversion(new SnakeCaseEnumConverter<SanctionsCaseStatus>()).HasMaxLength(40).IsRequired();
        builder.Property(c => c.Severity).HasConversion(new SnakeCaseEnumConverter<ExceptionSeverity>()).HasMaxLength(20).IsRequired();
        builder.Property(c => c.Category).HasMaxLength(100);
        builder.Property(c => c.Recommendation);
        builder.Property(c => c.GridRecommendedRange);
        builder.Property(c => c.DeviationReason);
        builder.Property(c => c.HrOutcomeJson);
        builder.Property(c => c.DcDecisionJson);
        builder.Property(c => c.Version).IsRowVersion();

        // Decoupling (FR-M7-011): FK to the originating exception, Restrict — closing the exception never cascades here.
        builder.HasOne<AuditException>().WithMany().HasForeignKey(c => c.ExceptionId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(c => c.TeamMembers).WithOne().HasForeignKey(m => m.SanctionsCaseId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(c => c.TeamMembers).HasField("_teamMembers").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(c => c.Status);
        builder.HasIndex(c => c.ExceptionId);
        builder.HasIndex(c => c.SubjectUserId);
    }
}

public sealed class SanctionsCaseTeamMemberConfiguration : IEntityTypeConfiguration<SanctionsCaseTeamMember>
{
    public void Configure(EntityTypeBuilder<SanctionsCaseTeamMember> builder)
    {
        builder.ToTable("sanctions_case_team");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.RoleMarker).HasMaxLength(30).IsRequired();

        builder.HasIndex(m => m.SanctionsCaseId);
        builder.HasIndex(m => new { m.SanctionsCaseId, m.UserId }).IsUnique();
    }
}

public sealed class SanctionsGridVersionConfiguration : IEntityTypeConfiguration<SanctionsGridVersion>
{
    public void Configure(EntityTypeBuilder<SanctionsGridVersion> builder)
    {
        builder.ToTable("sanctions_grid_versions");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).ValueGeneratedNever();

        builder.Property(g => g.GridDefinitionJson).IsRequired();
        builder.Property(g => g.ActivationReason);
        builder.Property(g => g.Version).IsRowVersion();

        builder.HasIndex(g => g.VersionNumber).IsUnique();
        // Exactly one active version bank-wide (US-M7-002): filtered unique index on is_active = 1.
        builder.HasIndex(g => g.IsActive).IsUnique().HasFilter("[is_active] = 1");
    }
}

public sealed class SanctionsAppealConfiguration : IEntityTypeConfiguration<SanctionsAppeal>
{
    public void Configure(EntityTypeBuilder<SanctionsAppeal> builder)
    {
        builder.ToTable("sanctions_appeals");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.Basis).IsRequired();
        builder.Property(a => a.Status).HasConversion(new SnakeCaseEnumConverter<AppealStatus>()).HasMaxLength(30).IsRequired();
        builder.Property(a => a.DecisionJson);
        builder.Property(a => a.Version).IsRowVersion();

        builder.HasOne<SanctionsCase>().WithMany().HasForeignKey(a => a.SanctionsCaseId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.SanctionsCaseId);
        builder.HasIndex(a => new { a.RoutedToUserId, a.Status });
    }
}
