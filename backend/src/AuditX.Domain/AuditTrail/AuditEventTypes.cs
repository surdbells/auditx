namespace AuditX.Domain.AuditTrail;

/// <summary>Canonical audit-trail event-type identifiers for M1 (identity) events.</summary>
public static class AuditEventTypes
{
    // Authentication & session
    public const string UserProvisioned = "user_provisioned";
    public const string LoginSucceeded = "login_success";
    public const string LoginFailed = "login_failure";
    public const string LoggedOut = "logout";
    public const string SessionTerminated = "session_terminated";

    // User lifecycle
    public const string UserDeactivated = "user_deactivated";
    public const string UserReactivated = "user_reactivated";
    public const string NotificationPreferencesUpdated = "notification_preferences_updated";

    // Roles & permissions
    public const string RoleCreated = "role_created";
    public const string RoleUpdated = "role_updated";
    public const string RoleArchived = "role_archived";
    public const string RoleUnarchived = "role_unarchived";
    public const string RoleGranted = "role_granted";
    public const string RoleRevoked = "role_revoked";

    // Delegation
    public const string DelegationStarted = "delegation_started";
    public const string DelegationEnded = "delegation_ended";
    public const string DelegationRevoked = "delegation_revoked";

    // Maker-checker
    public const string MakerCheckerSubmitted = "maker_checker_submitted";
    public const string MakerCheckerApproved = "maker_checker_approved";
    public const string MakerCheckerRejected = "maker_checker_rejected";
}

/// <summary>Canonical target-object-type identifiers used in audit-trail entries.</summary>
public static class AuditTargetTypes
{
    public const string User = "user";
    public const string Role = "role";
    public const string UserRole = "user_role";
    public const string MakerCheckerAction = "maker_checker_action";
    public const string Session = "session";
    public const string BankSettings = "bank_settings";
}
