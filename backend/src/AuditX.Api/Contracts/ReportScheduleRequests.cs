namespace AuditX.Api.Contracts;

/// <summary>Create a recurring report schedule (D3-C). Kind must be a standalone report kind; cadence is snake_case.</summary>
public sealed record CreateReportScheduleRequest(
    string Name,
    string Kind,
    string Cadence,
    IReadOnlyList<Guid>? RecipientUserIds,
    IReadOnlyList<string>? RecipientEmails);

/// <summary>Edit a report schedule (kind is fixed at creation). Carries the rowversion for optimistic concurrency.</summary>
public sealed record UpdateReportScheduleRequest(
    string Name,
    string Cadence,
    IReadOnlyList<Guid>? RecipientUserIds,
    IReadOnlyList<string>? RecipientEmails,
    bool IsActive,
    string Version);
