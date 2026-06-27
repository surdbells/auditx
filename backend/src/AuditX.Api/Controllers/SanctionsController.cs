using AuditX.Api.Authorization;
using AuditX.Api.Contracts;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Sanctions.Commands;
using AuditX.Application.Sanctions.Queries;
using AuditX.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuditX.Api.Controllers;

[Authorize]
public sealed class SanctionsController(IDispatcher dispatcher) : ApiControllerBase
{
    // ---- Tracker + detail ----

    [RequirePermission(PermissionKeys.ViewSanctions)]
    [HttpGet("api/v1/sanctions/cases")]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] string? cursor, [FromQuery] int? limit, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new ListSanctionsCasesQuery(status, cursor, limit), cancellationToken));

    [RequirePermission(PermissionKeys.ViewSanctions)]
    [HttpGet("api/v1/sanctions/cases/{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new GetSanctionsCaseByIdQuery(id), cancellationToken));

    [RequirePermission(PermissionKeys.DcMember)]
    [HttpGet("api/v1/sanctions/dc-queue")]
    public async Task<IActionResult> DcQueue([FromQuery] string? cursor, [FromQuery] int? limit, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new ListDcQueueQuery(cursor, limit), cancellationToken));

    // ---- Trigger (exception-scoped route) ----

    [RequirePermission(PermissionKeys.TriggerSanctions)]
    [HttpPost("api/v1/exceptions/{id:guid}/sanctions/trigger")]
    public async Task<IActionResult> Trigger(Guid id, [FromBody] TriggerSanctionsRequest request, CancellationToken cancellationToken)
        => Created(await dispatcher.Send(new TriggerSanctionsCommand(id, request.SubjectUserId), cancellationToken));

    // ---- Recommendation ----

    [RequirePermission(PermissionKeys.RecommendSanction)]
    [HttpPatch("api/v1/sanctions/cases/{id:guid}/recommendation")]
    public async Task<IActionResult> Recommend(Guid id, [FromBody] RecordRecommendationRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new RecordRecommendationCommand(id, request.Recommendation, request.DeviationReason, request.Version), cancellationToken));

    [RequirePermission(PermissionKeys.RecommendSanction)]
    [HttpPost("api/v1/sanctions/cases/{id:guid}/submit")]
    public async Task<IActionResult> Submit(Guid id, [FromBody] VersionOnlyRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new SubmitRecommendationCommand(id, request.Version), cancellationToken));

    // ---- Dossier ----

    [RequirePermission(PermissionKeys.ViewSanctions)]
    [HttpPost("api/v1/sanctions/cases/{id:guid}/dossier")]
    public async Task<IActionResult> Dossier(Guid id, CancellationToken cancellationToken)
    {
        var result = await dispatcher.Send(new GenerateDossierCommand(id), cancellationToken);
        Response.Headers["X-Dossier-Sha256"] = result.Sha256Hash;
        return File(result.Content, result.ContentType, result.Filename);
    }

    // ---- HR outcome / DC referral / DC decision ----

    [RequirePermission(PermissionKeys.RecordHrOutcome)]
    [HttpPost("api/v1/sanctions/cases/{id:guid}/hr-outcome")]
    public async Task<IActionResult> HrOutcome(Guid id, [FromBody] RecordHrOutcomeRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new RecordHrOutcomeCommand(id, request.OutcomeType, request.Detail, request.EvidenceFileId, request.Version), cancellationToken));

    [RequirePermission(PermissionKeys.ReferToDc)]
    [HttpPost("api/v1/sanctions/cases/{id:guid}/dc-refer")]
    public async Task<IActionResult> ReferToDc(Guid id, [FromBody] ReferToDcRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new ReferToDcCommand(id, request.ReferralReason, request.Version), cancellationToken));

    [RequirePermission(PermissionKeys.DcMember)]
    [HttpPost("api/v1/sanctions/cases/{id:guid}/dc-decision")]
    public async Task<IActionResult> DcDecision(Guid id, [FromBody] RecordDcDecisionRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new RecordDcDecisionCommand(id, request.Decision, request.Rationale, request.VotingRecord, request.Version), cancellationToken));

    // ---- Appeals ----

    [RequirePermission(PermissionKeys.FileAppeal)]
    [HttpPost("api/v1/sanctions/cases/{id:guid}/appeals")]
    public async Task<IActionResult> FileAppeal(Guid id, [FromBody] FileAppealRequest request, CancellationToken cancellationToken)
        => Created(await dispatcher.Send(new FileAppealCommand(id, request.Basis, request.EvidenceFileId, request.Version), cancellationToken));

    [RequirePermission(PermissionKeys.DecideAppeal)]
    [HttpPost("api/v1/sanctions/appeals/{appealId:guid}/decision")]
    public async Task<IActionResult> DecideAppeal(Guid appealId, [FromBody] DecideAppealRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new DecideAppealCommand(appealId, request.Outcome, request.Rationale, request.Version), cancellationToken));

    // ---- Close ----

    // Closure is a terminal write (the decision is already recorded), so it is gated by the audit-function
    // case-management write permission — not the read permission ViewSanctions (which would let any viewer
    // terminally close a case, a SoD breach).
    [RequirePermission(PermissionKeys.RecommendSanction)]
    [HttpPost("api/v1/sanctions/cases/{id:guid}/close")]
    public async Task<IActionResult> Close(Guid id, [FromBody] VersionOnlyRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new CloseCaseCommand(id, request.Version), cancellationToken));
}
