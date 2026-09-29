namespace AuditX.Domain.Enums;

/// <summary>Why a one-time credential token was issued.</summary>
public enum CredentialTokenPurpose
{
    /// <summary>First-time set-up: the user chooses their initial local password from an emailed invite link.</summary>
    Invite,

    /// <summary>Self-service or admin-triggered reset of a forgotten local password.</summary>
    Reset,
}
