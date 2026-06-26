using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Identity.Events;

/// <summary>Convenience base for identity domain events; stamps the occurrence time.</summary>
public abstract record IdentityEvent : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record UserProvisionedEvent(Guid UserId, string SamAccountName, string Email) : IdentityEvent;

public sealed record UserLoggedInEvent(Guid UserId, AuthenticationMethod Method) : IdentityEvent;

public sealed record UserDeactivatedEvent(Guid UserId, Guid? DeactivatedBy) : IdentityEvent;

public sealed record RoleGrantedEvent(Guid UserId, Guid RoleId, Guid? GrantedBy) : IdentityEvent;

public sealed record RoleRevokedEvent(Guid UserId, Guid RoleId, Guid? RevokedBy) : IdentityEvent;

public sealed record DelegationStartedEvent(
    Guid UserRoleId,
    Guid FromUserId,
    Guid ToUserId,
    Guid RoleId,
    DateTimeOffset EndsAtUtc) : IdentityEvent;

public sealed record DelegationEndedEvent(Guid UserRoleId, Guid ToUserId, Guid RoleId) : IdentityEvent;

public sealed record DelegationRevokedEvent(Guid UserRoleId, Guid ToUserId, Guid RoleId, Guid RevokedBy) : IdentityEvent;

public sealed record MakerCheckerSubmittedEvent(Guid ActionId, string ActionType, Guid MakerUserId) : IdentityEvent;

public sealed record MakerCheckerApprovedEvent(Guid ActionId, string ActionType, Guid CheckerUserId) : IdentityEvent;

public sealed record MakerCheckerRejectedEvent(Guid ActionId, string ActionType, Guid CheckerUserId, string Reason) : IdentityEvent;
