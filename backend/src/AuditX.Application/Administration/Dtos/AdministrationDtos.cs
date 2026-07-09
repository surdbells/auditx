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
    string PrimaryColor,
    string AccentColor,
    string? LogoDataUri,
    string? IconDataUri);

public sealed record ResourceLimitsDto(int MaxEvidenceFileMb, int MaxAuditEvidenceGb);

/// <summary>Public branding surface served anonymously so the shell/login can theme before authentication.</summary>
public sealed record BrandingDto(
    string OrganizationName, string PrimaryColor, string AccentColor, string? LogoDataUri, string? IconDataUri);

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

public sealed record SystemHealthDto(
    string Status,
    int ActiveUserCount,
    int TotalUserCount,
    int TemplateCount,
    int IntegrationCount,
    DateTimeOffset GeneratedAt);
