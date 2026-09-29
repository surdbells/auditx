using AuditX.Application.Identity.Roles;

namespace AuditX.Api.Contracts;

public sealed record LoginRequest(string Username, string Password);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record ForgotPasswordRequest(string UsernameOrEmail);

public sealed record ResetPasswordRequest(string Token, string NewPassword);

public sealed record UpdateNotificationPreferencesRequest(string? PreferencesJson);

public sealed record UpdateUserRequest(string Status);

public sealed record CreateUserRequest(string Email, string FirstName, string LastName, string? ExternalId, IReadOnlyList<string>? RoleNames);

public sealed record UpdateUserProfileRequest(string Email, string FirstName, string LastName, string? DisplayName);

public sealed record SetUserCapacityRequest(decimal? CapacityDays);

public sealed record SetUserManagerRequest(Guid? ManagerId);

public sealed record UpdateMyPreferencesRequest(string Timezone, string Locale);

public sealed record GrantRoleRequest(Guid RoleId, string? ScopeValue);

public sealed record CreateDelegationRequest(Guid ToUserId, Guid RoleId, DateTimeOffset StartDate, DateTimeOffset EndDate);

public sealed record CreateRoleRequest(
    string Name,
    string Description,
    IReadOnlyList<RolePermissionInput> Permissions,
    IReadOnlyList<Guid> ParentRoleIds);

public sealed record UpdateRoleRequest(
    string Name,
    string Description,
    IReadOnlyList<RolePermissionInput> Permissions,
    IReadOnlyList<Guid> ParentRoleIds);

public sealed record RejectActionRequest(string Reason);

public sealed record ConfigureGateRequest(string ActionType, bool IsEnabled, string? CheckerRoleName, bool AllowMakerAsChecker);
