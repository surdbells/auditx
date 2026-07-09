namespace AuditX.Application.Reports.Dtos;

/// <summary>The 202 payload from POST generate: the report id IS the job handle (no separate jobs surface — D1).</summary>
public sealed record ReportGenerationAcceptedDto(Guid ReportId, string Status);

/// <summary>One produced artefact descriptor surfaced on the report metadata.</summary>
public sealed record ProducedArtefactDto(string Format, string ContentType, long SizeBytes, string Sha256);

/// <summary>Report metadata / status surface (version, hash, template version, produced formats, timestamps).
/// <c>AuditId</c> is null for standalone (cross-audit) reports; <c>Kind</c> discriminates the shape.</summary>
public sealed record ReportDto(
    Guid Id,
    Guid? AuditId,
    string Kind,
    int VersionNumber,
    string Status,
    string? Sha256Hash,
    Guid TemplateId,
    int TemplateVersionSnapshot,
    IReadOnlyList<string> RequestedFormats,
    IReadOnlyList<ProducedArtefactDto> ProducedArtefacts,
    string? FailureReason,
    Guid GeneratedBy,
    DateTimeOffset RequestedAt,
    DateTimeOffset? CompletedAt,
    string Version);

/// <summary>Compact list item for the per-audit / per-kind version list. <c>AuditId</c> is null for standalone reports.</summary>
public sealed record ReportListItemDto(
    Guid Id,
    Guid? AuditId,
    string Kind,
    int VersionNumber,
    string Status,
    string? Sha256Hash,
    IReadOnlyList<string> ProducedFormats,
    Guid GeneratedBy,
    DateTimeOffset RequestedAt,
    DateTimeOffset? CompletedAt);

/// <summary>A single distribution-log row.</summary>
public sealed record ReportDistributionDto(
    Guid Id,
    Guid ReportId,
    int ReportVersionNumber,
    Guid? RecipientUserId,
    string? RecipientEmail,
    DateTimeOffset DispatchedAt,
    Guid DispatchedBy,
    string Outcome);

/// <summary>Result of the verify-hash endpoint: stored vs recomputed, and whether they match.</summary>
public sealed record ReportHashVerificationDto(string? StoredHash, string RecomputedHash, bool Match);

/// <summary>The outcome of a distribute request: how many recipients were dispatched to.</summary>
public sealed record ReportDistributionResultDto(int RecipientCount);

/// <summary>A report template projection.</summary>
public sealed record ReportTemplateDto(
    Guid Id,
    string Name,
    int VersionNumber,
    string TemplateDefinitionJson,
    bool IsActive,
    string? ActivationReason,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAtUtc,
    Guid? ActivatedBy,
    DateTimeOffset? ActivatedAt,
    string Version);

/// <summary>The downloaded artefact bytes returned to the controller for streaming.</summary>
public sealed record ReportArtefactResult(byte[] Content, string ContentType, string Filename, string Sha256Hash);
