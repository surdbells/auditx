using AuditX.Domain.Common;
using AuditX.Domain.Enums;
using AuditX.Domain.Reports.Events;

namespace AuditX.Domain.Reports;

/// <summary>
/// A generated report (M8). Aggregate root over its distribution log. Two shapes, discriminated by <see cref="Kind"/>:
/// the per-audit <b>engagement</b> report (<see cref="ReportKind.AuditEngagement"/>, <see cref="AuditId"/> set,
/// versioned per audit); and <b>standalone</b> cross-audit reports (<see cref="AuditId"/> null, versioned per kind)
/// rendered from the M9 analytics. The canonical HTML's SHA-256 is stored on <see cref="Sha256Hash"/> for
/// verify-on-read. The template definition JSON is SNAPSHOTTED at <see cref="Start"/> so a historical report always
/// renders as issued even after the active template moves on (B2/B3).
///
/// Immutability (FR-M8-009): there is NO content mutation after <see cref="Complete"/>. The status guards throw
/// <see cref="InvalidStateTransitionException"/> on illegal moves and the API exposes no mutating verb on a report.
/// Producing different content = edit the audit (M4/M5/M6) and regenerate → a new <see cref="Report"/> row.
/// </summary>
public sealed class Report : AggregateRoot, ISoftDeletable
{
    private readonly List<ReportDistribution> _distributions = [];

    private Report()
    {
    }

    /// <summary>The engagement audit; null for standalone (cross-audit) reports.</summary>
    public Guid? AuditId { get; private set; }

    /// <summary>Discriminates engagement (per-audit) from the standalone analytics report kinds.</summary>
    public ReportKind Kind { get; private set; }

    public int VersionNumber { get; private set; }

    public ReportStatus Status { get; private set; }

    /// <summary>SHA-256 of the canonical HTML artefact (null until <see cref="Complete"/>).</summary>
    public string? Sha256Hash { get; private set; }

    public Guid TemplateId { get; private set; }

    public int TemplateVersionSnapshot { get; private set; }

    /// <summary>The template definition JSON snapshotted at Start so historical reports render as issued (B2/B3).</summary>
    public string TemplateDefinitionSnapshotJson { get; private set; } = null!;

    /// <summary>JSON array of requested formats, e.g. <c>["html","docx"]</c>. HTML is always present.</summary>
    public string RequestedFormatsJson { get; private set; } = "[\"html\"]";

    /// <summary>JSON array of produced artefacts <c>[{format,fileKey,contentType,sizeBytes,sha256}]</c>.</summary>
    public string ProducedArtefactsJson { get; private set; } = "[]";

    public string? FailureReason { get; private set; }

    public Guid GeneratedBy { get; private set; }

    public DateTimeOffset RequestedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>Column only — the retention-expiry job is deferred (M11/M15-owned).</summary>
    public DateTimeOffset? RetentionUntil { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? DeletedBy { get; private set; }

    public string? DeletionReason { get; private set; }

    public byte[] Version { get; private set; } = [];

    public IReadOnlyList<ReportDistribution> Distributions => _distributions.AsReadOnly();

    /// <summary>The engagement construction path: a manual per-audit generation request (status → pending).</summary>
    public static Report Start(
        Guid auditId, int versionNumber, Guid templateId, int templateVersion, string templateDefinitionSnapshotJson,
        IReadOnlyList<string> requestedFormats, Guid actorId, DateTimeOffset nowUtc)
    {
        var formats = NormaliseFormats(requestedFormats);
        var report = new Report
        {
            AuditId = auditId,
            Kind = ReportKind.AuditEngagement,
            VersionNumber = versionNumber,
            Status = ReportStatus.Pending,
            TemplateId = templateId,
            TemplateVersionSnapshot = templateVersion,
            TemplateDefinitionSnapshotJson = Guard.NotNullOrWhiteSpace(
                templateDefinitionSnapshotJson, "report.template_definition_required", "A template definition snapshot is required."),
            RequestedFormatsJson = SerializeFormats(formats),
            GeneratedBy = actorId,
            RequestedAt = nowUtc,
        };
        report.RaiseDomainEvent(new ReportGenerationRequestedEvent(report.Id, auditId, versionNumber, actorId));
        return report;
    }

    /// <summary>
    /// The standalone construction path: a cross-audit report request (status → pending). No <see cref="AuditId"/>;
    /// versioning is per <paramref name="kind"/>. <paramref name="kind"/> must not be
    /// <see cref="ReportKind.AuditEngagement"/> — that shape is built through <see cref="Start"/>.
    /// </summary>
    public static Report StartStandalone(
        ReportKind kind, int versionNumber, Guid templateId, int templateVersion, string templateDefinitionSnapshotJson,
        IReadOnlyList<string> requestedFormats, Guid actorId, DateTimeOffset nowUtc)
    {
        if (kind == ReportKind.AuditEngagement)
        {
            throw new DomainException("report.kind_not_standalone", "An engagement report must be created through Start with an audit.");
        }

        var formats = NormaliseFormats(requestedFormats);
        var report = new Report
        {
            AuditId = null,
            Kind = kind,
            VersionNumber = versionNumber,
            Status = ReportStatus.Pending,
            TemplateId = templateId,
            TemplateVersionSnapshot = templateVersion,
            TemplateDefinitionSnapshotJson = Guard.NotNullOrWhiteSpace(
                templateDefinitionSnapshotJson, "report.template_definition_required", "A template definition snapshot is required."),
            RequestedFormatsJson = SerializeFormats(formats),
            GeneratedBy = actorId,
            RequestedAt = nowUtc,
        };
        report.RaiseDomainEvent(new ReportGenerationRequestedEvent(report.Id, null, versionNumber, actorId));
        return report;
    }

    /// <summary>Move pending → running as the generation worker begins (idempotent guard lives in the service).</summary>
    public void MarkRunning() => Transition(ReportStatus.Running, ReportStatus.Pending);

    /// <summary>
    /// Complete generation: record the canonical hash + produced artefacts (running → completed). After this the
    /// report is content-immutable. Raises <see cref="ReportGeneratedEvent"/> for M10 + the trail.
    /// </summary>
    public void Complete(string canonicalSha256, IReadOnlyList<ProducedArtefact> artefacts, string producedArtefactsJson, DateTimeOffset nowUtc)
    {
        Transition(ReportStatus.Completed, ReportStatus.Running);
        Sha256Hash = Guard.NotNullOrWhiteSpace(canonicalSha256, "report.hash_required", "A canonical hash is required to complete a report.");
        if (artefacts.Count == 0)
        {
            throw new DomainException("report.no_artefacts", "A completed report must have at least one produced artefact.");
        }

        ProducedArtefactsJson = Guard.NotNullOrWhiteSpace(producedArtefactsJson, "report.artefacts_required", "Produced artefacts are required.");
        CompletedAt = nowUtc;
        RaiseDomainEvent(new ReportGeneratedEvent(Id, AuditId, VersionNumber, GeneratedBy));
    }

    /// <summary>Mark generation failed with a reason (→ failed). Terminal; regeneration is a new report row.</summary>
    public void Fail(string reason)
    {
        Transition(ReportStatus.Failed, ReportStatus.Pending, ReportStatus.Running);
        FailureReason = Guard.NotNullOrWhiteSpace(reason, "report.failure_reason_required", "A failure reason is required.");
        RaiseDomainEvent(new ReportGenerationFailedEvent(Id, AuditId, VersionNumber, FailureReason));
    }

    /// <summary>
    /// Record a distribution row and raise <see cref="ReportDistributedEvent"/>. Only a completed report may be
    /// distributed. The exactly-one-of user/email guard lives in <see cref="ReportDistribution"/>.
    /// </summary>
    public ReportDistribution RecordDistribution(
        Guid? recipientUserId, string? recipientEmail, string resolvedEmail, Guid dispatchedBy, DateTimeOffset nowUtc,
        string auditName, int totalCount, int exceptionCount, string severitySummary)
    {
        if (Status != ReportStatus.Completed)
        {
            throw new InvalidStateTransitionException("report.not_completed", "Only a completed report can be distributed.");
        }

        var distribution = new ReportDistribution(Id, VersionNumber, recipientUserId, recipientEmail, dispatchedBy, nowUtc);
        _distributions.Add(distribution);
        RaiseDomainEvent(new ReportDistributedEvent(
            Id, AuditId, VersionNumber, recipientUserId, resolvedEmail, auditName, totalCount, exceptionCount, severitySummary));
        return distribution;
    }

    /// <summary>
    /// Raise the integrity alert when verify-on-read detects a hash mismatch (C1/C2). Does NOT mutate report
    /// content — it is an alert only — and the event payload carries Critical severity for the M10 override.
    /// </summary>
    public void RaiseHashMismatch(string expected, string recomputed, Guid detectedBy)
        => RaiseDomainEvent(new ReportHashMismatchEvent(Id, AuditId, expected, recomputed, detectedBy));

    void ISoftDeletable.SoftDelete(Guid? deletedBy, DateTimeOffset deletedAtUtc)
    {
        IsDeleted = true;
        DeletedAt = deletedAtUtc;
        DeletedBy = deletedBy;
    }

    private void Transition(ReportStatus to, params ReportStatus[] from)
    {
        if (from.Length > 0 && !from.Contains(Status))
        {
            throw new InvalidStateTransitionException("report.invalid_transition", $"Cannot move to {to} from {Status}.");
        }

        Status = to;
    }

    private static IReadOnlyList<string> NormaliseFormats(IReadOnlyList<string> requested)
    {
        // HTML is the canonical, always-produced artefact; PDF + CSV + XLSX are always produced too so every report can
        // be exported (PDF/Excel/CSV) from the viewer. DOCX (and any other requested format) is preserved on top.
        var set = new List<string> { "html", "pdf", "csv", "xlsx" };
        foreach (var format in requested)
        {
            var normalised = format?.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(normalised) && !set.Contains(normalised))
            {
                set.Add(normalised);
            }
        }

        return set;
    }

    private static string SerializeFormats(IReadOnlyList<string> formats)
        => "[" + string.Join(",", formats.Select(f => $"\"{f}\"")) + "]";
}
