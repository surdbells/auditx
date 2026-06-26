using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Administration;

/// <summary>
/// Record of a signed offline release package install attempt (US-M15-032). Verification (package
/// signature + manifest hash + change-record) happens in the infrastructure layer; this captures the
/// outcome for the audit trail and the releases surface.
/// </summary>
public sealed class ReleaseInstall : Entity
{
    private ReleaseInstall()
    {
    }

    public string Version { get; private set; } = null!;

    public string ManifestSha256 { get; private set; } = null!;

    public string ChangeRecordReference { get; private set; } = null!;

    public ReleaseInstallStatus Status { get; private set; }

    public string? Detail { get; private set; }

    public static ReleaseInstall Record(string version, string manifestSha256, string changeRecordReference, ReleaseInstallStatus status, string? detail)
        => new()
        {
            Version = Guard.NotNullOrWhiteSpace(version, "release.version_required", "Release version is required."),
            ManifestSha256 = Guard.NotNullOrWhiteSpace(manifestSha256, "release.manifest_required", "Manifest hash is required."),
            ChangeRecordReference = Guard.NotNullOrWhiteSpace(changeRecordReference, "release.change_record_required", "A change-record reference is required."),
            Status = status,
            Detail = detail,
        };
}

/// <summary>Record of a backup restore drill (US-M15-026): outcome and verification notes.</summary>
public sealed class RestoreDrill : Entity
{
    private RestoreDrill()
    {
    }

    public DateTimeOffset ExecutedAt { get; private set; }

    public RestoreOutcome Outcome { get; private set; }

    public string Details { get; private set; } = string.Empty;

    public static RestoreDrill Record(DateTimeOffset executedAt, RestoreOutcome outcome, string? details)
        => new() { ExecutedAt = executedAt, Outcome = outcome, Details = details ?? string.Empty };
}

/// <summary>
/// Approval-gated per-object restore request (US-M15-025). A requester provides a justification; a
/// designated approver approves or rejects before the restore executes.
/// </summary>
public sealed class ObjectRestoreRequest : AggregateRoot
{
    private ObjectRestoreRequest()
    {
    }

    public string ObjectType { get; private set; } = null!;

    public Guid ObjectId { get; private set; }

    public DateTimeOffset SnapshotDate { get; private set; }

    public string Justification { get; private set; } = null!;

    public ObjectRestoreStatus Status { get; private set; }

    public Guid RequestedByUserId { get; private set; }

    public Guid? DecidedByUserId { get; private set; }

    public string? DecisionComment { get; private set; }

    public static ObjectRestoreRequest Create(string objectType, Guid objectId, DateTimeOffset snapshotDate, string justification, Guid requestedByUserId)
        => new()
        {
            ObjectType = Guard.NotNullOrWhiteSpace(objectType, "restore.object_type_required", "Object type is required."),
            ObjectId = objectId,
            SnapshotDate = snapshotDate,
            Justification = Guard.MinLength(justification, 20, "restore.justification_too_short", "A justification of at least 20 characters is required."),
            Status = ObjectRestoreStatus.Requested,
            RequestedByUserId = requestedByUserId,
        };

    public void Approve(Guid approverUserId)
    {
        EnsureRequested();
        if (approverUserId == RequestedByUserId)
        {
            throw new DomainException("restore.self_approval", "A restore request cannot be approved by its requester.");
        }

        Status = ObjectRestoreStatus.Approved;
        DecidedByUserId = approverUserId;
    }

    public void Reject(Guid approverUserId, string reason)
    {
        EnsureRequested();
        Status = ObjectRestoreStatus.Rejected;
        DecidedByUserId = approverUserId;
        DecisionComment = Guard.MinLength(reason, 10, "restore.reason_too_short", "A rejection reason is required.");
    }

    public void MarkExecuted()
    {
        if (Status != ObjectRestoreStatus.Approved)
        {
            throw new InvalidStateTransitionException("restore.not_approved", "Only an approved restore can be executed.");
        }

        Status = ObjectRestoreStatus.Executed;
    }

    private void EnsureRequested()
    {
        if (Status != ObjectRestoreStatus.Requested)
        {
            throw new InvalidStateTransitionException("restore.not_pending", $"Restore request is already {Status.ToString().ToLowerInvariant()}.");
        }
    }
}
