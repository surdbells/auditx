namespace AuditX.Infrastructure.Options;

/// <summary>SMTP + SMS gateway configuration for notifications (bound from <c>Notifications</c>).</summary>
public sealed class NotificationOptions
{
    public const string SectionName = "Notifications";

    public SmtpOptions Smtp { get; set; } = new();

    public SmsOptions Sms { get; set; } = new();

    public TeamsOptions Teams { get; set; } = new();

    /// <summary>
    /// Public base URL of the AuditX web app (e.g. <c>https://auditx.bank.local</c>), used to build deep links in
    /// notification emails. Leave empty in development; templates then fall back to a relative path.
    /// </summary>
    public string WebBaseUrl { get; set; } = string.Empty;
}

public sealed class TeamsOptions
{
    /// <summary>
    /// Microsoft Teams incoming-webhook URL for the default channel. When empty, Teams notifications are skipped
    /// (never attempted). Treat as a secret — supply via environment / secret store, not source control.
    /// </summary>
    public string? DefaultWebhookUrl { get; set; }
}

public sealed class SmtpOptions
{
    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    public bool UseStartTls { get; set; } = true;

    public string FromAddress { get; set; } = "auditx@bank.local";

    public string FromName { get; set; } = "AuditX";

    public string? Username { get; set; }

    public string? Password { get; set; }
}

public sealed class SmsOptions
{
    /// <summary>Bank SMS gateway endpoint; when empty, SMS sends fail permanently (no external SaaS on-prem).</summary>
    public string? GatewayUrl { get; set; }
}
