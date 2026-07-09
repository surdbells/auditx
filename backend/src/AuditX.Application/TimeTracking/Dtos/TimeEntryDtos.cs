namespace AuditX.Application.TimeTracking.Dtos;

/// <summary>A single logged time entry against an audit.</summary>
public sealed record TimeEntryDto(
    Guid Id,
    Guid AuditId,
    Guid UserId,
    Guid? ChecklistItemId,
    DateOnly WorkDate,
    decimal Hours,
    string Category,
    string? Notes,
    string Version);

/// <summary>Hours rolled up for one activity category.</summary>
public sealed record CategoryHoursDto(string Category, decimal Hours);

/// <summary>Hours rolled up for one contributor.</summary>
public sealed record UserHoursDto(Guid UserId, decimal Hours);

/// <summary>
/// Budget-vs-actual + composition summary for a single audit's logged time. <see cref="VarianceHours"/> and
/// <see cref="PercentConsumed"/> are null when no budget is set.
/// </summary>
public sealed record TimeEntrySummaryDto(
    Guid AuditId,
    decimal? BudgetedHours,
    decimal ActualHours,
    decimal? VarianceHours,
    double? PercentConsumed,
    int EntryCount,
    int ContributorCount,
    IReadOnlyList<CategoryHoursDto> ByCategory,
    IReadOnlyList<UserHoursDto> ByUser);
