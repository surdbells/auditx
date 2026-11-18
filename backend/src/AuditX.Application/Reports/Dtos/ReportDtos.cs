namespace AuditX.Application.Reports.Dtos;

/// <summary>The 202 payload from POST generate: the report id IS the job handle (no separate jobs surface — D1).</summary>
public sealed record ReportGenerationAcceptedDto(Guid ReportId, string Status);

/// <summary>One produced artefact descriptor surfaced on the report metadata.</summary>
public sealed record ProducedArtefactDto(string Format, string ContentType, long SizeBytes, string Sha256);

/// <summary>
/// The branding a report was issued under (frozen at generation): the institution name, its colours, and the
/// SHA-256 of the logo embedded in the artefacts (null when it was issued without a logo).
/// </summary>
public sealed record ReportBrandingDto(string InstitutionName, string PrimaryColor, string AccentColor, string? LogoSha256);

/// <summary>Report metadata / status surface (version, hash, template version, produced formats, timestamps).
/// <c>AuditId</c> is null for standalone (cross-audit) reports; <c>Kind</c> discriminates the shape.
/// <c>Branding</c> is null until generation starts, and for reports issued before branding snapshots existed.</summary>
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
    string Version,
    ReportBrandingDto? Branding = null)
{
    /// <summary><c>draft</c> (for discussion with management) or <c>final</c>.</summary>
    public string Stage { get; init; } = "final";

    /// <summary><c>not_required</c>, <c>awaiting</c>, <c>signed_off</c> or <c>returned</c>.</summary>
    public string SignOffStatus { get; init; } = "not_required";

    public Guid? SignOffBy { get; init; }

    public DateTimeOffset? SignOffAt { get; init; }

    /// <summary>The signer's remark when signing off, or the reason when returning it for changes.</summary>
    public string? SignOffNote { get; init; }

    /// <summary>True once the report has been sent to anyone.</summary>
    public bool IsDistributed { get; init; }

    /// <summary>True when the report may be distributed now (a completed draft, or a signed-off final).</summary>
    public bool IsDistributable { get; init; }
}

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
    DateTimeOffset? CompletedAt)
{
    public string Stage { get; init; } = "final";

    public string SignOffStatus { get; init; } = "not_required";
}

/// <summary>A comment on a draft report.</summary>
public sealed record ReportCommentDto(Guid Id, Guid ReportId, int ReportVersionNumber, Guid AuthorUserId, string Body, DateTimeOffset PostedAt);

/// <summary>A final report awaiting the Chief Internal Auditor's sign-off.</summary>
public sealed record ReportSignOffQueueItemDto(
    Guid ReportId, Guid AuditId, string AuditName, int VersionNumber, Guid GeneratedBy, DateTimeOffset? CompletedAt);

/// <summary>A single distribution-log row.</summary>
public sealed record ReportDistributionDto(
    Guid Id,
    Guid ReportId,
    int ReportVersionNumber,
    Guid? RecipientUserId,
    string? RecipientEmail,
    DateTimeOffset DispatchedAt,
    Guid DispatchedBy,
    string Outcome,
    DateTimeOffset? OutcomeRecordedAt,
    Guid? OutcomeRecordedBy);

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
