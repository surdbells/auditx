namespace AuditX.Domain.Enums;

/// <summary>Kinds of bank-infrastructure integration AuditX connects to (M14).</summary>
public enum IntegrationType
{
    ActiveDirectory,
    Smtp,
    Sms,
    FileStorage,
    Siem,
    Saml,
    Oidc,
    Webhook,
}

/// <summary>Rolling health state of an integration, derived from recent call outcomes.</summary>
public enum IntegrationHealthState
{
    Healthy,
    Degraded,
    Failing,
}

/// <summary>Lifecycle of a single outbound webhook delivery attempt set.</summary>
public enum WebhookDeliveryStatus
{
    Pending,
    Delivered,
    Failed,
    DeadLetter,
}
