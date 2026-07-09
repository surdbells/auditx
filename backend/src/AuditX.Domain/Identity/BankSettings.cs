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

    /// <summary>Maximum size of a single evidence file, in megabytes (US-M15; configurable limit).</summary>
    public int MaxEvidenceFileMb { get; private set; } = 50;

    /// <summary>Maximum total evidence storage per audit, in gigabytes.</summary>
    public int MaxAuditEvidenceGb { get; private set; } = 5;

    /// <summary>When true, a Pass verdict also requires a comment (BR-M5-002 bank policy).</summary>
    public bool RequireCommentOnPass { get; private set; }

    /// <summary>
    /// When true, annual plans may cover overlapping periods; when false (default), the overlap check is enforced.
    /// Configurable so banks that run concurrent/rolling plans can opt out of the single-period-at-a-time rule.
    /// </summary>
    public bool AllowOverlappingPlanPeriods { get; private set; }

    /// <summary>
    /// When true, an audit may be launched from a plan item before the annual plan is formally approved
    /// (any status except Closed); when false (default), audits can only be launched from an Approved plan.
    /// Lets banks that don't run a formal Audit-Committee approval gate turn plan items into audits directly.
    /// </summary>
    public bool AllowAuditLaunchBeforeApproval { get; private set; }

    /// <summary>Primary brand colour (hex, e.g. <c>#4f46e5</c>) applied to the UI theme.</summary>
    public string PrimaryColor { get; private set; } = "#4f46e5";

    /// <summary>Accent brand colour (hex, e.g. <c>#7c3aed</c>) applied to the UI theme.</summary>
    public string AccentColor { get; private set; } = "#7c3aed";

    /// <summary>Optional organisation logo as a <c>data:image/*</c> URI, shown in the app shell.</summary>
    public string? LogoDataUri { get; private set; }

    /// <summary>Optional favicon/app icon as a <c>data:image/*</c> URI, used as the browser-tab icon.</summary>
    public string? IconDataUri { get; private set; }

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

    public void SetRequireCommentOnPass(bool value) => RequireCommentOnPass = value;

    public void SetAllowOverlappingPlanPeriods(bool value) => AllowOverlappingPlanPeriods = value;

    public void SetAllowAuditLaunchBeforeApproval(bool value) => AllowAuditLaunchBeforeApproval = value;

    /// <summary>Sets the branding: primary/accent colours are required; logo/icon are optional data URIs.</summary>
    public void SetBranding(string primaryColor, string accentColor, string? logoDataUri, string? iconDataUri)
    {
        PrimaryColor = Guard.NotNullOrWhiteSpace(primaryColor, "bank.primary_color_required", "Primary colour is required.");
        AccentColor = Guard.NotNullOrWhiteSpace(accentColor, "bank.accent_color_required", "Accent colour is required.");
        LogoDataUri = string.IsNullOrWhiteSpace(logoDataUri) ? null : logoDataUri;
        IconDataUri = string.IsNullOrWhiteSpace(iconDataUri) ? null : iconDataUri;
    }

    public void SetResourceLimits(int maxEvidenceFileMb, int maxAuditEvidenceGb)
    {
        MaxEvidenceFileMb = maxEvidenceFileMb is <= 0 or > 1024 ? MaxEvidenceFileMb : maxEvidenceFileMb;
        MaxAuditEvidenceGb = maxAuditEvidenceGb is <= 0 or > 1024 ? MaxAuditEvidenceGb : maxAuditEvidenceGb;
    }
}
