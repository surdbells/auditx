using AuditX.Domain.Common;

namespace AuditX.Domain.Administration;

/// <summary>
/// A time-bounded ITANDT support-channel session (US-M15-031). Disabled by default; enabled by Institution IT
/// for named engineers and a capped duration. All actions performed through it are audited with
/// actor_type = itandt_support. The bank may revoke an active session at any time.
/// </summary>
public sealed class SupportChannelSession : AggregateRoot
{
    public const int MaxDurationMinutes = 480;
    public const int DefaultDurationMinutes = 240;

    private readonly List<string> _engineerIdentifiers = [];

    private SupportChannelSession()
    {
    }

    public IReadOnlyList<string> EngineerIdentifiers => _engineerIdentifiers.AsReadOnly();

    public Guid EnabledByUserId { get; private set; }

    public DateTimeOffset EnabledAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public Guid? RevokedByUserId { get; private set; }

    public static SupportChannelSession Enable(IEnumerable<string> engineerIdentifiers, Guid enabledByUserId, DateTimeOffset nowUtc, int durationMinutes)
    {
        var ids = engineerIdentifiers.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        if (ids.Count == 0)
        {
            throw new DomainException("support.no_engineers", "At least one named engineer is required.");
        }

        var duration = durationMinutes is <= 0 or > MaxDurationMinutes ? DefaultDurationMinutes : durationMinutes;
        var session = new SupportChannelSession
        {
            EnabledByUserId = enabledByUserId,
            EnabledAt = nowUtc,
            ExpiresAt = nowUtc.AddMinutes(duration),
        };
        session._engineerIdentifiers.AddRange(ids);
        return session;
    }

    public void Revoke(Guid revokedByUserId, DateTimeOffset nowUtc)
    {
        if (RevokedAt is not null)
        {
            return;
        }

        RevokedAt = nowUtc;
        RevokedByUserId = revokedByUserId;
    }

    public bool IsActiveAt(DateTimeOffset nowUtc) => RevokedAt is null && nowUtc < ExpiresAt;
}
