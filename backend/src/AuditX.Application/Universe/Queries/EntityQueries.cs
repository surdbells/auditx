using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Common.Models;
using AuditX.Application.Universe.Dtos;
using AuditX.Application.Universe.Mapping;
using AuditX.Domain.AuditTrail;

namespace AuditX.Application.Universe.Queries;

public sealed record ListEntitiesQuery(string? EntityType, Guid? Owner, bool IncludeArchived, string? Search, string? Cursor, int? Limit)
    : IQuery<CursorPage<EntityDto>>;

public sealed class ListEntitiesQueryHandler(IAuditUniverseRepository entities)
    : IQueryHandler<ListEntitiesQuery, CursorPage<EntityDto>>
{
    public async Task<CursorPage<EntityDto>> Handle(ListEntitiesQuery query, CancellationToken cancellationToken)
    {
        var page = PageRequest.Of(query.Cursor, query.Limit);
        var result = await entities.SearchAsync(query.EntityType, query.Owner, query.IncludeArchived, query.Search, page, cancellationToken);
        return new CursorPage<EntityDto>(result.Items.Select(e => e.ToDto()).ToArray(), result.NextCursor, result.HasMore);
    }
}

public sealed record GetEntityQuery(Guid Id) : IQuery<EntityDto>;

public sealed class GetEntityQueryHandler(IAuditUniverseRepository entities)
    : IQueryHandler<GetEntityQuery, EntityDto>
{
    public async Task<EntityDto> Handle(GetEntityQuery query, CancellationToken cancellationToken)
    {
        var entity = await entities.GetByIdAsync(query.Id, cancellationToken) ?? throw new NotFoundException("Entity", query.Id);
        return entity.ToDto();
    }
}

/// <summary>Risk-score change history for an entity, from the append-only audit trail (US-M3-010).</summary>
public sealed record RiskScoreHistoryQuery(Guid EntityId) : IQuery<IReadOnlyList<AuditTrailEntryView>>;

public sealed class RiskScoreHistoryQueryHandler(IAuditTrailReader auditTrail)
    : IQueryHandler<RiskScoreHistoryQuery, IReadOnlyList<AuditTrailEntryView>>
{
    public Task<IReadOnlyList<AuditTrailEntryView>> Handle(RiskScoreHistoryQuery query, CancellationToken cancellationToken)
        => auditTrail.GetForTargetAsync(AuditTargetTypes.AuditUniverseEntity, query.EntityId, AuditEventTypes.RiskScoreUpdated, 200, cancellationToken);
}
