using AuditX.Domain.Common;

namespace AuditX.Domain.Identity;

/// <summary>
/// Single-row, deployment-wide settings (the on-premises deployment serves exactly one bank). Holds
/// presentation defaults and the optional coarse-grained AD provisioning filter (US-M1-006) that
/// gates which directory users may reach the awaiting-role state.
/// </summary>
public sealed class BankSettings : Entity
{
    private BankSettings()
    {
    }

    public string BankDisplayName { get; private set; } = "AuditX";

    public string Timezone { get; private set; } = "UTC";

    public string LocaleDefault { get; private set; } = "en-GB";

    /// <summary>Optional AD Organisational Unit DN that first-login users must belong to.</summary>
    public string? AdProvisioningFilterOuDn { get; private set; }

    /// <summary>Optional AD group objectSid that first-login users must be a member of.</summary>
    public string? AdProvisioningFilterGroupSid { get; private set; }

    public static BankSettings CreateDefault(string bankDisplayName) => new()
    {
        BankDisplayName = Guard.NotNullOrWhiteSpace(bankDisplayName, "bank.name_required", "Bank display name is required."),
        Timezone = "UTC",
        LocaleDefault = "en-GB",
    };

    public void Update(string bankDisplayName, string timezone, string localeDefault)
    {
        BankDisplayName = Guard.NotNullOrWhiteSpace(bankDisplayName, "bank.name_required", "Bank display name is required.");
        Timezone = Guard.NotNullOrWhiteSpace(timezone, "bank.timezone_required", "Timezone is required.");
        LocaleDefault = Guard.NotNullOrWhiteSpace(localeDefault, "bank.locale_required", "Locale is required.");
    }

    public void SetAdProvisioningFilter(string? ouDn, string? groupSid)
    {
        AdProvisioningFilterOuDn = string.IsNullOrWhiteSpace(ouDn) ? null : ouDn.Trim();
        AdProvisioningFilterGroupSid = string.IsNullOrWhiteSpace(groupSid) ? null : groupSid.Trim();
    }
}
