namespace AuditX.Domain.Enums;

/// <summary>
/// Lifecycle state of an AuditX user. A user authenticated by Active Directory for the first time is
/// provisioned as <see cref="AwaitingRoleAssignment"/> with no roles (principle of least privilege),
/// and only becomes <see cref="Active"/> once an administrator grants at least one role.
/// </summary>
public enum UserStatus
{
    AwaitingRoleAssignment,
    Active,
    Deactivated,
}
