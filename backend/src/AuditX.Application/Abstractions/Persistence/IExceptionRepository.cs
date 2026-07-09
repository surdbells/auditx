using AuditX.Application.Common.Models;
using AuditX.Domain.Enums;
using AuditX.Domain.Exceptions;

namespace AuditX.Application.Abstractions.Persistence;

/// <summary>Filters for the cross-audit exception tracker (US-M6-019).</summary>
public sealed record ExceptionSearchFilter(
    ExceptionStatus? Status, ExceptionSeverity? Severity, Guid? OwnerUserId, Guid? AuditableEntityId,
    Guid? AuditId, string? Category, bool? IsRecurrence, bool? IsOverdue, DateOnly AsOfDate, string? Search = null,
    Guid? AnnualPlanId = null, DateTimeOffset? RaisedFrom = null, DateTimeOffset? RaisedTo = null);

/// <summary>Flat, join-resolved export row for the cross-audit finding-register CSV.</summary>
public sealed record ExceptionExportRow(
    Guid Id, Guid AuditId, string AuditName, string Title, ExceptionSeverity Severity, string? Category,
    ExceptionStatus Status, bool CiaPending, bool IsRecurrence, Guid OwnerUserId, Guid? AuditableEntityId,
    DateTimeOffset RaisedAt, DateOnly TargetDate, int MapActionCount, int CompletedMapActionCount,
    decimal? FinancialImpact, string? FinancialImpactCurrency);

public interface IExceptionRepository
{
    Task<AuditException?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Streams every finding matching the filter (join-resolved, newest-first) for the register CSV export.</summary>
    IAsyncEnumerable<ExceptionExportRow> StreamForExportAsync(ExceptionSearchFilter filter, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditException>> ListByAuditAsync(Guid auditId, ExceptionStatus? status, CancellationToken cancellationToken = default);

    /// <summary>All exceptions for an audit WITH their MAP actions eagerly loaded (for report assembly — M8).</summary>
    Task<IReadOnlyList<AuditException>> ListByAuditWithMapActionsAsync(Guid auditId, CancellationToken cancellationToken = default);

    Task<CursorPage<AuditException>> SearchAsync(ExceptionSearchFilter filter, PageRequest page, CancellationToken cancellationToken = default);

    /// <summary>Most recent closed exception for the same entity + category within the recurrence window (US-M6-017).</summary>
    Task<AuditException?> FindClosedForRecurrenceAsync(Guid auditableEntityId, string? category, DateTimeOffset sinceUtc, CancellationToken cancellationToken = default);

    void Add(AuditException exception);
}
