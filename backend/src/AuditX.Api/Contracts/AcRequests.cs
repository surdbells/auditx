namespace AuditX.Api.Contracts;

/// <summary>Body for POST /ac-packs/generate. HTML is always produced; <c>docx</c> additionally requests DOCX.</summary>
public sealed record GenerateAcPackRequest(DateOnly PeriodStart, DateOnly PeriodEnd, string? AcMeetingLabel, bool? Docx);

/// <summary>Body for PATCH /ac-packs/{id}/cia-text. Sets/replaces the CIA supplementary narrative (PendingReview only).</summary>
public sealed record UpdateAcPackCiaTextRequest(string? SupplementaryText);

/// <summary>Body for POST /ac-packs/{id}/approve. Optional last-mile supplementary text edit before approval.</summary>
public sealed record ApproveAcPackRequest(string? SupplementaryText);

/// <summary>Body for POST /ac-action-items. <c>dueDate</c> and <c>assignedToUserId</c> are optional.</summary>
public sealed record CreateAcActionItemRequest(string Title, string? Description, Guid? AssignedToUserId, DateOnly? DueDate);

/// <summary>
/// Body for PATCH /ac-action-items/{id}. A non-blank <c>closureResponse</c> closes the item (blank → 422);
/// otherwise <c>markInProgress</c> moves it open → in_progress.
/// </summary>
public sealed record UpdateAcActionItemRequest(bool? MarkInProgress, string? ClosureResponse);

/// <summary>Body for POST /ac-comments. Polymorphic target: <c>targetType</c> is one of plan|pack|finding.</summary>
public sealed record AddAcCommentRequest(string TargetType, Guid TargetId, string Comment);

/// <summary>Body for POST /findings/{type}/{id}/restrict-visibility. The allow-list of directory users who may see the finding's detail.</summary>
public sealed record RestrictFindingVisibilityRequest(IReadOnlyList<Guid>? AllowedUserIds, string? Reason);
