using AuditX.Domain.Common;

namespace AuditX.Domain.Configuration.Events;

public abstract record ConfigurationEvent : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>Raised when a new (inactive) configuration version is drafted for a domain.</summary>
public sealed record ConfigurationVersionCreatedEvent(Guid ConfigurationId, string Domain, int VersionNumber, Guid CreatedByUserId) : ConfigurationEvent;

/// <summary>Raised when a configuration version becomes the active one for its domain.</summary>
public sealed record ConfigurationVersionActivatedEvent(Guid ConfigurationId, string Domain, int VersionNumber, Guid ActivatedByUserId) : ConfigurationEvent;

/// <summary>
/// Raised when a rollback creates a NEW forward version that reproduces a prior version's definition (the prior
/// version is left untouched). Carries the version copied FROM and the new version number that now holds the content.
/// </summary>
public sealed record ConfigurationRolledBackEvent(Guid ConfigurationId, string Domain, int FromVersionNumber, int NewVersionNumber, Guid ActorUserId) : ConfigurationEvent;
