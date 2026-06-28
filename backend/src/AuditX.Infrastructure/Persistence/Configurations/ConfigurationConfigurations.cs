using AuditX.Domain.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuditX.Infrastructure.Persistence.Configurations;

public sealed class BankConfigurationConfiguration : IEntityTypeConfiguration<BankConfiguration>
{
    public void Configure(EntityTypeBuilder<BankConfiguration> builder)
    {
        builder.ToTable("bank_configurations");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Domain).HasMaxLength(100).IsRequired();
        builder.Property(c => c.DefinitionJson).IsRequired(); // NVARCHAR(MAX)
        builder.Property(c => c.ChangeReason).IsRequired();    // NVARCHAR(MAX)
        builder.Property(c => c.Version).IsRowVersion();

        // Per-domain incrementing version is unique (no two rows share a (domain, version_number)).
        builder.HasIndex(c => new { c.Domain, c.VersionNumber }).IsUnique();
        // Exactly one active, live version PER DOMAIN (M12): filtered unique on (domain, is_active) WHERE the row
        // is active AND not soft-deleted, so a soft-deleted active version does not block promoting a replacement.
        builder.HasIndex(c => new { c.Domain, c.IsActive }).IsUnique().HasFilter("[is_active] = 1 AND [is_deleted] = 0");

        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}
