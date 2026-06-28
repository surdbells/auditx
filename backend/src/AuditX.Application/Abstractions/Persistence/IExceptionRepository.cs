using AuditX.Application.Common.Models;
using AuditX.Domain.Enums;
using AuditX.Domain.Exceptions;

namespace AuditX.Application.Abstractions.Persistence;

/// <summary>Filters for the cross-audit exception tracker (US-M6-019).</summary>
public sealed record ExceptionSearchFilter(
    ExceptionStatus? Status, ExceptionSeverity? Severity, Guid? OwnerUserId, Guid? AuditableEntityId,
    Guid? AuditId, string? Category, bool? IsRecurrence, bool? IsOverdue, DateOnly AsOfDate, string? Search = null);

public interface IExceptionRepository
{
    Task<AuditException?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditException>> ListByAuditAsync(Guid auditId, ExceptionStatus? status, CancellationToken cancellationToken = default);

    /// <summary>All exceptions for an audit WITH their MAP actions eagerly loaded (for report assembly — M8).</summary>
    Task<IReadOnlyList<AuditException>> ListByAuditWithMapActionsAsync(Guid auditId, CancellationToken cancellationToken = default);

    Task<CursorPage<AuditException>> SearchAsync(ExceptionSearchFilter filter, PageRequest page, CancellationToken cancellationToken = default);

    /// <summary>Most recent closed exception for the same entity + category within the recurrence window (US-M6-017).</summary>
    Task<AuditException?> FindClosedForRecurrenceAsync(Guid auditableEntityId, string? category, DateTimeOffset sinceUtc, CancellationToken cancellationToken = default);

    void Add(AuditException exception);
}
