using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Sharing;

/// <summary>
/// A shareable, revocable, optionally-expiring reference to a target resource (D3-B) — today a generated report.
/// SECURITY: the link is a <b>reference, not an access grant</b>. Resolving a link never returns content and never
/// widens access; the viewer must still authenticate and pass the target's own permission check. The value it adds
/// over the raw resource URL is an opaque slug that can be <see cref="Revoke"/>d or expire (<see cref="ExpiresAt"/>).
/// A standalone soft-deletable, rowversion-guarded aggregate. Revocation is retained (not deleted) for the audit trail.
/// </summary>
public sealed class SharedLink : Entity, ISoftDeletable
{
    private SharedLink()
    {
    }

    /// <summary>The opaque, URL-safe token that appears in the shared URL. Unique.</summary>
    public string Slug { get; private set; } = null!;

    public SharedLinkTargetType TargetType { get; private set; }

    public Guid TargetId { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    /// <summary>Optional hard expiry; null = never expires (until revoked).</summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public Guid? RevokedByUserId { get; private set; }

    public byte[] Version { get; private set; } = [];

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? DeletedBy { get; private set; }

    public static SharedLink Create(
        string slug, SharedLinkTargetType targetType, Guid targetId, Guid createdByUserId, DateTimeOffset? expiresAt)
    {
        Guard.Against(targetId == Guid.Empty, "shared_link.target_required", "A target is required.");
        Guard.Against(createdByUserId == Guid.Empty, "shared_link.creator_required", "A creator is required.");
        return new SharedLink
        {
            Slug = Guard.NotNullOrWhiteSpace(slug, "shared_link.slug_required", "A slug is required."),
            TargetType = targetType,
            TargetId = targetId,
            CreatedByUserId = createdByUserId,
            ExpiresAt = expiresAt,
        };
    }

    /// <summary>Revokes the link so it no longer resolves. Idempotent — a second revoke keeps the first actor/time.</summary>
    public void Revoke(Guid userId, DateTimeOffset nowUtc)
    {
        if (RevokedAt is not null)
        {
            return;
        }

        RevokedAt = nowUtc;
        RevokedByUserId = userId;
    }

    /// <summary>True while the link still resolves: not deleted, not revoked, and not past its expiry.</summary>
    public bool IsActive(DateTimeOffset nowUtc)
        => !IsDeleted && RevokedAt is null && (ExpiresAt is null || ExpiresAt > nowUtc);

    public void SoftDelete(Guid? deletedBy, DateTimeOffset deletedAtUtc)
    {
        if (IsDeleted)
        {
            return;
        }

        IsDeleted = true;
        DeletedBy = deletedBy;
        DeletedAt = deletedAtUtc;
    }
}
