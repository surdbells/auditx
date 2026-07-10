using AuditX.Api.Authorization;
using AuditX.Api.Contracts;
using AuditX.Application.Ac.Commands;
using AuditX.Application.Ac.Queries;
using AuditX.Application.Common.Messaging;
using AuditX.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuditX.Api.Controllers;

/// <summary>
/// M13 Audit-Committee pack generation, retrieval, download (verify-on-read), CIA review/approve/distribute and the
/// distribution log. Generate requires GenerateACPack + CIA (the CIA half is enforced in-handler). AC members see
/// only approved/distributed packs (enforced in-handler). Approve/Distribute are single-actor CIA (NOT maker-checker).
/// </summary>
[Authorize]
[Route("api/v1/ac-packs")]
public sealed class AcPacksController(IDispatcher dispatcher) : ApiControllerBase
{
    [RequirePermission(PermissionKeys.GenerateAcPack)]
    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] GenerateAcPackRequest request, CancellationToken cancellationToken)
    {
        var result = await dispatcher.Send(
            new GenerateAcPackCommand(request.PeriodStart, request.PeriodEnd, request.AcMeetingLabel, request.Docx ?? false), cancellationToken);
        return Accepted(result);
    }

    [RequirePermission(PermissionKeys.ViewAcPacks)]
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new ListAcPacksQuery(status, page, pageSize), cancellationToken));

    [RequirePermission(PermissionKeys.ViewAcPacks)]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new GetAcPackQuery(id), cancellationToken));

    [RequirePermission(PermissionKeys.ViewAcPacks)]
    [HttpGet("{id:guid}/analytics")]
    public async Task<IActionResult> Analytics(Guid id, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new GetAcPackAnalyticsQuery(id), cancellationToken));

    [RequirePermission(PermissionKeys.ViewAcPacks)]
    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, [FromQuery] string format, CancellationToken cancellationToken)
    {
        var result = await dispatcher.Query(new DownloadAcPackArtefactQuery(id, format), cancellationToken);
        Response.Headers["X-Ac-Pack-Sha256"] = result.Sha256Hash;
        return File(result.Content, result.ContentType, result.Filename);
    }

    [RequirePermission(PermissionKeys.Cia)]
    [HttpPatch("{id:guid}/cia-text")]
    public async Task<IActionResult> UpdateCiaText(Guid id, [FromBody] UpdateAcPackCiaTextRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new UpdateAcPackCiaTextCommand(id, request.SupplementaryText), cancellationToken));

    [RequirePermission(PermissionKeys.Cia)]
    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveAcPackRequest? request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new ApproveAcPackCommand(id, request?.SupplementaryText), cancellationToken));

    [RequirePermission(PermissionKeys.Cia)]
    [HttpPost("{id:guid}/distribute")]
    public async Task<IActionResult> Distribute(Guid id, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new DistributeAcPackCommand(id), cancellationToken));

    [RequirePermission(PermissionKeys.ViewAcPacks)]
    [HttpGet("{id:guid}/distributions")]
    public async Task<IActionResult> Distributions(Guid id, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new ListAcPackDistributionsQuery(id, page, pageSize), cancellationToken));
}
