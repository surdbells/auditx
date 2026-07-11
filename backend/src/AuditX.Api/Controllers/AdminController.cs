using AuditX.Api.Authorization;
using AuditX.Api.Contracts;
using AuditX.Application.Administration.Commands;
using AuditX.Application.Administration.Queries;
using AuditX.Application.Common.Messaging;
using AuditX.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuditX.Api.Controllers;

[Authorize]
[Route("api/v1/admin")]
public sealed class AdminController(IDispatcher dispatcher) : ApiControllerBase
{
    // ---- Bank settings & limits ----

    [RequirePermission(PermissionKeys.ViewBankSettings)]
    [HttpGet("bank-settings")]
    public async Task<IActionResult> GetBankSettings(CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new GetBankSettingsQuery(), cancellationToken));

    [RequirePermission(PermissionKeys.ManageBankSettings)]
    [HttpPatch("bank-settings")]
    public async Task<IActionResult> UpdateBankSettings([FromBody] UpdateBankSettingsRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new UpdateBankSettingsCommand(
            request.BankDisplayName, request.Timezone, request.LocaleDefault, request.AdProvisioningFilterOuDn, request.AdProvisioningFilterGroupSid,
            request.AllowOverlappingPlanPeriods, request.AllowAuditLaunchBeforeApproval,
            request.PrimaryColor, request.AccentColor, request.LogoDataUri, request.IconDataUri,
            request.ShowOverview, request.ShowWalkthrough, request.ReportRetentionMonths, request.AutoStartWalkthrough), cancellationToken));

    [RequirePermission(PermissionKeys.ConfigureLimits)]
    [HttpPatch("resource-limits")]
    public async Task<IActionResult> UpdateResourceLimits([FromBody] UpdateResourceLimitsRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new UpdateResourceLimitsCommand(request.MaxEvidenceFileMb, request.MaxAuditEvidenceGb), cancellationToken));

    // ---- Bulk user operations ----

    [RequirePermission(PermissionKeys.ManageUsers)]
    [HttpPost("users/bulk-deactivate")]
    public async Task<IActionResult> BulkDeactivate([FromBody] BulkUserIdsRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new BulkDeactivateUsersCommand(request.UserIds), cancellationToken));

    [RequirePermission(PermissionKeys.ManageUsers)]
    [HttpPost("users/bulk-import")]
    public async Task<IActionResult> BulkImport([FromBody] BulkImportRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new BulkImportUsersCommand(request.CsvContent), cancellationToken));

    // ---- ITANDT support channel ----

    [RequirePermission(PermissionKeys.ManageSupportChannel)]
    [HttpGet("support-channel")]
    public async Task<IActionResult> SupportChannelStatus(CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new GetSupportChannelStatusQuery(), cancellationToken));

    [RequirePermission(PermissionKeys.ManageSupportChannel)]
    [HttpPost("support-channel/enable")]
    public async Task<IActionResult> EnableSupportChannel([FromBody] EnableSupportChannelRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new EnableSupportChannelCommand(request.EngineerIdentifiers, request.DurationMinutes), cancellationToken));

    [RequirePermission(PermissionKeys.ManageSupportChannel)]
    [HttpPost("support-channel/revoke")]
    public async Task<IActionResult> RevokeSupportChannel(CancellationToken cancellationToken)
    {
        await dispatcher.Send(new RevokeSupportChannelCommand(), cancellationToken);
        return NoContent();
    }

    // ---- Signed offline releases ----

    [RequirePermission(PermissionKeys.InstallReleases)]
    [HttpGet("releases")]
    public async Task<IActionResult> ListReleases(CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new ListReleasesQuery(), cancellationToken));

    [RequirePermission(PermissionKeys.InstallReleases)]
    [HttpPost("releases/install")]
    public async Task<IActionResult> InstallRelease([FromBody] InstallReleaseRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new InstallReleaseCommand(
            request.Version, request.ManifestSha256, request.ChangeRecordReference, request.SignatureBase64, request.ManifestContentBase64), cancellationToken));

    // ---- Backup / restore ----

    [RequirePermission(PermissionKeys.ManageRetention)]
    [HttpGet("restore-drills")]
    public async Task<IActionResult> ListRestoreDrills(CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new ListRestoreDrillsQuery(), cancellationToken));

    [RequirePermission(PermissionKeys.ManageRetention)]
    [HttpPost("restore-drills")]
    public async Task<IActionResult> RecordRestoreDrill([FromBody] RecordRestoreDrillRequest request, CancellationToken cancellationToken)
        => Created(await dispatcher.Send(new RecordRestoreDrillCommand(request.Outcome, request.Details), cancellationToken));

    [RequirePermission(PermissionKeys.ExecRestore)]
    [HttpPost("restore/object")]
    public async Task<IActionResult> RequestObjectRestore([FromBody] RequestObjectRestoreRequest request, CancellationToken cancellationToken)
        => Created(await dispatcher.Send(new RequestObjectRestoreCommand(request.ObjectType, request.ObjectId, request.SnapshotDate, request.Justification), cancellationToken));

    [RequirePermission(PermissionKeys.ExecRestore)]
    [HttpPost("restore/object/{id:guid}/decide")]
    public async Task<IActionResult> DecideObjectRestore(Guid id, [FromBody] DecideObjectRestoreRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new DecideObjectRestoreCommand(id, request.Approve, request.Comment), cancellationToken));

    // ---- System health ----

    [RequirePermission(PermissionKeys.ViewSystemHealth)]
    [HttpGet("system-health")]
    public async Task<IActionResult> SystemHealth(CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new GetSystemHealthQuery(), cancellationToken));
}
