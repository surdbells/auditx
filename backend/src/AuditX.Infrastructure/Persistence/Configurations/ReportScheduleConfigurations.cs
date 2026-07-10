using AuditX.Domain.Enums;
using AuditX.Domain.Scheduling;
using AuditX.Infrastructure.Persistence.Conversions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuditX.Infrastructure.Persistence.Configurations;

public sealed class ReportScheduleConfiguration : IEntityTypeConfiguration<ReportSchedule>
{
    public void Configure(EntityTypeBuilder<ReportSchedule> builder)
    {
        builder.ToTable("report_schedules");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.Name).HasMaxLength(120).IsRequired();
        builder.Property(s => s.Kind).HasConversion(new SnakeCaseEnumConverter<ReportKind>()).HasMaxLength(30).IsRequired();
        builder.Property(s => s.Cadence).HasConversion(new SnakeCaseEnumConverter<ReportCadence>()).HasMaxLength(20).IsRequired();
        builder.Property(s => s.RecipientsJson).IsRequired();
        builder.Property(s => s.Version).IsRowVersion();

        // Soft-delete: live rows only in normal reads (and the due-scan).
        builder.HasQueryFilter(s => !s.IsDeleted);

        // The runner scans active, due schedules: index (is_active, next_run_at) over live rows.
        builder.HasIndex(s => new { s.IsActive, s.NextRunAt }).HasFilter("[is_deleted] = 0");
    }
}
