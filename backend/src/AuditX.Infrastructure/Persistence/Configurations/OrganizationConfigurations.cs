using AuditX.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuditX.Infrastructure.Persistence.Configurations;

public sealed class OrgUnitConfiguration : IEntityTypeConfiguration<OrgUnit>
{
    public void Configure(EntityTypeBuilder<OrgUnit> builder)
    {
        builder.ToTable("org_units");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();

        builder.Property(o => o.Name).HasMaxLength(200).IsRequired();
        builder.Property(o => o.Code).HasMaxLength(40).IsRequired();
        builder.Property(o => o.Version).IsRowVersion();

        builder.HasIndex(o => o.Code).IsUnique();
        builder.HasIndex(o => o.ParentOrgUnitId);
    }
}
