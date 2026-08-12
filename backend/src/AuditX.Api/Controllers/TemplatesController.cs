using AuditX.Api.Authorization;
using AuditX.Api.Contracts;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Identity.Dtos;
using AuditX.Application.Templates.Commands;
using AuditX.Application.Templates.Dtos;
using AuditX.Application.Templates.Queries;
using AuditX.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuditX.Api.Controllers;

[Authorize]
[Route("api/v1/templates")]
public sealed class TemplatesController(IDispatcher dispatcher) : ApiControllerBase
{
    // ---- Read ----

    [RequirePermission(PermissionKeys.ViewTemplates)]
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? auditType,
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new ListTemplatesQuery(auditType, status, search, page, pageSize), cancellationToken));

    [RequirePermission(PermissionKeys.ViewTemplates)]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new GetTemplateQuery(id), cancellationToken));

    [RequirePermission(PermissionKeys.ViewTemplates)]
    [HttpGet("{id:guid}/versions")]
    public async Task<IActionResult> Versions(Guid id, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new ListTemplateVersionsQuery(id), cancellationToken));

    [RequirePermission(PermissionKeys.ViewTemplates)]
    [HttpGet("{id:guid}/versions/{versionNumber:int}")]
    public async Task<IActionResult> Version(Guid id, int versionNumber, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new GetTemplateVersionQuery(id, versionNumber), cancellationToken));

    [RequirePermission(PermissionKeys.ViewTemplates)]
    [HttpGet("{id:guid}/versions/{fromVersion:int}/diff/{toVersion:int}")]
    public async Task<IActionResult> Diff(Guid id, int fromVersion, int toVersion, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new GetTemplateDiffQuery(id, fromVersion, toVersion), cancellationToken));

    // ---- Authoring ----

    [RequirePermission(PermissionKeys.ManageTemplates)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTemplateRequest request, CancellationToken cancellationToken)
        => Created(await dispatcher.Send(new CreateTemplateCommand(request.Name, request.AuditType, request.Description), cancellationToken));

    [RequirePermission(PermissionKeys.ManageTemplates)]
    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTemplateMetadataRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new UpdateTemplateMetadataCommand(id, request.Name, request.Description), cancellationToken));

    [RequirePermission(PermissionKeys.ManageTemplates)]
    [HttpPost("{id:guid}/items")]
    public async Task<IActionResult> AddItem(Guid id, [FromBody] TemplateItemRequest request, CancellationToken cancellationToken)
        => Created(await dispatcher.Send(new AddTemplateItemCommand(
            id, request.Prompt, request.ReferenceNotes, request.ResponseType, request.SectionName, request.IsRequired, request.DefaultAssignmentRuleJson,
            request.ResponseConfigJson, request.RiskRating), cancellationToken));

    [RequirePermission(PermissionKeys.ManageTemplates)]
    [HttpPatch("{id:guid}/items/{itemId:guid}")]
    public async Task<IActionResult> UpdateItem(Guid id, Guid itemId, [FromBody] TemplateItemRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new UpdateTemplateItemCommand(
            id, itemId, request.Prompt, request.ReferenceNotes, request.ResponseType, request.SectionName, request.IsRequired, request.DefaultAssignmentRuleJson,
            request.ResponseConfigJson, request.RiskRating), cancellationToken));

    [RequirePermission(PermissionKeys.ManageTemplates)]
    [HttpDelete("{id:guid}/items/{itemId:guid}")]
    public async Task<IActionResult> RemoveItem(Guid id, Guid itemId, CancellationToken cancellationToken)
    {
        await dispatcher.Send(new RemoveTemplateItemCommand(id, itemId), cancellationToken);
        return NoContent();
    }

    [RequirePermission(PermissionKeys.ManageTemplates)]
    [HttpPost("{id:guid}/items/reorder")]
    public async Task<IActionResult> ReorderItems(Guid id, [FromBody] ReorderItemsRequest request, CancellationToken cancellationToken)
    {
        await dispatcher.Send(new ReorderTemplateItemsCommand(id, request.OrderedItemIds), cancellationToken);
        return NoContent();
    }

    [RequirePermission(PermissionKeys.ManageTemplates)]
    [HttpPost("{id:guid}/sections")]
    public async Task<IActionResult> AddSection(Guid id, [FromBody] AddSectionRequest request, CancellationToken cancellationToken)
        => Created(await dispatcher.Send(new AddTemplateSectionCommand(id, request.Name), cancellationToken));

    [RequirePermission(PermissionKeys.ManageTemplates)]
    [HttpPatch("{id:guid}/sections")]
    public async Task<IActionResult> RenameSection(Guid id, [FromBody] RenameSectionRequest request, CancellationToken cancellationToken)
    {
        await dispatcher.Send(new RenameTemplateSectionCommand(id, request.CurrentName, request.NewName), cancellationToken);
        return NoContent();
    }

    [RequirePermission(PermissionKeys.ManageTemplates)]
    [HttpDelete("{id:guid}/sections")]
    public async Task<IActionResult> RemoveSection(Guid id, [FromQuery] string name, CancellationToken cancellationToken)
    {
        await dispatcher.Send(new RemoveTemplateSectionCommand(id, name), cancellationToken);
        return NoContent();
    }

    [RequirePermission(PermissionKeys.ManageTemplates)]
    [HttpPost("{id:guid}/sections/reorder")]
    public async Task<IActionResult> ReorderSections(Guid id, [FromBody] ReorderSectionsRequest request, CancellationToken cancellationToken)
    {
        await dispatcher.Send(new ReorderTemplateSectionsCommand(id, request.OrderedSectionNames), cancellationToken);
        return NoContent();
    }

    // ---- Lifecycle ----

    [RequirePermission(PermissionKeys.ManageTemplates)]
    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> Publish(Guid id, CancellationToken cancellationToken)
    {
        var result = await dispatcher.Send(new PublishTemplateCommand(id), cancellationToken);
        return result.IsPending
            ? Accepted(new PendingActionDto(result.PendingActionId!.Value))
            : Envelope(result.Template!);
    }

    [RequirePermission(PermissionKeys.ManageTemplates)]
    [HttpPost("{id:guid}/new-draft")]
    public async Task<IActionResult> NewDraft(Guid id, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new CreateNewDraftCommand(id), cancellationToken));

    [RequirePermission(PermissionKeys.ManageTemplates)]
    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        await dispatcher.Send(new ArchiveTemplateCommand(id), cancellationToken);
        return NoContent();
    }

    [RequirePermission(PermissionKeys.ManageTemplates)]
    [HttpPost("{id:guid}/unarchive")]
    public async Task<IActionResult> Unarchive(Guid id, CancellationToken cancellationToken)
    {
        await dispatcher.Send(new UnarchiveTemplateCommand(id), cancellationToken);
        return NoContent();
    }

    [RequirePermission(PermissionKeys.ManageTemplates)]
    [HttpPost("{id:guid}/clone")]
    public async Task<IActionResult> Clone(Guid id, [FromBody] CloneTemplateRequest request, CancellationToken cancellationToken)
        => Created(await dispatcher.Send(new CloneTemplateCommand(id, request.NewName), cancellationToken));
}

[Authorize]
[Route("api/v1/rating-scales")]
public sealed class RatingScalesController(IDispatcher dispatcher) : ApiControllerBase
{
    [RequirePermission(PermissionKeys.ViewTemplates)]
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? active, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Query(new ListRatingScalesQuery(active), cancellationToken));

    [RequirePermission(PermissionKeys.ManageTemplates)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRatingScaleRequest request, CancellationToken cancellationToken)
        => Created(await dispatcher.Send(new CreateRatingScaleCommand(request.Name, request.Description, request.PointsJson), cancellationToken));

    [RequirePermission(PermissionKeys.ManageTemplates)]
    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRatingScaleRequest request, CancellationToken cancellationToken)
        => Envelope(await dispatcher.Send(new UpdateRatingScaleCommand(id, request.Name, request.Description, request.PointsJson, request.IsActive), cancellationToken));
}
