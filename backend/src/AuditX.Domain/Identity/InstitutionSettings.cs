using AuditX.Domain.Common;

namespace AuditX.Domain.Identity;

/// <summary>
/// Single-row, deployment-wide settings (the on-premises deployment serves exactly one bank). Holds
/// presentation defaults and the optional coarse-grained AD provisioning filter (US-M1-006) that
/// gates which directory users may reach the awaiting-role state.
/// </summary>
public sealed class InstitutionSettings : Entity
{
    private InstitutionSettings()
    {
    }

    public string InstitutionDisplayName { get; private set; } = "AuditX";

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

    /// <summary>
    /// When true, an Approved plan's items may still be edited directly via a "minor revision" (date shift,
    /// no re-approval) after approval; when false (default), an Approved plan is fully locked and the only
    /// way to change it is a "material revision", which re-opens it for Audit-Committee re-approval — an
    /// audit must happen as planned unless the bank explicitly opts into lighter-weight post-approval edits.
    /// </summary>
    public bool AllowMinorPlanRevisionAfterApproval { get; private set; }

    /// <summary>Primary brand colour (hex, e.g. <c>#4f46e5</c>) applied to the UI theme.</summary>
    public string PrimaryColor { get; private set; } = "#4f46e5";

    /// <summary>Accent brand colour (hex, e.g. <c>#7c3aed</c>) applied to the UI theme.</summary>
    public string AccentColor { get; private set; } = "#7c3aed";

    /// <summary>Optional organisation logo as a <c>data:image/*</c> URI, shown in the app shell.</summary>
    public string? LogoDataUri { get; private set; }

    /// <summary>Optional favicon/app icon as a <c>data:image/*</c> URI, used as the browser-tab icon.</summary>
    public string? IconDataUri { get; private set; }

    /// <summary>When true (default), each page's "Overview" (About-this-page) button is shown in the page guide.</summary>
    public bool ShowOverview { get; private set; } = true;

    /// <summary>When true (default), each page's "Walkthrough" button is shown in the page guide.</summary>
    public bool ShowWalkthrough { get; private set; } = true;

    /// <summary>
    /// When true (default), the walkthrough auto-starts the first time a user opens a page on a device.
    /// Independent of <see cref="ShowWalkthrough"/> — but a hidden walkthrough never auto-starts.
    /// </summary>
    public bool AutoStartWalkthrough { get; private set; } = true;

    /// <summary>
    /// Months a completed report is retained before its artefacts are expired (no longer served). 0 (default) = retain
    /// indefinitely / no expiry. Bounded to 600 months (50 years).
    /// </summary>
    public int ReportRetentionMonths { get; private set; }

    /// <summary>
    /// Minutes of user inactivity before the idle warning appears. 0 = idle logout disabled.
    /// Bounded to 480 minutes (8 hours). Default 15 — a secure baseline for an audit platform.
    /// </summary>
    public int IdleTimeoutMinutes { get; private set; } = 15;

    /// <summary>
    /// Seconds the idle warning counts down before the user is signed out automatically.
    /// Bounded to 20–600 seconds (the 20s floor keeps WCAG 2.2.1 "Timing Adjustable" satisfiable). Default 60.
    /// </summary>
    public int IdleWarningSeconds { get; private set; } = 60;

    // ---- Local-password policy (M1 local authentication) ----

    /// <summary>
    /// Master switch: when true, users may hold a local (institution-managed) password credential and
    /// authenticate without Active Directory. Default false — AuditX authenticates against AD only and
    /// stores no passwords unless this is explicitly turned on.
    /// </summary>
    public bool EnableLocalPasswords { get; private set; }

    /// <summary>Minimum local-password length. Bounded 8–128. Default 12.</summary>
    public int PasswordMinLength { get; private set; } = 12;

    /// <summary>Require at least one uppercase letter in a local password. Default true.</summary>
    public bool PasswordRequireUppercase { get; private set; } = true;

    /// <summary>Require at least one lowercase letter in a local password. Default true.</summary>
    public bool PasswordRequireLowercase { get; private set; } = true;

    /// <summary>Require at least one digit in a local password. Default true.</summary>
    public bool PasswordRequireDigit { get; private set; } = true;

    /// <summary>Require at least one non-alphanumeric symbol in a local password. Default true.</summary>
    public bool PasswordRequireSymbol { get; private set; } = true;

    /// <summary>Number of previous passwords a new password may not reuse. 0 = no history. Bounded 0–24. Default 5.</summary>
    public int PasswordHistoryDepth { get; private set; } = 5;

    /// <summary>Days before a local password expires and must be changed. 0 = never expires. Bounded 0–3650. Default 90.</summary>
    public int PasswordExpiryDays { get; private set; } = 90;

    /// <summary>Consecutive failed local sign-ins before the account is locked. 0 = lockout disabled. Bounded 0–20. Default 5.</summary>
    public int PasswordMaxFailedAttempts { get; private set; } = 5;

    /// <summary>Minutes an account stays locked after too many failed attempts. Bounded 1–1440. Default 15.</summary>
    public int PasswordLockoutMinutes { get; private set; } = 15;

    public static InstitutionSettings CreateDefault(string institutionDisplayName) => new()
    {
        InstitutionDisplayName = Guard.NotNullOrWhiteSpace(institutionDisplayName, "institution.name_required", "Institution display name is required."),
        Timezone = "UTC",
        LocaleDefault = "en-GB",
    };

    public void Update(string institutionDisplayName, string timezone, string localeDefault)
    {
        InstitutionDisplayName = Guard.NotNullOrWhiteSpace(institutionDisplayName, "institution.name_required", "Institution display name is required.");
        Timezone = Guard.NotNullOrWhiteSpace(timezone, "institution.timezone_required", "Timezone is required.");
        LocaleDefault = Guard.NotNullOrWhiteSpace(localeDefault, "institution.locale_required", "Locale is required.");
    }

    public void SetAdProvisioningFilter(string? ouDn, string? groupSid)
    {
        AdProvisioningFilterOuDn = string.IsNullOrWhiteSpace(ouDn) ? null : ouDn.Trim();
        AdProvisioningFilterGroupSid = string.IsNullOrWhiteSpace(groupSid) ? null : groupSid.Trim();
    }

    public void SetRequireCommentOnPass(bool value) => RequireCommentOnPass = value;

    public void SetAllowOverlappingPlanPeriods(bool value) => AllowOverlappingPlanPeriods = value;

    public void SetAllowAuditLaunchBeforeApproval(bool value) => AllowAuditLaunchBeforeApproval = value;

    public void SetAllowMinorPlanRevisionAfterApproval(bool value) => AllowMinorPlanRevisionAfterApproval = value;

    /// <summary>Sets the page-guide behaviour: the "Overview" / "Walkthrough" buttons and the auto-start tour.</summary>
    public void SetPageGuideVisibility(bool showOverview, bool showWalkthrough, bool autoStartWalkthrough)
    {
        ShowOverview = showOverview;
        ShowWalkthrough = showWalkthrough;
        AutoStartWalkthrough = autoStartWalkthrough;
    }

    /// <summary>Sets the report retention period in months (0 = retain indefinitely). Out-of-range values are ignored.</summary>
    public void SetReportRetentionMonths(int months)
    {
        if (months is >= 0 and <= 600)
        {
            ReportRetentionMonths = months;
        }
    }

    /// <summary>Sets the idle-logout policy (minutes to the warning, 0 = disabled; warning countdown seconds). Out-of-range values are ignored.</summary>
    public void SetIdleTimeout(int minutes, int warningSeconds)
    {
        if (minutes is >= 0 and <= 480)
        {
            IdleTimeoutMinutes = minutes;
        }

        if (warningSeconds is >= 20 and <= 600)
        {
            IdleWarningSeconds = warningSeconds;
        }
    }

    /// <summary>Sets the branding: primary/accent colours are required; logo/icon are optional data URIs.</summary>
    public void SetBranding(string primaryColor, string accentColor, string? logoDataUri, string? iconDataUri)
    {
        PrimaryColor = Guard.NotNullOrWhiteSpace(primaryColor, "institution.primary_color_required", "Primary colour is required.");
        AccentColor = Guard.NotNullOrWhiteSpace(accentColor, "institution.accent_color_required", "Accent colour is required.");
        LogoDataUri = string.IsNullOrWhiteSpace(logoDataUri) ? null : logoDataUri;
        IconDataUri = string.IsNullOrWhiteSpace(iconDataUri) ? null : iconDataUri;
    }

    /// <summary>Toggle only the local-password master switch, leaving the rest of the policy intact.</summary>
    public void SetEnableLocalPasswords(bool value) => EnableLocalPasswords = value;

    public void SetResourceLimits(int maxEvidenceFileMb, int maxAuditEvidenceGb)
    {
        MaxEvidenceFileMb = maxEvidenceFileMb is <= 0 or > 1024 ? MaxEvidenceFileMb : maxEvidenceFileMb;
        MaxAuditEvidenceGb = maxAuditEvidenceGb is <= 0 or > 1024 ? MaxAuditEvidenceGb : maxAuditEvidenceGb;
    }

    /// <summary>
    /// Sets the local-password policy. Bounds are enforced here as defence-in-depth (the application-layer
    /// validator surfaces friendly errors); out-of-range values are rejected rather than silently clamped
    /// because this is a security control.
    /// </summary>
    public void SetPasswordPolicy(
        bool enableLocalPasswords,
        int minLength,
        bool requireUppercase,
        bool requireLowercase,
        bool requireDigit,
        bool requireSymbol,
        int historyDepth,
        int expiryDays,
        int maxFailedAttempts,
        int lockoutMinutes)
    {
        Guard.Against(minLength is < 8 or > 128, "institution.password_min_length_invalid", "Minimum password length must be between 8 and 128.");
        Guard.Against(historyDepth is < 0 or > 24, "institution.password_history_invalid", "Password history depth must be between 0 and 24.");
        Guard.Against(expiryDays is < 0 or > 3650, "institution.password_expiry_invalid", "Password expiry days must be between 0 and 3650.");
        Guard.Against(maxFailedAttempts is < 0 or > 20, "institution.password_lockout_attempts_invalid", "Max failed attempts must be between 0 and 20.");
        Guard.Against(lockoutMinutes is < 1 or > 1440, "institution.password_lockout_minutes_invalid", "Lockout duration must be between 1 and 1440 minutes.");

        EnableLocalPasswords = enableLocalPasswords;
        PasswordMinLength = minLength;
        PasswordRequireUppercase = requireUppercase;
        PasswordRequireLowercase = requireLowercase;
        PasswordRequireDigit = requireDigit;
        PasswordRequireSymbol = requireSymbol;
        PasswordHistoryDepth = historyDepth;
        PasswordExpiryDays = expiryDays;
        PasswordMaxFailedAttempts = maxFailedAttempts;
        PasswordLockoutMinutes = lockoutMinutes;
    }
}
