using AuditX.Domain.Audits;
using AuditX.Domain.Engagement;
using AuditX.Domain.Enums;
using AuditX.Domain.Reports;
using AuditX.Infrastructure.Persistence.Conversions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuditX.Infrastructure.Persistence.Configurations;

public sealed class EngagementLetterConfiguration : IEntityTypeConfiguration<EngagementLetter>
{
    public void Configure(EntityTypeBuilder<EngagementLetter> builder)
    {
        builder.ToTable("engagement_letters");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();

        builder.Property(l => l.Body).HasMaxLength(EngagementLetter.MaxBodyLength).IsRequired();
        builder.Property(l => l.AcknowledgementComment).HasMaxLength(EngagementLetter.MaxCommentLength);
        builder.Property(l => l.Version).IsRowVersion();
        builder.Property(l => l.Status)
            .HasConversion(new SnakeCaseEnumConverter<EngagementLetterStatus>())
            .HasMaxLength(20)
            .IsRequired();

        // One letter per audit (Restrict — audits are not hard-deleted).
        builder.HasOne<Audit>().WithMany().HasForeignKey(l => l.AuditId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(l => l.AuditId).IsUnique();

        builder.HasMany(l => l.PreviousRevisions).WithOne().HasForeignKey("EngagementLetterId").OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(l => l.PreviousRevisions).HasField("_previousRevisions").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class EngagementLetterRevisionConfiguration : IEntityTypeConfiguration<EngagementLetterRevision>
{
    public void Configure(EntityTypeBuilder<EngagementLetterRevision> builder)
    {
        builder.ToTable("engagement_letter_revisions");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Body).HasMaxLength(EngagementLetter.MaxBodyLength).IsRequired();
        builder.Property(r => r.AcknowledgementComment).HasMaxLength(EngagementLetter.MaxCommentLength);
        builder.HasIndex("EngagementLetterId", nameof(EngagementLetterRevision.Revision)).IsUnique();
    }
}

public sealed class AuditMeetingConfiguration : IEntityTypeConfiguration<AuditMeeting>
{
    public void Configure(EntityTypeBuilder<AuditMeeting> builder)
    {
        builder.ToTable("audit_meetings");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.Minutes).HasMaxLength(AuditMeeting.MaxMinutesLength).IsRequired();
        builder.PrimitiveCollection(m => m.AttendeeUserIds).HasColumnName("attendee_user_ids");
        builder.PrimitiveCollection(m => m.ExternalAttendees).HasColumnName("external_attendees");
        builder.PrimitiveCollection(m => m.AgreedPoints).HasColumnName("agreed_points");
        builder.Property(m => m.Version).IsRowVersion();
        builder.Property(m => m.Kind)
            .HasConversion(new SnakeCaseEnumConverter<MeetingKind>())
            .HasMaxLength(20)
            .IsRequired();

        builder.HasOne<Audit>().WithMany().HasForeignKey(m => m.AuditId).OnDelete(DeleteBehavior.Restrict);
        // The draft report the exit meeting discussed (reports are soft-deleted, never removed).
        builder.HasOne<Report>().WithMany().HasForeignKey(m => m.ReportId).OnDelete(DeleteBehavior.Restrict);

        // One kickoff and one exit meeting record per audit.
        builder.HasIndex(m => new { m.AuditId, m.Kind }).IsUnique();
    }
}
