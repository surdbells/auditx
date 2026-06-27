namespace AuditX.Application.AuditTrail.Dtos;

/// <summary>An audit-trail entry projected for the M11 investigator surfaces (enums snake_case).</summary>
public sealed record AuditTrailEntryDto(
    Guid Id,
    string EventType,
    string TargetObjectType,
    Guid? TargetObjectId,
    Guid? ActorUserId,
    string ActorType,
    string? ActorSystemLabel,
    DateTimeOffset OccurredAtUtc,
    string? OriginatingTimezone,
    string? BeforeStateJson,
    string? AfterStateJson,
    string? RequestContextJson,
    string? EventPayloadJson);

/// <summary>A generated audit-trail export artefact (M11) with its integrity hash.</summary>
public sealed record AuditTrailExportDto(string FileName, string ContentType, string Sha256, int RowCount, byte[] Content);
