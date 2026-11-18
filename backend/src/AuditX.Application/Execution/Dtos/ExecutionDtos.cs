namespace AuditX.Application.Execution.Dtos;

public sealed record ChecklistResponseDto(
    Guid Id, Guid AuditId, Guid ChecklistItemId, string? Verdict, string? Comment, string? ValueJson,
    Guid ResponderUserId, bool IsDraft, int ResponseVersion, DateTimeOffset? RespondedAt, decimal? Score = null,
    string? Observation = null, string? Recommendation = null, string? SelectedOptionCode = null, string? SelectedOptionLabel = null);

/// <summary>One conclusion option the responder may pick on a verdict-based item (the organisation's own labels).</summary>
public sealed record ResponseInputOptionDto(string Code, string Label, bool IsDeficiency, bool IsNotApplicable, bool RequiresComment);

/// <summary>One point on the rating scale a Rating item is answered on.</summary>
public sealed record ResponseInputRatingPointDto(int Value, string Label);

/// <summary>
/// Everything the respond form needs to render one checklist item's input, resolved server-side: conclusion
/// <paramref name="Options"/> (verdict types), <paramref name="RatingPoints"/> (rating), <paramref name="Choices"/>
/// (multiple choice) or <paramref name="Unit"/> (numeric). Only the member relevant to the type is populated.
/// </summary>
public sealed record ResponseInputDto(
    string ResponseType,
    IReadOnlyList<ResponseInputOptionDto>? Options = null,
    IReadOnlyList<ResponseInputRatingPointDto>? RatingPoints = null,
    IReadOnlyList<string>? Choices = null,
    string? Unit = null);

/// <summary>
/// An uploaded evidence file. <c>IsRecalled</c> (with who / when / why) marks a document the person who provided it
/// took back: it stays listed as history but no longer counts as evidence.
/// </summary>
public sealed record EvidenceFileDto(
    Guid Id, Guid AuditId, string ContextType, Guid ContextId, string OriginalFilename, string MimeType,
    long SizeBytes, string Sha256Hash, Guid UploadedBy, DateTimeOffset UploadedAt, bool IsFlagged,
    bool IsRecalled = false, DateTimeOffset? RecalledAt = null, Guid? RecalledBy = null, string? RecallReason = null);

/// <summary>Returned to the controller for a verified download; streamed as a file, not enveloped.</summary>
public sealed record EvidenceDownloadResult(string FileName, string MimeType, byte[] Content);

public sealed record ChecklistProgressItemDto(
    Guid ItemId, string? SectionName, int OrderIndex, string Prompt, string ItemState,
    string? Verdict, bool IsRequired, Guid? AssignedUserId, bool HasException,
    string ResponseType, string? ResponseConfigJson, string? ValueJson, decimal? Score = null, string? RiskRating = null,
    Guid? ControlId = null);

public sealed record ChecklistProgressDto(
    int TotalItems, int RespondedItems, int InProgressItems, int NotStartedItems,
    IReadOnlyList<ChecklistProgressItemDto> Items);

public sealed record ResponseHistoryEntryDto(
    Guid Id, string EventType, Guid? ActorUserId, DateTimeOffset OccurredAtUtc, string? StateJson, string? BeforeStateJson = null);

public sealed record ReviewSummaryDto(int TotalItems, int Responded, int Pass, int Fail, int Na, int Exceptions);

/// <summary>A reviewer's note on the working papers and the team's answer. Status: open / answered / cleared.</summary>
public sealed record ReviewNoteDto(
    Guid Id, Guid? ChecklistItemId, string Body, string Status, Guid RaisedBy, DateTimeOffset RaisedAt,
    string? Answer, Guid? AnsweredBy, DateTimeOffset? AnsweredAt, Guid? ClearedBy, DateTimeOffset? ClearedAt);

/// <summary>One checklist item's review: who answered it, who signed it off, and how many notes on it are not cleared.</summary>
public sealed record ReviewItemDto(
    Guid ItemId, bool IsAnswered, Guid? ResponderUserId, Guid? ReviewedBy, DateTimeOffset? ReviewedAt, int OutstandingNotes);

/// <summary>
/// The working-paper review of an audit. <c>IsRequired</c> is false for a self-assessment (no independent reviewer);
/// <c>IsOpen</c> while the audit is in progress or under review. <c>CanReview</c> / <c>CanAnswer</c> say what the
/// caller may do. <c>Version</c> is the audit's concurrency token after the read or change.
/// </summary>
public sealed record WorkingPaperReviewDto(
    bool IsRequired, bool IsOpen, bool CanReview, bool CanAnswer,
    int TotalItems, int SignedOffItems, int ReadyForSignOff, int OpenNotes, int AnsweredNotes,
    IReadOnlyList<ReviewItemDto> Items, IReadOnlyList<ReviewNoteDto> Notes, string Version);

public sealed record FailWithoutExceptionItemDto(Guid ItemId, string Prompt, string? Comment, Guid? AssignedUserId);

public sealed record FailWithoutExceptionDto(int Count, IReadOnlyList<FailWithoutExceptionItemDto> Items);
