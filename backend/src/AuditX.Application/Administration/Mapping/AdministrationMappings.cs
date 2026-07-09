using AuditX.Application.Administration.Dtos;
using AuditX.Domain.Administration;
using AuditX.Domain.Identity;

namespace AuditX.Application.Administration.Mapping;

public static class AdministrationMappings
{
    public static BankSettingsDto ToDto(this BankSettings bank) => new(
        bank.BankDisplayName, bank.Timezone, bank.LocaleDefault,
        bank.AdProvisioningFilterOuDn, bank.AdProvisioningFilterGroupSid,
        bank.MaxEvidenceFileMb, bank.MaxAuditEvidenceGb, bank.AllowOverlappingPlanPeriods,
        bank.AllowAuditLaunchBeforeApproval,
        bank.PrimaryColor, bank.AccentColor, bank.LogoDataUri, bank.IconDataUri);

    public static SupportChannelStatusDto ToStatusDto(this SupportChannelSession? session, DateTimeOffset nowUtc)
        => session is null
            ? new SupportChannelStatusDto(false, [], null, null, null)
            : new SupportChannelStatusDto(session.IsActiveAt(nowUtc), session.EngineerIdentifiers.ToArray(), session.EnabledAt, session.ExpiresAt, session.RevokedAt);

    public static ReleaseInstallDto ToDto(this ReleaseInstall release) => new(
        release.Id, release.Version, release.ManifestSha256, release.ChangeRecordReference, release.Status.ToString(), release.Detail, release.CreatedAt);

    public static RestoreDrillDto ToDto(this RestoreDrill drill) => new(
        drill.Id, drill.ExecutedAt, drill.Outcome.ToString(), drill.Details);

    public static ObjectRestoreRequestDto ToDto(this ObjectRestoreRequest request) => new(
        request.Id, request.ObjectType, request.ObjectId, request.SnapshotDate, request.Justification,
        request.Status.ToString(), request.RequestedByUserId, request.DecidedByUserId, request.DecisionComment);
}
