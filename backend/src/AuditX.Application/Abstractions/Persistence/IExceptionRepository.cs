using AuditX.Application.Common.Models;
using AuditX.Domain.Enums;
using AuditX.Domain.Exceptions;

namespace AuditX.Application.Abstractions.Persistence;

/// <summary>Open and overdue finding counts for one owner (management-line roll-up).</summary>
public readonly record struct OwnerFindingCounts(int Open, int Overdue);

/// <summary>Filters for the cross-audit exception tracker (US-M6-019).</summary>
public sealed record ExceptionSearchFilter(
    ExceptionStatus? Status, ExceptionSeverity? Severity, Guid? OwnerUserId, Guid? AuditableEntityId,
    Guid? AuditId, string? Category, bool? IsRecurrence, bool? IsOverdue, DateOnly AsOfDate, string? Search = null,
    Guid? AnnualPlanId = null, DateTimeOffset? RaisedFrom = null, DateTimeOffset? RaisedTo = null,
    string? RootCauseCategory = null, string? NonConformanceCategory = null,
    bool? IsOpen = null);

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

    Task<PagedResult<AuditException>> SearchAsync(ExceptionSearchFilter filter, PageSpec page, CancellationToken cancellationToken = default);

    /// <summary>Most recent closed exception for the same entity + category within the recurrence window (US-M6-017).</summary>
    Task<AuditException?> FindClosedForRecurrenceAsync(Guid auditableEntityId, string? category, DateTimeOffset sinceUtc, CancellationToken cancellationToken = default);

    /// <summary>Open findings whose MAP response is overdue (past its due/target date) and not yet escalated — for the
    /// daily reporting-line escalation job.</summary>
    Task<IReadOnlyList<AuditException>> ListOverdueForEscalationAsync(DateOnly today, CancellationToken cancellationToken = default);

    /// <summary>Per-owner open + overdue finding counts for a set of owners — the fact behind the management-line roll-up.</summary>
    Task<IReadOnlyDictionary<Guid, OwnerFindingCounts>> CountOpenAndOverdueByOwnersAsync(
        IReadOnlyCollection<Guid> ownerIds, DateOnly today, CancellationToken cancellationToken = default);

    void Add(AuditException exception);
}

/// <summary>Persistence for bank-configurable <see cref="ExceptionRaisingRule"/>s — one per <see cref="ResponseType"/>.</summary>
public interface IExceptionRaisingRuleRepository
{
    Task<ExceptionRaisingRule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ExceptionRaisingRule?> GetByResponseTypeAsync(ResponseType responseType, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExceptionRaisingRule>> GetAllAsync(CancellationToken cancellationToken = default);

    void Add(ExceptionRaisingRule rule);
}

/// <summary>Enriched root-cause-gap↔exception link row (joined to the finding for display).</summary>
public sealed record RootCauseGapLinkedExceptionRow(Guid LinkId, Guid ExceptionId, string Title, ExceptionSeverity Severity, ExceptionStatus Status);

/// <summary>Persistence for <see cref="RootCauseGap"/> aggregates (with their exception links loaded).</summary>
public interface IRootCauseGapRepository
{
    Task<RootCauseGap?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PagedResult<RootCauseGap>> SearchAsync(RootCauseGapStatus? status, string? search, PageSpec page, CancellationToken cancellationToken = default);

    /// <summary>The findings linked to a gap, joined to the exception for title/severity/status display.</summary>
    Task<IReadOnlyList<RootCauseGapLinkedExceptionRow>> ListLinkedExceptionsAsync(Guid gapId, CancellationToken cancellationToken = default);

    void Add(RootCauseGap gap);
}
