namespace AuditX.Domain.Enums;

/// <summary>How a user's identity was established for a session.</summary>
public enum AuthenticationMethod
{
    /// <summary>Integrated Windows Authentication via Kerberos (seamless SSO on domain-joined workstations).</summary>
    Kerberos,

    /// <summary>Forms fallback: username + password validated by an LDAP bind against Active Directory.</summary>
    Forms,

    /// <summary>Local development provider (seeded users). Never enabled in production.</summary>
    Development,

    /// <summary>A local (institution-managed) password validated against a stored PBKDF2 hash.</summary>
    LocalPassword,
}
