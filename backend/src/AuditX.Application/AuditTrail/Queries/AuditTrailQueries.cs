using System.Globalization;
using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.AuditTrail.Dtos;
using AuditX.Application.AuditTrail.Mapping;
using AuditX.Application.Common.Csv;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Common.Models;
using AuditX.Domain.AuditTrail;

namespace AuditX.Application.AuditTrail.Queries;

// ---- General filtered query (US-M11-005 / FR-M11-004) ----

public sealed record QueryAuditTrailQuery(
    Guid? ActorUserId, string? EventType, string? TargetObjectType, Guid? TargetObjectId,
    DateTimeOffset? From, DateTimeOffset? To, string? Cursor, int? Limit) : IQuery<CursorPage<AuditTrailEntryDto>>;

public sealed class QueryAuditTrailQueryHandler(IAuditTrailReader reader, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : IQueryHandler<QueryAuditTrailQuery, CursorPage<AuditTrailEntryDto>>
{
    public async Task<CursorPage<AuditTrailEntryDto>> Handle(QueryAuditTrailQuery query, CancellationToken cancellationToken)
    {
        var filter = new AuditTrailFilter(query.ActorUserId, query.EventType, query.TargetObjectType, query.TargetObjectId, query.From, query.To);
        var page = await reader.QueryAsync(filter, PageRequest.Of(query.Cursor, query.Limit), cancellationToken);

        // The trail access is itself auditable (who viewed what). Targets `audit_trail` so it is never re-queried inline.
        audit.Record(AuditEventTypes.TrailQueried, AuditTargetTypes.AuditTrail, null, payload: new
        {
            query.ActorUserId, query.EventType, query.TargetObjectType, query.TargetObjectId,
            query.From, query.To, returned = page.Items.Count,
        });
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CursorPage<AuditTrailEntryDto>(page.Items.Select(e => e.ToDto()).ToArray(), page.NextCursor, page.HasMore);
    }
}

// ---- Filter facets (distinct event/target types for dropdowns) ----

public sealed record GetAuditTrailFacetsQuery : IQuery<AuditTrailFacets>;

public sealed class GetAuditTrailFacetsQueryHandler(IAuditTrailReader reader)
    : IQueryHandler<GetAuditTrailFacetsQuery, AuditTrailFacets>
{
    public Task<AuditTrailFacets> Handle(GetAuditTrailFacetsQuery query, CancellationToken cancellationToken)
        => reader.GetFacetsAsync(cancellationToken);
}

// ---- Per-object history (US-M11-008) ----

public sealed record GetObjectHistoryQuery(string TargetObjectType, Guid TargetObjectId, string? Cursor, int? Limit) : IQuery<CursorPage<AuditTrailEntryDto>>;

public sealed class GetObjectHistoryQueryHandler(IAuditTrailReader reader, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : IQueryHandler<GetObjectHistoryQuery, CursorPage<AuditTrailEntryDto>>
{
    public async Task<CursorPage<AuditTrailEntryDto>> Handle(GetObjectHistoryQuery query, CancellationToken cancellationToken)
    {
        // Reuse the keyset-paginated query so a long object history is never silently truncated.
        var filter = new AuditTrailFilter(TargetObjectType: query.TargetObjectType, TargetObjectId: query.TargetObjectId);
        var page = await reader.QueryAsync(filter, PageRequest.Of(query.Cursor, query.Limit), cancellationToken);

        // Self-audit targets `audit_trail` with a null id (the queried object id lives in the payload, so the
        // (target_type, target_id) pair stays internally consistent).
        audit.Record(AuditEventTypes.TrailQueried, AuditTargetTypes.AuditTrail, null,
            payload: new { query.TargetObjectType, queried_object_id = query.TargetObjectId, returned = page.Items.Count });
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CursorPage<AuditTrailEntryDto>(page.Items.Select(e => e.ToDto()).ToArray(), page.NextCursor, page.HasMore);
    }
}

// ---- CSV export (US-M11-007) ----

public sealed record ExportAuditTrailQuery(
    Guid? ActorUserId, string? EventType, string? TargetObjectType, Guid? TargetObjectId,
    DateTimeOffset? From, DateTimeOffset? To) : IQuery<AuditTrailExportDto>;

public sealed class ExportAuditTrailQueryHandler(IAuditTrailReader reader, IClock clock, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : IQueryHandler<ExportAuditTrailQuery, AuditTrailExportDto>
{
    // Tabular index only — the before/after/payload JSON blobs are deliberately excluded so an export cannot
    // leak credential material or bulk PII embedded in state snapshots.
    private static readonly string[] Header =
        ["id", "occurred_at_utc", "actor_type", "actor_user_id", "event_type", "target_object_type", "target_object_id", "actor_system_label", "originating_timezone"];

    public async Task<AuditTrailExportDto> Handle(ExportAuditTrailQuery query, CancellationToken cancellationToken)
    {
        var filter = new AuditTrailFilter(query.ActorUserId, query.EventType, query.TargetObjectType, query.TargetObjectId, query.From, query.To);

        var csv = new CsvWriter(Header);
        await foreach (var e in reader.StreamAsync(filter, cancellationToken))
        {
            var dto = e.ToDto();
            csv.AppendRow(
                dto.Id.ToString(),
                dto.OccurredAtUtc.ToString("o", CultureInfo.InvariantCulture),
                dto.ActorType,
                dto.ActorUserId?.ToString(),
                dto.EventType,
                dto.TargetObjectType,
                dto.TargetObjectId?.ToString(),
                dto.ActorSystemLabel,
                dto.OriginatingTimezone);
        }

        var result = csv.Build($"audit-trail-{clock.UtcNow.UtcDateTime:yyyyMMddHHmmss}.csv");

        audit.Record(AuditEventTypes.TrailExported, AuditTargetTypes.AuditTrail, null, payload: new
        {
            format = "csv", row_count = result.RowCount, sha256 = result.Sha256,
            query.ActorUserId, query.EventType, query.TargetObjectType, query.TargetObjectId, query.From, query.To,
        });
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AuditTrailExportDto(result.FileName, result.ContentType, result.Sha256, result.RowCount, result.Content);
    }
}
