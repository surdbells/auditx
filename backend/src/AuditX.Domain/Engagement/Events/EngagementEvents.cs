using AuditX.Domain.Common;

namespace AuditX.Domain.Engagement.Events;

public abstract record EngagementEvent : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>An engagement letter was issued; the audit's auditee (<c>AuditeeUserId</c>) is asked to acknowledge it.</summary>
public sealed record EngagementLetterIssuedEvent(Guid LetterId, Guid AuditId, string AuditName, int Revision, Guid AuditeeUserId, Guid IssuedBy) : EngagementEvent;

/// <summary>The auditee acknowledged the engagement letter; whoever issued it (<c>IssuedBy</c>) is told.</summary>
public sealed record EngagementLetterAcknowledgedEvent(Guid LetterId, Guid AuditId, string AuditName, int Revision, Guid AcknowledgedBy, Guid IssuedBy) : EngagementEvent;

/// <summary>The minutes of a kickoff or exit meeting were recorded (or corrected); the auditee (<c>AuditeeUserId</c>) is told.</summary>
public sealed record AuditMeetingRecordedEvent(Guid MeetingId, Guid AuditId, string AuditName, string Meeting, Guid AuditeeUserId, bool IsCorrection) : EngagementEvent;
