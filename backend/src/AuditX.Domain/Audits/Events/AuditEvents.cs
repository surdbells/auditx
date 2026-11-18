using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Audits.Events;

public abstract record AuditEvent : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record AuditCreatedEvent(Guid AuditId, string Name, string AuditType, Guid? PlanItemId) : AuditEvent;

public sealed record AuditTransitionedEvent(Guid AuditId, AuditStatus FromStatus, AuditStatus ToStatus, string? Reason) : AuditEvent;

public sealed record AuditCompletedEvent(Guid AuditId, Guid? PlanItemId, DateOnly ActualEndDate) : AuditEvent;

public sealed record AuditCancelledEvent(Guid AuditId, string Reason) : AuditEvent;

public sealed record AuditTeamMemberAddedEvent(Guid AuditId, Guid UserId, TeamRole Role) : AuditEvent;

public sealed record AuditTeamMemberRemovedEvent(Guid AuditId, Guid UserId) : AuditEvent;

public sealed record AuditLeadTransferredEvent(Guid AuditId, Guid OutgoingLeadUserId, Guid IncomingLeadUserId) : AuditEvent;

/// <summary>
/// A pre-audit kickoff meeting was scheduled (or rescheduled). Carries the auditee so the notification
/// pipeline can give them advance notice, plus the meeting time / location for the message body.
/// </summary>
public sealed record AuditKickoffScheduledEvent(
    Guid AuditId, string AuditName, DateTimeOffset ScheduledAtUtc, string? Location, Guid AuditeeUserId, Guid LeadUserId) : AuditEvent;

// ---- M5 Execution ----

public sealed record ItemRespondedEvent(Guid AuditId, Guid ItemId, Guid ResponseId, ResponseVerdict? Verdict, bool IsDraft) : AuditEvent;

public sealed record DraftDiscardedEvent(Guid AuditId, Guid ItemId, Guid ResponseId) : AuditEvent;

/// <summary>A reviewer signed a checklist item's working paper off.</summary>
public sealed record ItemSignedOffEvent(Guid AuditId, Guid ItemId, Guid ReviewerUserId) : AuditEvent;

/// <summary>
/// A reviewer raised a note on the working papers. <c>PreparerUserId</c> — who answered the item, or the lead for a
/// note on the audit as a whole — is told.
/// </summary>
public sealed record ReviewNoteRaisedEvent(Guid AuditId, string AuditName, Guid NoteId, Guid? ItemId, Guid RaisedBy, Guid PreparerUserId) : AuditEvent;

/// <summary>The team answered a review note; the reviewer who raised it (<c>RaisedBy</c>) is told.</summary>
public sealed record ReviewNoteAnsweredEvent(Guid AuditId, string AuditName, Guid NoteId, Guid? ItemId, Guid AnsweredBy, Guid RaisedBy) : AuditEvent;

/// <summary>A reviewer cleared a review note.</summary>
public sealed record ReviewNoteClearedEvent(Guid AuditId, Guid NoteId, Guid? ItemId, Guid ClearedBy) : AuditEvent;

/// <summary>A reviewer reopened an answered review note; the person who answered it (<c>AnsweredBy</c>) is told.</summary>
public sealed record ReviewNoteReopenedEvent(Guid AuditId, string AuditName, Guid NoteId, Guid? ItemId, Guid ReopenedBy, Guid AnsweredBy) : AuditEvent;

/// <summary>A team member declared they are independent of the area the audit examines (no conflict).</summary>
public sealed record IndependenceDeclaredEvent(Guid AuditId, Guid DeclarationId, Guid UserId) : AuditEvent;

/// <summary>A team member disclosed a conflict of interest on the audit; its lead (<c>LeadUserId</c>) is told to decide on it.</summary>
public sealed record IndependenceConflictDisclosedEvent(Guid AuditId, string AuditName, Guid DeclarationId, Guid UserId, Guid LeadUserId) : AuditEvent;

/// <summary>A manager accepted a disclosed conflict with safeguards; the person who disclosed it (<c>DeclarantUserId</c>) is told.</summary>
public sealed record IndependenceConflictAcceptedEvent(Guid AuditId, string AuditName, Guid DeclarationId, Guid DeclarantUserId, Guid AcceptedBy) : AuditEvent;

public sealed record ItemAssignedEvent(Guid AuditId, Guid ItemId, Guid? AssigneeUserId) : AuditEvent;
