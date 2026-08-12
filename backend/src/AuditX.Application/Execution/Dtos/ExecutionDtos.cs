namespace AuditX.Application.Execution.Dtos;

public sealed record ChecklistResponseDto(
    Guid Id, Guid AuditId, Guid ChecklistItemId, string? Verdict, string? Comment, string? ValueJson,
    Guid ResponderUserId, bool IsDraft, int ResponseVersion, DateTimeOffset? RespondedAt, decimal? Score = null,
    string? Observation = null, string? Recommendation = null, string? SelectedOptionCode = null, string? SelectedOptionLabel = null);

public sealed record EvidenceFileDto(
    Guid Id, Guid AuditId, string ContextType, Guid ContextId, string OriginalFilename, string MimeType,
    long SizeBytes, string Sha256Hash, Guid UploadedBy, DateTimeOffset UploadedAt, bool IsFlagged);

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

public sealed record FailWithoutExceptionItemDto(Guid ItemId, string Prompt, string? Comment, Guid? AssignedUserId);

public sealed record FailWithoutExceptionDto(int Count, IReadOnlyList<FailWithoutExceptionItemDto> Items);
