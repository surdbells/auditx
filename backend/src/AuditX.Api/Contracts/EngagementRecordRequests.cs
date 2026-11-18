namespace AuditX.Api.Contracts;

/// <summary>Write or change the engagement letter. <c>LetterVersion</c> is required once a letter exists.</summary>
public sealed record SaveEngagementLetterRequest(string Body, string? LetterVersion);

public sealed record IssueEngagementLetterRequest(string LetterVersion);

public sealed record AcknowledgeEngagementLetterRequest(string? Comment, string LetterVersion);

/// <summary>The caller's independence declaration; <c>Version</c> is the audit's.</summary>
public sealed record DeclareIndependenceRequest(bool HasConflict, string? ConflictDetails, string Version);

/// <summary>Accept a disclosed conflict with the safeguards; <c>Version</c> is the audit's.</summary>
public sealed record AcceptIndependenceConflictRequest(string Safeguards, string Version);

/// <summary>The minutes of the kickoff or exit meeting. <c>MeetingVersion</c> is required when correcting a record.</summary>
public sealed record RecordAuditMeetingRequest(
    DateTimeOffset HeldAt,
    IReadOnlyList<Guid>? AttendeeUserIds,
    IReadOnlyList<string>? ExternalAttendees,
    string Minutes,
    IReadOnlyList<string>? AgreedPoints,
    Guid? ReportId,
    string? MeetingVersion);
