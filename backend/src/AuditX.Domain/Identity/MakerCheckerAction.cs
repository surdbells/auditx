using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Identity;

/// <summary>
/// A pending dual-control action. When a gated operation is submitted by a maker, the operation is
/// NOT executed; instead its intent is captured here as <see cref="MakerCheckerStatus.Pending"/>.
/// A different user (the checker) must approve it before it takes effect. A maker may never approve
/// their own action (BR-M1-008), regardless of configuration.
/// </summary>
public sealed class MakerCheckerAction : AggregateRoot
{
    private MakerCheckerAction()
    {
    }

    /// <summary>Logical action type, e.g. <c>template_publish</c>, <c>map_approval</c>, <c>role_permission_change</c>.</summary>
    public string ActionType { get; private set; } = null!;

    public string TargetObjectType { get; private set; } = null!;

    public Guid? TargetObjectId { get; private set; }

    public Guid MakerUserId { get; private set; }

    /// <summary>Serialised intent (the original request) replayed on approval.</summary>
    public string PendingPayloadJson { get; private set; } = null!;

    public MakerCheckerStatus Status { get; private set; }

    public Guid? CheckerUserId { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    public string? ResolutionComment { get; private set; }

    public static MakerCheckerAction Submit(
        string actionType,
        string targetObjectType,
        Guid? targetObjectId,
        Guid makerUserId,
        string pendingPayloadJson)
    {
        return new MakerCheckerAction
        {
            ActionType = Guard.NotNullOrWhiteSpace(actionType, "mc.action_type_required", "Action type is required."),
            TargetObjectType = Guard.NotNullOrWhiteSpace(targetObjectType, "mc.target_type_required", "Target object type is required."),
            TargetObjectId = targetObjectId,
            MakerUserId = makerUserId,
            PendingPayloadJson = pendingPayloadJson,
            Status = MakerCheckerStatus.Pending,
        };
    }

    /// <summary>Approve the pending action. The caller then executes the original action atomically.</summary>
    public void Approve(Guid checkerUserId, DateTimeOffset atUtc)
    {
        EnsurePending();
        if (checkerUserId == MakerUserId)
        {
            throw new DomainException("mc.self_approval_forbidden", "A maker may not approve their own action.");
        }

        Status = MakerCheckerStatus.Approved;
        CheckerUserId = checkerUserId;
        ResolvedAt = atUtc;
    }

    public void Reject(Guid checkerUserId, string reason, DateTimeOffset atUtc)
    {
        EnsurePending();
        if (checkerUserId == MakerUserId)
        {
            throw new DomainException("mc.self_approval_forbidden", "A maker may not reject their own action.");
        }

        Status = MakerCheckerStatus.Rejected;
        CheckerUserId = checkerUserId;
        ResolutionComment = Guard.MinLength(reason, 20, "mc.reason_too_short", "A rejection reason of at least 20 characters is required.");
        ResolvedAt = atUtc;
    }

    public void Expire(DateTimeOffset atUtc)
    {
        if (Status == MakerCheckerStatus.Pending)
        {
            Status = MakerCheckerStatus.Expired;
            ResolvedAt = atUtc;
        }
    }

    private void EnsurePending()
    {
        if (Status != MakerCheckerStatus.Pending)
        {
            throw new InvalidStateTransitionException("mc.not_pending", $"Action is already {Status.ToString().ToLowerInvariant()}.");
        }
    }
}
