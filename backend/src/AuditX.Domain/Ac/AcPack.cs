using AuditX.Domain.Ac.Events;
using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Ac;

/// <summary>
/// A versioned, hash-sealed Audit-Committee pack (M13). Aggregate root over its distribution log. Mirrors the M8
/// <see cref="AuditX.Domain.Reports.Report"/>: per-bank sequential <see cref="VersionNumber"/> (unique), the
/// canonical HTML's SHA-256 on <see cref="Sha256Hash"/> for verify-on-read, and the assembled M9-analytics
/// composition SNAPSHOTTED into <see cref="ContentSnapshotJson"/> at completion so a historical pack always renders
/// as issued. Adds the CIA review/approve/distribute gate on top of the M8 generate→render→store→hash flow.
///
/// Immutability: <see cref="ContentSnapshotJson"/> is set once at <see cref="Complete"/> and never mutated. The only
/// editable content is <see cref="CiaSupplementaryText"/>, and only while <see cref="AcPackStatus.PendingReview"/>.
/// </summary>
public sealed class AcPack : AggregateRoot, ISoftDeletable
{
    private readonly List<AcPackDistribution> _distributions = [];

    private AcPack()
    {
    }

    public int VersionNumber { get; private set; }

    public AcPackStatus Status { get; private set; }

    public DateOnly PeriodStart { get; private set; }

    public DateOnly PeriodEnd { get; private set; }

    /// <summary>Free-text label for the AC meeting this pack is prepared for (no meetings/minutes entity — deferred).</summary>
    public string? AcMeetingLabel { get; private set; }

    /// <summary>The immutable assembled M9-analytics composition (snapshotted at Complete; re-render never re-queries live).</summary>
    public string? ContentSnapshotJson { get; private set; }

    /// <summary>CIA narrative added to the pack. Editable ONLY while PendingReview.</summary>
    public string? CiaSupplementaryText { get; private set; }

    /// <summary>The canonical HTML artefact's opaque blob-store key (null until Complete).</summary>
    public string? ArtefactStoragePath { get; private set; }

    /// <summary>SHA-256 of the canonical HTML artefact (null until Complete).</summary>
    public string? Sha256Hash { get; private set; }

    /// <summary>JSON array of produced artefacts <c>[{format,fileKey,contentType,sizeBytes,sha256}]</c>.</summary>
    public string ProducedArtefactsJson { get; private set; } = "[]";

    /// <summary>JSON array of requested formats, e.g. <c>["html","docx"]</c>. HTML is always present.</summary>
    public string RequestedFormatsJson { get; private set; } = "[\"html\"]";

    public string? FailureReason { get; private set; }

    public Guid GeneratedBy { get; private set; }

    public DateTimeOffset RequestedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public Guid? ApprovedBy { get; private set; }

    public DateTimeOffset? ApprovedAt { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? DeletedBy { get; private set; }

    public string? DeletionReason { get; private set; }

    public byte[] Version { get; private set; } = [];

    public IReadOnlyList<AcPackDistribution> Distributions => _distributions.AsReadOnly();

    /// <summary>The sole construction path: a generation request (status → generated).</summary>
    public static AcPack Start(
        int versionNumber, DateOnly periodStart, DateOnly periodEnd, string? acMeetingLabel,
        IReadOnlyList<string> requestedFormats, Guid actorId, DateTimeOffset nowUtc)
    {
        if (periodEnd < periodStart)
        {
            throw new DomainException("ac_pack.invalid_period", "The reporting period end cannot be before its start.");
        }

        var formats = NormaliseFormats(requestedFormats);
        var pack = new AcPack
        {
            VersionNumber = versionNumber,
            Status = AcPackStatus.Generated,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            AcMeetingLabel = string.IsNullOrWhiteSpace(acMeetingLabel) ? null : acMeetingLabel.Trim(),
            RequestedFormatsJson = SerializeFormats(formats),
            GeneratedBy = actorId,
            RequestedAt = nowUtc,
        };
        pack.RaiseDomainEvent(new AcPackGenerationRequestedEvent(pack.Id, versionNumber, actorId));
        return pack;
    }

    /// <summary>Move generated → running as the generation worker begins (idempotent guard lives in the service).</summary>
    public void MarkRunning() => Transition(AcPackStatus.Running, AcPackStatus.Generated);

    /// <summary>
    /// Complete generation: snapshot the assembled content, record the canonical hash + produced artefacts
    /// (running → pending_review). After this the content snapshot is immutable. Raises <see cref="AcPackGeneratedEvent"/>.
    /// </summary>
    public void Complete(
        string contentSnapshotJson, string canonicalSha256, string artefactStoragePath, string producedArtefactsJson,
        int artefactCount, DateTimeOffset nowUtc)
    {
        Transition(AcPackStatus.PendingReview, AcPackStatus.Running);
        ContentSnapshotJson = Guard.NotNullOrWhiteSpace(contentSnapshotJson, "ac_pack.snapshot_required", "A content snapshot is required to complete an AC pack.");
        Sha256Hash = Guard.NotNullOrWhiteSpace(canonicalSha256, "ac_pack.hash_required", "A canonical hash is required to complete an AC pack.");
        ArtefactStoragePath = Guard.NotNullOrWhiteSpace(artefactStoragePath, "ac_pack.artefact_path_required", "A canonical artefact path is required.");
        if (artefactCount == 0)
        {
            throw new DomainException("ac_pack.no_artefacts", "A completed AC pack must have at least one produced artefact.");
        }

        ProducedArtefactsJson = Guard.NotNullOrWhiteSpace(producedArtefactsJson, "ac_pack.artefacts_required", "Produced artefacts are required.");
        CompletedAt = nowUtc;
        RaiseDomainEvent(new AcPackGeneratedEvent(Id, VersionNumber, GeneratedBy));
    }

    /// <summary>Mark generation failed with a reason (→ failed). Terminal; regeneration is a new pack row.</summary>
    public void Fail(string reason)
    {
        Transition(AcPackStatus.Failed, AcPackStatus.Generated, AcPackStatus.Running);
        FailureReason = Guard.NotNullOrWhiteSpace(reason, "ac_pack.failure_reason_required", "A failure reason is required.");
        RaiseDomainEvent(new AcPackGenerationFailedEvent(Id, VersionNumber, FailureReason));
    }

    /// <summary>Add/replace the CIA supplementary narrative. Only permitted while PendingReview (US — pre-approval edit).</summary>
    public void AddSupplementaryText(string? text)
    {
        if (Status != AcPackStatus.PendingReview)
        {
            throw new InvalidStateTransitionException(
                "ac_pack.supplementary_text_locked", "Supplementary text can only be edited while the pack is pending review.");
        }

        CiaSupplementaryText = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    /// <summary>
    /// Re-seal the produced artefacts at approval time (still PendingReview) so the canonical artefact + hash reflect
    /// the FINAL pack — including the CIA supplementary narrative added during review (which the generation-time render
    /// could not contain). The immutable <see cref="ContentSnapshotJson"/> is unchanged; only the rendered artefacts +
    /// their hash are replaced, keeping verify-on-read coherent against the distributed document.
    /// </summary>
    public void ResealArtefacts(string canonicalSha256, string artefactStoragePath, string producedArtefactsJson, int artefactCount)
    {
        if (Status != AcPackStatus.PendingReview)
        {
            throw new InvalidStateTransitionException(
                "ac_pack.reseal_locked", "Artefacts can only be re-sealed while the pack is pending review.");
        }

        if (artefactCount == 0)
        {
            throw new DomainException("ac_pack.no_artefacts", "A re-sealed AC pack must have at least one produced artefact.");
        }

        Sha256Hash = Guard.NotNullOrWhiteSpace(canonicalSha256, "ac_pack.hash_required", "A canonical hash is required.");
        ArtefactStoragePath = Guard.NotNullOrWhiteSpace(artefactStoragePath, "ac_pack.artefact_path_required", "A canonical artefact path is required.");
        ProducedArtefactsJson = Guard.NotNullOrWhiteSpace(producedArtefactsJson, "ac_pack.artefacts_required", "Produced artefacts are required.");
    }

    /// <summary>Approve the pack for distribution (pending_review → approved). Single-actor CIA; NOT maker-checker.</summary>
    public void Approve(Guid approvedBy, DateTimeOffset nowUtc)
    {
        Transition(AcPackStatus.Approved, AcPackStatus.PendingReview);
        ApprovedBy = approvedBy;
        ApprovedAt = nowUtc;
        RaiseDomainEvent(new AcPackApprovedEvent(Id, VersionNumber, approvedBy));
    }

    /// <summary>
    /// Record a distribution row to one AC recipient and raise a per-recipient <see cref="AcPackDistributedEvent"/>.
    /// Only an approved (or already-distributing) pack may be distributed; the first call moves approved → distributed.
    /// </summary>
    public AcPackDistribution RecordDistribution(Guid recipientUserId, Guid dispatchedBy, DateTimeOffset nowUtc)
    {
        if (Status is not (AcPackStatus.Approved or AcPackStatus.Distributed))
        {
            throw new InvalidStateTransitionException("ac_pack.not_approved", "Only an approved AC pack can be distributed.");
        }

        var distribution = new AcPackDistribution(Id, VersionNumber, recipientUserId, dispatchedBy, nowUtc);
        _distributions.Add(distribution);
        if (Status == AcPackStatus.Approved)
        {
            Status = AcPackStatus.Distributed;
        }

        RaiseDomainEvent(new AcPackDistributedEvent(Id, VersionNumber, recipientUserId, AcMeetingLabel));
        return distribution;
    }

    /// <summary>
    /// Raise the integrity alert when verify-on-read detects a hash mismatch (mirrors M8). Does NOT mutate pack
    /// content — it is an alert only — and the event payload carries Critical severity for the M10 override.
    /// </summary>
    public void RaiseHashMismatch(string expected, string recomputed, Guid detectedBy)
        => RaiseDomainEvent(new AcPackHashMismatchEvent(Id, expected, recomputed, detectedBy));

    void ISoftDeletable.SoftDelete(Guid? deletedBy, DateTimeOffset deletedAtUtc)
    {
        IsDeleted = true;
        DeletedAt = deletedAtUtc;
        DeletedBy = deletedBy;
    }

    private void Transition(AcPackStatus to, params AcPackStatus[] from)
    {
        if (from.Length > 0 && !from.Contains(Status))
        {
            throw new InvalidStateTransitionException("ac_pack.invalid_transition", $"Cannot move to {to} from {Status}.");
        }

        Status = to;
    }

    private static IReadOnlyList<string> NormaliseFormats(IReadOnlyList<string> requested)
    {
        // HTML is the canonical, always-produced artefact; preserve any additional requested format (e.g. docx).
        var set = new List<string> { "html" };
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
