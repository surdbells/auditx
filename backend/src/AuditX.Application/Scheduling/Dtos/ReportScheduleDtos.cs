namespace AuditX.Application.Scheduling.Dtos;

/// <summary>A recurring report schedule (D3-C). Recipients are surfaced as resolved lists (not the raw JSON).</summary>
public sealed record ReportScheduleDto(
    Guid Id,
    string Name,
    string Kind,
    string Cadence,
    IReadOnlyList<Guid> RecipientUserIds,
    IReadOnlyList<string> RecipientEmails,
    bool IsActive,
    DateTimeOffset NextRunAt,
    DateTimeOffset? LastRunAt,
    Guid? LastReportId,
    Guid CreatedByUserId,
    string Version);
