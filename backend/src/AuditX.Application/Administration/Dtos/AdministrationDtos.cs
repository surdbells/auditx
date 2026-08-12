namespace AuditX.Application.Administration.Dtos;

public sealed record BankSettingsDto(
    string BankDisplayName,
    string Timezone,
    string LocaleDefault,
    string? AdProvisioningFilterOuDn,
    string? AdProvisioningFilterGroupSid,
    int MaxEvidenceFileMb,
    int MaxAuditEvidenceGb,
    bool AllowOverlappingPlanPeriods,
    bool AllowAuditLaunchBeforeApproval,
    bool AllowMinorPlanRevisionAfterApproval,
    string PrimaryColor,
    string AccentColor,
    string? LogoDataUri,
    string? IconDataUri,
    bool ShowOverview,
    bool ShowWalkthrough,
    int ReportRetentionMonths,
    bool AutoStartWalkthrough,
    int IdleTimeoutMinutes,
    int IdleWarningSeconds);

public sealed record ResourceLimitsDto(int MaxEvidenceFileMb, int MaxAuditEvidenceGb);

/// <summary>Public branding surface served anonymously so the shell/login can theme before authentication.</summary>
public sealed record BrandingDto(
    string OrganizationName, string PrimaryColor, string AccentColor, string? LogoDataUri, string? IconDataUri,
    bool ShowOverview, bool ShowWalkthrough, bool AutoStartWalkthrough,
    int IdleTimeoutMinutes, int IdleWarningSeconds);

public sealed record BulkOperationErrorDto(string Identifier, string Message);

public sealed record BulkOperationResultDto(int SuccessCount, IReadOnlyList<BulkOperationErrorDto> Errors);

public sealed record SupportChannelStatusDto(
    bool Active,
    IReadOnlyList<string> EngineerIdentifiers,
    DateTimeOffset? EnabledAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? RevokedAt);

public sealed record ReleaseInstallDto(
    Guid Id,
    string Version,
    string ManifestSha256,
    string ChangeRecordReference,
    string Status,
    string? Detail,
    DateTimeOffset CreatedAt);

public sealed record RestoreDrillDto(Guid Id, DateTimeOffset ExecutedAt, string Outcome, string Details);

public sealed record ObjectRestoreRequestDto(
    Guid Id,
    string ObjectType,
    Guid ObjectId,
    DateTimeOffset SnapshotDate,
    string Justification,
    string Status,
    Guid RequestedByUserId,
    Guid? DecidedByUserId,
    string? DecisionComment);

/// <summary>A dependency probe (database, cache, …). Status is healthy | degraded | unhealthy.</summary>
public sealed record SystemHealthCheckDto(string Name, string Status, string? Detail);

/// <summary>A labelled operational count for the health dashboard.</summary>
public sealed record SystemHealthMetricDto(string Label, long Value);

public sealed record SystemHealthDto(
    string Status,
    IReadOnlyList<SystemHealthCheckDto> Checks,
    IReadOnlyList<SystemHealthMetricDto> Metrics,
    string Version,
    string Environment,
    long UptimeSeconds,
    DateTimeOffset GeneratedAt);
