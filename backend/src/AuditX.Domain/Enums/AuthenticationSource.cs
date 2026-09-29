namespace AuditX.Domain.Enums;

/// <summary>Where a user's credentials live — the directory (AD/dev) or a local institution-managed password.</summary>
public enum AuthenticationSource
{
    /// <summary>Authenticated against Active Directory (or the development provider). AuditX stores no password.</summary>
    Directory,

    /// <summary>Authenticated against a local password credential hashed and stored by AuditX.</summary>
    Local,
}
