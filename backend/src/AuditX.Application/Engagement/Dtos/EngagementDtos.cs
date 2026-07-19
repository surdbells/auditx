namespace AuditX.Application.Engagement.Dtos;

/// <summary>
/// A next step the current user can take on an engagement. <see cref="Kind"/> is "route" (navigate to
/// <see cref="Route"/>) or "transition" (advance the audit to <see cref="TargetState"/> via the shared
/// transition flow). The frontend may localise by <see cref="Code"/>; <see cref="Label"/> is an English fallback.
/// </summary>
public sealed record NextActionDto(
    string Code,
    string Label,
    string Kind,
    string? Route,
    string? TargetState,
    string PermissionKey);

/// <summary>One stage in the ordered lifecycle timeline. <see cref="State"/> is done | current | pending.</summary>
public sealed record LifecycleStageDto(string Code, string Label, string State);

/// <summary>The full journey for a single engagement (per-engagement view).</summary>
public sealed record EngagementJourneyDto(
    Guid AuditId,
    string Name,
    string AuditType,
    string Status,
    string Stage,
    int ProgressPercent,
    int OpenExceptionCount,
    IReadOnlyList<LifecycleStageDto> Stages,
    IReadOnlyList<NextActionDto> NextActions);

/// <summary>A board row: an engagement with its current stage and the signed-in user's next action(s).</summary>
public sealed record EngagementBoardItemDto(
    Guid AuditId,
    string Name,
    string AuditType,
    string Status,
    string Stage,
    int ProgressPercent,
    int OpenExceptionCount,
    bool WaitingOnMe,
    IReadOnlyList<NextActionDto> NextActions);
