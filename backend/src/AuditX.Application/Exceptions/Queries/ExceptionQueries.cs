using System.Globalization;
using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Csv;
using AuditX.Application.Common.Enums;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Common.Models;
using AuditX.Application.Exceptions.Commands;
using AuditX.Application.Exceptions.Dtos;
using AuditX.Application.Exceptions.Mapping;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Enums;

namespace AuditX.Application.Exceptions.Queries;

public sealed record GetExceptionByIdQuery(Guid Id) : IQuery<ExceptionDto>;

public sealed class GetExceptionByIdQueryHandler(
    IExceptionRepository exceptions, IAuditRepository audits, IPermissionResolver permissions, ICurrentUser currentUser, IClock clock)
    : IQueryHandler<GetExceptionByIdQuery, ExceptionDto>
{
    public async Task<ExceptionDto> Handle(GetExceptionByIdQuery query, CancellationToken cancellationToken)
    {
        var exception = await exceptions.GetByIdAsync(query.Id, cancellationToken) ?? throw new NotFoundException("Exception", query.Id);
        var auditEntity = await audits.GetByIdAsync(exception.AuditId, cancellationToken) ?? throw new NotFoundException("Audit", exception.AuditId);
        await ExceptionAccess.EnsureCanAccessAsync(auditEntity, currentUser.UserId, permissions, cancellationToken);
        return exception.ToDto(DateOnly.FromDateTime(clock.UtcNow.UtcDateTime));
    }
}

public sealed record ListExceptionsForAuditQuery(Guid AuditId, string? Status) : IQuery<IReadOnlyList<ExceptionListItemDto>>;

public sealed class ListExceptionsForAuditQueryHandler(
    IExceptionRepository exceptions, IAuditRepository audits, IPermissionResolver permissions, ICurrentUser currentUser, IClock clock)
    : IQueryHandler<ListExceptionsForAuditQuery, IReadOnlyList<ExceptionListItemDto>>
{
    public async Task<IReadOnlyList<ExceptionListItemDto>> Handle(ListExceptionsForAuditQuery query, CancellationToken cancellationToken)
    {
        var auditEntity = await audits.GetByIdAsync(query.AuditId, cancellationToken) ?? throw new NotFoundException("Audit", query.AuditId);
        await ExceptionAccess.EnsureCanAccessAsync(auditEntity, currentUser.UserId, permissions, cancellationToken);

        var status = ParseStatus(query.Status);
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var list = await exceptions.ListByAuditAsync(query.AuditId, status, cancellationToken);
        return list.Select(e => e.ToListItemDto(today)).ToArray();
    }

    internal static ExceptionStatus? ParseStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        // The DTO surfaces a derived 'pending_cia_approval' label (Critical on CIA hold); accept it as a
        // filter by mapping back to its real persisted status so a client can round-trip the value it received.
        if (value.Replace("_", string.Empty).Equals("pendingciaapproval", StringComparison.OrdinalIgnoreCase))
        {
            return ExceptionStatus.PendingClosure;
        }

        return Enum.TryParse<ExceptionStatus>(value.Replace("_", string.Empty), ignoreCase: true, out var s)
            ? s
            : throw new ConflictException("exception.invalid_status", $"Unknown status '{value}'.");
    }
}

public sealed record SearchExceptionsQuery(
    string? Status, string? Severity, Guid? OwnerUserId, Guid? AuditableEntityId, Guid? AuditId,
    string? Category, bool? IsRecurrence, bool? IsOverdue, string? Search,
    Guid? AnnualPlanId, DateTimeOffset? RaisedFrom, DateTimeOffset? RaisedTo, int? Page, int? PageSize)
    : IQuery<PagedResult<ExceptionListItemDto>>;

public sealed class SearchExceptionsQueryHandler(IExceptionRepository exceptions, IClock clock)
    : IQueryHandler<SearchExceptionsQuery, PagedResult<ExceptionListItemDto>>
{
    public async Task<PagedResult<ExceptionListItemDto>> Handle(SearchExceptionsQuery query, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        ExceptionSeverity? severity = string.IsNullOrWhiteSpace(query.Severity)
            ? null
            : Enum.TryParse<ExceptionSeverity>(query.Severity.Replace("_", string.Empty), ignoreCase: true, out var sv)
                ? sv
                : throw new ConflictException("exception.invalid_severity", $"Unknown severity '{query.Severity}'.");

        var filter = new ExceptionSearchFilter(
            ListExceptionsForAuditQueryHandler.ParseStatus(query.Status), severity, query.OwnerUserId,
            query.AuditableEntityId, query.AuditId, query.Category, query.IsRecurrence, query.IsOverdue, today, query.Search,
            query.AnnualPlanId, query.RaisedFrom, query.RaisedTo);

        var page = PageSpec.Of(query.Page, query.PageSize);
        var result = await exceptions.SearchAsync(filter, page, cancellationToken);
        return result.Map(e => e.ToListItemDto(today));
    }
}

// ---- Finding-register CSV export (cross-audit) ----

public sealed record ExportFindingRegisterQuery(
    string? Status, string? Severity, Guid? OwnerUserId, Guid? AuditableEntityId, Guid? AuditId,
    string? Category, bool? IsRecurrence, bool? IsOverdue, string? Search,
    Guid? AnnualPlanId, DateTimeOffset? RaisedFrom, DateTimeOffset? RaisedTo) : IQuery<CsvExportResult>;

public sealed class ExportFindingRegisterQueryHandler(IExceptionRepository exceptions, IClock clock, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : IQueryHandler<ExportFindingRegisterQuery, CsvExportResult>
{
    private static readonly string[] Header =
    [
        "exception_id", "audit_id", "audit_name", "title", "severity", "category", "status", "cia_pending",
        "is_recurrence", "owner_user_id", "auditable_entity_id", "raised_at_utc", "target_date", "is_overdue",
        "financial_impact", "financial_impact_currency", "map_action_count", "completed_map_action_count",
    ];

    public async Task<CsvExportResult> Handle(ExportFindingRegisterQuery query, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        ExceptionSeverity? severity = string.IsNullOrWhiteSpace(query.Severity)
            ? null
            : Enum.TryParse<ExceptionSeverity>(query.Severity.Replace("_", string.Empty), ignoreCase: true, out var sv)
                ? sv
                : throw new ConflictException("exception.invalid_severity", $"Unknown severity '{query.Severity}'.");

        var filter = new ExceptionSearchFilter(
            ListExceptionsForAuditQueryHandler.ParseStatus(query.Status), severity, query.OwnerUserId,
            query.AuditableEntityId, query.AuditId, query.Category, query.IsRecurrence, query.IsOverdue, today, query.Search,
            query.AnnualPlanId, query.RaisedFrom, query.RaisedTo);

        var csv = new CsvWriter(Header);
        await foreach (var r in exceptions.StreamForExportAsync(filter, cancellationToken))
        {
            var isOverdue = r.Status is not (ExceptionStatus.Closed or ExceptionStatus.Cancelled) && r.TargetDate < today;
            var statusLabel = r.Status == ExceptionStatus.PendingClosure && r.CiaPending ? "pending_cia_approval" : r.Status.ToSnake();
            csv.AppendRow(
                r.Id.ToString(),
                r.AuditId.ToString(),
                r.AuditName,
                r.Title,
                r.Severity.ToSnake(),
                r.Category,
                statusLabel,
                r.CiaPending ? "true" : "false",
                r.IsRecurrence ? "true" : "false",
                r.OwnerUserId.ToString(),
                r.AuditableEntityId?.ToString(),
                r.RaisedAt.ToString("o", CultureInfo.InvariantCulture),
                r.TargetDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                isOverdue ? "true" : "false",
                r.FinancialImpact?.ToString(CultureInfo.InvariantCulture),
                r.FinancialImpactCurrency,
                r.MapActionCount.ToString(CultureInfo.InvariantCulture),
                r.CompletedMapActionCount.ToString(CultureInfo.InvariantCulture));
        }

        var result = csv.Build($"finding-register-{clock.UtcNow.UtcDateTime:yyyyMMddHHmmss}.csv");

        // The export is auditable (who pulled what) — payload carries the filter + integrity hash, never row content.
        audit.Record(AuditEventTypes.FindingRegisterExported, AuditTargetTypes.Exception, null, payload: new
        {
            format = "csv", row_count = result.RowCount, sha256 = result.Sha256,
            query.Status, query.Severity, query.AuditId, query.AuditableEntityId, query.AnnualPlanId, query.Search,
        });
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return result;
    }
}

public sealed record GetExceptionHistoryQuery(Guid Id) : IQuery<IReadOnlyList<ExceptionHistoryEntryDto>>;

public sealed class GetExceptionHistoryQueryHandler(
    IExceptionRepository exceptions, IAuditRepository audits, IPermissionResolver permissions, ICurrentUser currentUser, IAuditTrailReader trail)
    : IQueryHandler<GetExceptionHistoryQuery, IReadOnlyList<ExceptionHistoryEntryDto>>
{
    public async Task<IReadOnlyList<ExceptionHistoryEntryDto>> Handle(GetExceptionHistoryQuery query, CancellationToken cancellationToken)
    {
        var exception = await exceptions.GetByIdAsync(query.Id, cancellationToken) ?? throw new NotFoundException("Exception", query.Id);
        var auditEntity = await audits.GetByIdAsync(exception.AuditId, cancellationToken) ?? throw new NotFoundException("Audit", exception.AuditId);
        await ExceptionAccess.EnsureCanAccessAsync(auditEntity, currentUser.UserId, permissions, cancellationToken);

        var entries = await trail.GetForTargetAsync(AuditTargetTypes.Exception, query.Id, eventType: null, limit: 200, cancellationToken);
        return entries
            .OrderBy(e => e.OccurredAtUtc)
            .Select(e => new ExceptionHistoryEntryDto(e.Id, e.EventType, e.ActorUserId, e.OccurredAtUtc, e.EventPayloadJson ?? e.AfterStateJson))
            .ToArray();
    }
}
