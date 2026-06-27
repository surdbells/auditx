using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.AuditTrail.Dtos;
using AuditX.Application.Common.Enums;
using AuditX.Domain.Enums;

namespace AuditX.Application.AuditTrail.Mapping;

public static class AuditTrailMappings
{
    public static AuditTrailEntryDto ToDto(this AuditTrailEntryView e) => new(
        e.Id,
        e.EventType,
        e.TargetObjectType,
        e.TargetObjectId,
        e.ActorUserId,
        Enum.TryParse<ActorType>(e.ActorType, out var actorType) ? actorType.ToSnake() : e.ActorType.ToLowerInvariant(),
        e.ActorSystemLabel,
        e.OccurredAtUtc,
        e.OriginatingTimezone,
        e.BeforeStateJson,
        e.AfterStateJson,
        e.RequestContextJson,
        e.EventPayloadJson);
}
