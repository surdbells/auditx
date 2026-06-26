using AuditX.Domain.Common;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Integrations;

/// <summary>
/// Configuration for one bank-infrastructure integration (M14). Credentials are stored only as
/// ciphertext (<see cref="EncryptedCredentials"/>) — encryption/decryption happens in the
/// infrastructure layer via DataProtection, and credentials are never returned to clients.
/// </summary>
public sealed class IntegrationConfiguration : AggregateRoot, ISoftDeletable
{
    private IntegrationConfiguration()
    {
    }

    public IntegrationType Type { get; private set; }

    public string Name { get; private set; } = null!;

    /// <summary>Non-secret connection details (endpoint, sender domain, base DN, etc.).</summary>
    public string ConnectionDetailsJson { get; private set; } = "{}";

    public byte[]? EncryptedCredentials { get; private set; }

    public int TimeoutSeconds { get; private set; } = 30;

    public Guid? FallbackIntegrationId { get; private set; }

    public bool IsPrimary { get; private set; } = true;

    public bool IsActive { get; private set; } = true;

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? DeletedBy { get; private set; }

    /// <summary>Authentication-provider integrations are mutually exclusive when active (US-M14-006).</summary>
    public bool IsAuthProvider => Type is IntegrationType.ActiveDirectory or IntegrationType.Saml or IntegrationType.Oidc;

    public static IntegrationConfiguration Create(
        IntegrationType type,
        string name,
        string connectionDetailsJson,
        byte[]? encryptedCredentials,
        int timeoutSeconds)
    {
        return new IntegrationConfiguration
        {
            Type = type,
            Name = Guard.NotNullOrWhiteSpace(name, "integration.name_required", "Integration name is required."),
            ConnectionDetailsJson = string.IsNullOrWhiteSpace(connectionDetailsJson) ? "{}" : connectionDetailsJson,
            EncryptedCredentials = encryptedCredentials,
            TimeoutSeconds = NormaliseTimeout(timeoutSeconds),
            IsActive = true,
            IsPrimary = true,
        };
    }

    public void UpdateConnection(string connectionDetailsJson, byte[]? newEncryptedCredentials, int timeoutSeconds)
    {
        ConnectionDetailsJson = string.IsNullOrWhiteSpace(connectionDetailsJson) ? "{}" : connectionDetailsJson;
        TimeoutSeconds = NormaliseTimeout(timeoutSeconds);
        if (newEncryptedCredentials is { Length: > 0 })
        {
            EncryptedCredentials = newEncryptedCredentials;
        }
    }

    public void SetFallback(Guid? fallbackIntegrationId)
    {
        if (fallbackIntegrationId == Id)
        {
            throw new DomainException("integration.self_fallback", "An integration cannot be its own fallback.");
        }

        FallbackIntegrationId = fallbackIntegrationId;
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    public void SoftDelete(Guid? deletedBy, DateTimeOffset deletedAtUtc)
    {
        IsDeleted = true;
        DeletedBy = deletedBy;
        DeletedAt = deletedAtUtc;
        IsActive = false;
    }

    private static int NormaliseTimeout(int timeoutSeconds) => timeoutSeconds is <= 0 or > 600 ? 30 : timeoutSeconds;
}
