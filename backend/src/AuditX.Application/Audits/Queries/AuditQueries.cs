using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Audits.Dtos;
using AuditX.Application.Audits.Mapping;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Common.Models;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Enums;

namespace AuditX.Application.Audits.Queries;

public sealed record ListAuditsQuery(string? Status, string? AuditType, Guid? Lead, Guid? PlanItem, string? Search, int? Page, int? PageSize)
    : IQuery<PagedResult<AuditListItemDto>>;

public sealed class ListAuditsQueryHandler(IAuditRepository audits)
    : IQueryHandler<ListAuditsQuery, PagedResult<AuditListItemDto>>
{
    public async Task<PagedResult<AuditListItemDto>> Handle(ListAuditsQuery query, CancellationToken cancellationToken)
    {
        AuditStatus? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            status = Enum.TryParse<AuditStatus>(query.Status.Replace("_", string.Empty), ignoreCase: true, out var parsed)
                ? parsed
                : throw new ConflictException("invalid_status", $"Unknown audit status '{query.Status}'.");
        }

        var page = PageSpec.Of(query.Page, query.PageSize);
        var result = await audits.SearchAsync(status, query.AuditType, query.Lead, query.PlanItem, query.Search, page, cancellationToken);
        return result.Map(a => a.ToListItemDto());
    }
}

public sealed record GetAuditQuery(Guid Id) : IQuery<AuditDto>;

public sealed class GetAuditQueryHandler(IAuditRepository audits)
    : IQueryHandler<GetAuditQuery, AuditDto>
{
    public async Task<AuditDto> Handle(GetAuditQuery query, CancellationToken cancellationToken)
    {
        var entity = await audits.GetByIdAsync(query.Id, cancellationToken) ?? throw new NotFoundException("Audit", query.Id);
        return entity.ToDto();
    }
}

/// <summary>
/// The audit's activity timeline — lifecycle (created/planned/started/reopened/completed/cancelled), team,
/// section and checklist events — read from the append-only trail. Gated by ViewAudit at the controller,
/// mirroring the response/exception-history precedent (not the admin ViewAuditTrail surface).
/// </summary>
public sealed record GetAuditHistoryQuery(Guid AuditId) : IQuery<IReadOnlyList<AuditHistoryEntryDto>>;

public sealed class GetAuditHistoryQueryHandler(IAuditRepository audits, IAuditTrailReader trail)
    : IQueryHandler<GetAuditHistoryQuery, IReadOnlyList<AuditHistoryEntryDto>>
{
    // The audit-family trail entries that all carry the audit id as their target id.
    private static readonly string[] AuditFamilyTargets =
    [
        AuditTargetTypes.Audit,
        AuditTargetTypes.AuditTeamMember,
        AuditTargetTypes.AuditSection,
        AuditTargetTypes.AuditChecklistItem,
    ];

    public async Task<IReadOnlyList<AuditHistoryEntryDto>> Handle(GetAuditHistoryQuery query, CancellationToken cancellationToken)
    {
        _ = await audits.GetByIdAsync(query.AuditId, cancellationToken) ?? throw new NotFoundException("Audit", query.AuditId);

        var entries = new List<AuditTrailEntryView>();
        foreach (var target in AuditFamilyTargets)
        {
            entries.AddRange(await trail.GetForTargetAsync(target, query.AuditId, eventType: null, limit: 200, cancellationToken));
        }

        return entries
            .OrderBy(e => e.OccurredAtUtc)
            .Select(e => new AuditHistoryEntryDto(
                e.Id, e.EventType, e.TargetObjectType, e.ActorUserId, e.OccurredAtUtc, e.AfterStateJson ?? e.EventPayloadJson))
            .ToArray();
    }
}

public sealed record AuditCountsQuery : IQuery<AuditCountsDto>;

public sealed class AuditCountsQueryHandler(IAuditRepository audits)
    : IQueryHandler<AuditCountsQuery, AuditCountsDto>
{
    public async Task<AuditCountsDto> Handle(AuditCountsQuery query, CancellationToken cancellationToken)
        => new(await audits.CountsByStatusAsync(cancellationToken));
}
