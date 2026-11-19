namespace AuditX.Application.Engagement.Records;

/// <summary>
/// An audit's engagement records as the signed-in user may see them: its engagement letter, its team's independence
/// declarations and its kickoff and exit meeting records. The auditee sees a letter only once it is issued, never the
/// independence declarations, and the meeting records in full (they attended). <c>CanManage</c>: may write the letter
/// and record the meetings. <c>CanAcknowledge</c>: the auditee may acknowledge the issued letter now.
/// <c>DraftReports</c>: the completed draft reports an exit meeting can name (for those who record meetings).
/// </summary>
public sealed record EngagementRecordsDto(
    Guid AuditId,
    bool IsSelfAssessment,
    bool CanManage,
    bool IsAuditee,
    bool CanAcknowledge,
    EngagementLetterDto? Letter,
    IndependenceDto? Independence,
    IReadOnlyList<AuditMeetingDto> Meetings,
    IReadOnlyList<DraftReportOptionDto> DraftReports,
    string AuditVersion,
    ProgrammeApprovalDto? Programme = null);

/// <summary>
/// Approval of the audit programme before fieldwork (null for a self-assessment and for the auditee). <c>Required</c>:
/// the institution requires it. <c>CanApprove</c>: the signed-in user may approve it now (a manager of the audit other
/// than its lead, while it is planned and not yet approved).
/// </summary>
public sealed record ProgrammeApprovalDto(bool Required, bool Approved, Guid? ApprovedBy, DateTimeOffset? ApprovedAt, string? Note, bool CanApprove);

public sealed record EngagementLetterDto(
    Guid Id,
    string Status,
    int Revision,
    string Body,
    Guid DraftedBy,
    DateTimeOffset DraftedAt,
    Guid? IssuedBy,
    DateTimeOffset? IssuedAt,
    Guid? AcknowledgedBy,
    DateTimeOffset? AcknowledgedAt,
    string? AcknowledgementComment,
    IReadOnlyList<EngagementLetterRevisionDto> PreviousRevisions,
    string Version);

public sealed record EngagementLetterRevisionDto(
    int Revision,
    string Body,
    Guid? IssuedBy,
    DateTimeOffset? IssuedAt,
    Guid? AcknowledgedBy,
    DateTimeOffset? AcknowledgedAt,
    string? AcknowledgementComment);

/// <summary>
/// The team's independence. <c>Required</c>: fieldwork waits for it (institution setting, not a self-assessment).
/// <c>Outstanding</c>: members still to declare or with a conflict awaiting a decision. <c>CanDeclare</c>: the caller
/// is on the team and may declare now. <c>CanDecide</c>: the caller may accept a disclosed conflict (a manager of the
/// audit, never on their own declaration).
/// </summary>
public sealed record IndependenceDto(
    bool Required,
    bool Complete,
    int Outstanding,
    bool CanDeclare,
    bool CanDecide,
    IReadOnlyList<IndependenceMemberDto> Members);

/// <summary>
/// One member's standing. <c>Status</c>: pending, declared, conflict_disclosed or conflict_accepted. The conflict
/// details and the safeguards are shown only to the declarant and to those who decide on conflicts.
/// </summary>
public sealed record IndependenceMemberDto(
    Guid UserId,
    string TeamRole,
    string Status,
    Guid? DeclarationId,
    DateTimeOffset? DeclaredAt,
    string? ConflictDetails,
    Guid? ReviewedBy,
    DateTimeOffset? ReviewedAt,
    string? ReviewNote);

public sealed record AuditMeetingDto(
    Guid Id,
    string Kind,
    DateTimeOffset HeldAt,
    IReadOnlyList<Guid> AttendeeUserIds,
    IReadOnlyList<string> ExternalAttendees,
    string Minutes,
    IReadOnlyList<string> AgreedPoints,
    Guid? ReportId,
    int? ReportVersionNumber,
    Guid RecordedBy,
    DateTimeOffset RecordedAt,
    Guid? LastEditedBy,
    DateTimeOffset? LastEditedAt,
    string Version);

public sealed record DraftReportOptionDto(Guid Id, int VersionNumber, DateTimeOffset? CompletedAt);
