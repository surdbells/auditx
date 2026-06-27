namespace AuditX.Domain.Configuration;

/// <summary>
/// The catalogue of bank-configuration domains carried by the generic versioned config store (M12). Each domain has
/// its own per-domain JSON schema and its own version timeline; exactly one version is active per domain. The one
/// fully-wired editable domain in this slice is <see cref="ExceptionDefaults"/>; further domains
/// (escalation_rules, audit_workflow, …) drop in later without a schema change to the store.
/// </summary>
public static class ConfigurationDomains
{
    /// <summary>Severity → remediation target days, the recurrence lookback window, and the recurrence threshold (M6/M9).</summary>
    public const string ExceptionDefaults = "exception_defaults";

    /// <summary>Every domain the store recognises. A draft/activate for an unknown domain is rejected (422).</summary>
    public static readonly IReadOnlyList<string> All = [ExceptionDefaults];

    public static bool IsKnown(string? domain) =>
        domain is not null && All.Contains(domain, StringComparer.Ordinal);
}
