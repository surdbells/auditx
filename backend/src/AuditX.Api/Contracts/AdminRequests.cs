namespace AuditX.Api.Contracts;

public sealed record UpdateBankSettingsRequest(
    string BankDisplayName, string Timezone, string LocaleDefault, string? AdProvisioningFilterOuDn, string? AdProvisioningFilterGroupSid,
    bool AllowOverlappingPlanPeriods = false, bool AllowAuditLaunchBeforeApproval = false);

public sealed record UpdateResourceLimitsRequest(int MaxEvidenceFileMb, int MaxAuditEvidenceGb);

public sealed record BulkUserIdsRequest(IReadOnlyList<Guid> UserIds);

public sealed record BulkImportRequest(string CsvContent);

public sealed record EnableSupportChannelRequest(IReadOnlyList<string> EngineerIdentifiers, int DurationMinutes);

public sealed record InstallReleaseRequest(
    string Version, string ManifestSha256, string ChangeRecordReference, string SignatureBase64, string ManifestContentBase64);

public sealed record RecordRestoreDrillRequest(string Outcome, string? Details);

public sealed record RequestObjectRestoreRequest(string ObjectType, Guid ObjectId, DateTimeOffset SnapshotDate, string Justification);

public sealed record DecideObjectRestoreRequest(bool Approve, string? Comment);
