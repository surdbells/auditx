namespace AuditX.Application.Identity.Passwords;

/// <summary>How an administrator establishes a user's initial (or reset) local password.</summary>
public enum InitialPasswordMethod
{
    /// <summary>The admin types the password; the user must change it at first sign-in.</summary>
    SetPassword,

    /// <summary>The system generates a strong temporary password (returned once); the user must change it at first sign-in.</summary>
    GenerateTemp,

    /// <summary>The user receives an emailed link to set their own password; no password is set now.</summary>
    Invite,
}
