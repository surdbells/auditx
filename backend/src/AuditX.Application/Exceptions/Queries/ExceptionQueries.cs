using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Abstractions.Persistence;
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

        return Enum.TryParse<ExceptionStatus>(value.Replace("_", string.Empty), ignoreCase: true, out var s)
            ? s
            : throw new ConflictException("exception.invalid_status", $"Unknown status '{value}'.");
    }
}

public sealed record SearchExceptionsQuery(
    string? Status, string? Severity, Guid? OwnerUserId, Guid? AuditableEntityId, Guid? AuditId,
    string? Category, bool? IsRecurrence, bool? IsOverdue, string? Cursor, int? Limit)
    : IQuery<CursorPage<ExceptionListItemDto>>;

public sealed class SearchExceptionsQueryHandler(IExceptionRepository exceptions, IClock clock)
    : IQueryHandler<SearchExceptionsQuery, CursorPage<ExceptionListItemDto>>
{
    public async Task<CursorPage<ExceptionListItemDto>> Handle(SearchExceptionsQuery query, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        ExceptionSeverity? severity = string.IsNullOrWhiteSpace(query.Severity)
            ? null
            : Enum.TryParse<ExceptionSeverity>(query.Severity.Replace("_", string.Empty), ignoreCase: true, out var sv)
                ? sv
                : throw new ConflictException("exception.invalid_severity", $"Unknown severity '{query.Severity}'.");

        var filter = new ExceptionSearchFilter(
            ListExceptionsForAuditQueryHandler.ParseStatus(query.Status), severity, query.OwnerUserId,
            query.AuditableEntityId, query.AuditId, query.Category, query.IsRecurrence, query.IsOverdue, today);

        var page = PageRequest.Of(query.Cursor, query.Limit);
        var result = await exceptions.SearchAsync(filter, page, cancellationToken);
        return new CursorPage<ExceptionListItemDto>(result.Items.Select(e => e.ToListItemDto(today)).ToArray(), result.NextCursor, result.HasMore);
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
