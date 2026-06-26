using AuditX.Application.Abstractions.Identity;

namespace AuditX.Infrastructure.Identity;

/// <summary>A seeded development user (used only when the Development identity provider is active).</summary>
public sealed record DevUser(
    string SamAccountName,
    string UserPrincipalName,
    string ObjectSid,
    string Email,
    string FirstName,
    string LastName,
    string Password,
    string? SeedRoleName);

/// <summary>
/// Fixed set of development users so the platform is usable locally without an Active Directory. The
/// administrator is seeded with the Administrator role to bootstrap role assignment; the others arrive
/// via just-in-time provisioning in the awaiting-role state, exactly as a real AD user would.
/// NEVER enabled in production (gated by <c>Identity:Provider = Development</c>).
/// </summary>
public static class DevUsers
{
    public const string DefaultPassword = "Passw0rd!";

    public static readonly IReadOnlyList<DevUser> All =
    [
        new("admin", "admin@auditx.local", "S-1-5-21-AUDITX-1001", "admin@auditx.local", "Ada", "Min", DefaultPassword, Domain.Authorization.BuiltInRoles.AdministratorName),
        new("manager", "manager@auditx.local", "S-1-5-21-AUDITX-1002", "manager@auditx.local", "Mary", "Manager", DefaultPassword, null),
        new("auditor", "auditor@auditx.local", "S-1-5-21-AUDITX-1003", "auditor@auditx.local", "Andy", "Auditor", DefaultPassword, null),
        new("auditee", "auditee@auditx.local", "S-1-5-21-AUDITX-1004", "auditee@auditx.local", "Aud", "Itee", DefaultPassword, null),
    ];

    public static DirectoryUser ToDirectoryUser(this DevUser user) => new(
        user.SamAccountName,
        user.UserPrincipalName,
        user.ObjectSid,
        user.Email,
        user.FirstName,
        user.LastName,
        $"{user.FirstName} {user.LastName}",
        IsEnabled: true);
}
