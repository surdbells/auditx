namespace AuditX.Domain.Enums;

/// <summary>Identifies what kind of principal performed an audited action.</summary>
public enum ActorType
{
    /// <summary>A normal AuditX end user, identified by <c>actor_user_id</c>.</summary>
    User,

    /// <summary>The platform itself (background jobs, scheduled tasks, system-initiated transitions).</summary>
    System,

    /// <summary>An ITANDT support engineer acting through the controlled, time-bounded support channel.</summary>
    ItandtSupport,
}
