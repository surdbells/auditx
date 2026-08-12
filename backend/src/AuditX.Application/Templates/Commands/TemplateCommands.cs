using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Json;
using AuditX.Application.Common.Messaging;
using AuditX.Application.Exceptions.Commands;
using AuditX.Application.Templates;
using AuditX.Application.Templates.Dtos;
using AuditX.Application.Templates.Mapping;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.Enums;
using AuditX.Domain.Templates;
using FluentValidation;

namespace AuditX.Application.Templates.Commands;

internal static class TemplateParsing
{
    public static ResponseType ParseResponseType(string? value)
    {
        var normalised = (value ?? string.Empty).Replace("_", string.Empty);
        return Enum.TryParse<ResponseType>(normalised, ignoreCase: true, out var parsed)
            ? parsed
            : throw new ConflictException("invalid_response_type", $"Unknown response type '{value}'.");
    }

    public static ExceptionSeverity? ParseRiskRating(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : ExceptionParsing.ParseSeverity(value);

    /// <summary>Pull the referenced rating-scale id out of a Rating item's opaque ResponseConfigJson, if present.</summary>
    public static Guid? ParseRatingScaleId(string? responseConfigJson)
    {
        if (string.IsNullOrWhiteSpace(responseConfigJson))
        {
            return null;
        }

        try
        {
            return AppJson.Deserialize<RatingResponseConfig>(responseConfigJson)?.RatingScaleId;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}

public sealed record CreateTemplateCommand(string Name, string AuditType, string? Description) : ICommand<TemplateDto>;

public sealed class CreateTemplateCommandValidator : AbstractValidator<CreateTemplateCommand>
{
    public CreateTemplateCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AuditType).NotEmpty().MaximumLength(100);
    }
}

public sealed class CreateTemplateCommandHandler(ITemplateRepository templates, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<CreateTemplateCommand, TemplateDto>
{
    public async Task<TemplateDto> Handle(CreateTemplateCommand command, CancellationToken cancellationToken)
    {
        if (await templates.GetByNameAndTypeAsync(command.Name.Trim(), command.AuditType.Trim(), cancellationToken) is not null)
        {
            throw new ConflictException("template_name_taken", $"A template named '{command.Name}' already exists for audit type '{command.AuditType}'.");
        }

        var template = Template.CreateDraft(command.Name.Trim(), command.AuditType.Trim(), command.Description);
        templates.Add(template);
        audit.Record(AuditEventTypes.TemplateCreated, AuditTargetTypes.Template, template.Id,
            after: new { template.Name, template.AuditType, status = template.Status.ToString() });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return template.ToDto();
    }
}

public sealed record UpdateTemplateMetadataCommand(Guid Id, string Name, string? Description) : ICommand<TemplateDto>;

public sealed class UpdateTemplateMetadataCommandHandler(ITemplateRepository templates, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateTemplateMetadataCommand, TemplateDto>
{
    public async Task<TemplateDto> Handle(UpdateTemplateMetadataCommand command, CancellationToken cancellationToken)
    {
        var template = await templates.GetByIdAsync(command.Id, cancellationToken) ?? throw new NotFoundException("Template", command.Id);
        var existing = await templates.GetByNameAndTypeAsync(command.Name.Trim(), template.AuditType, cancellationToken);
        if (existing is not null && existing.Id != template.Id)
        {
            throw new ConflictException("template_name_taken", $"A template named '{command.Name}' already exists for this audit type.");
        }

        template.UpdateMetadata(command.Name.Trim(), command.Description);
        audit.Record(AuditEventTypes.TemplateUpdated, AuditTargetTypes.Template, template.Id, after: new { template.Name });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return template.ToDto();
    }
}

public sealed record AddTemplateItemCommand(
    Guid TemplateId, string Prompt, string? ReferenceNotes, string ResponseType,
    string? SectionName, bool IsRequired, string? DefaultAssignmentRuleJson,
    string? ResponseConfigJson = null, string? RiskRating = null, Guid? ControlId = null) : ICommand<TemplateDto>;

public sealed class AddTemplateItemCommandValidator : AbstractValidator<AddTemplateItemCommand>
{
    public AddTemplateItemCommandValidator() => RuleFor(x => x.Prompt).NotEmpty().MaximumLength(2000);
}

public sealed class AddTemplateItemCommandHandler(ITemplateRepository templates, IRatingScaleRepository ratingScales, IControlRepository controls, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<AddTemplateItemCommand, TemplateDto>
{
    public async Task<TemplateDto> Handle(AddTemplateItemCommand command, CancellationToken cancellationToken)
    {
        var template = await templates.GetByIdAsync(command.TemplateId, cancellationToken) ?? throw new NotFoundException("Template", command.TemplateId);
        var responseType = TemplateParsing.ParseResponseType(command.ResponseType);
        await TemplateItemValidation.EnsureRatingScaleValidAsync(responseType, command.ResponseConfigJson, ratingScales, cancellationToken);
        await TemplateItemValidation.EnsureControlExistsAsync(command.ControlId, controls, cancellationToken);

        var item = template.AddItem(command.Prompt, command.ReferenceNotes, responseType,
            command.SectionName, command.IsRequired, command.DefaultAssignmentRuleJson,
            command.ResponseConfigJson, TemplateParsing.ParseRiskRating(command.RiskRating), command.ControlId);
        audit.Record(AuditEventTypes.TemplateItemAdded, AuditTargetTypes.Template, template.Id, payload: new { itemId = item.Id, item.Prompt });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return template.ToDto();
    }
}

public sealed record UpdateTemplateItemCommand(
    Guid TemplateId, Guid ItemId, string Prompt, string? ReferenceNotes, string ResponseType,
    string? SectionName, bool IsRequired, string? DefaultAssignmentRuleJson,
    string? ResponseConfigJson = null, string? RiskRating = null, Guid? ControlId = null) : ICommand<TemplateDto>;

public sealed class UpdateTemplateItemCommandHandler(ITemplateRepository templates, IRatingScaleRepository ratingScales, IControlRepository controls, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateTemplateItemCommand, TemplateDto>
{
    public async Task<TemplateDto> Handle(UpdateTemplateItemCommand command, CancellationToken cancellationToken)
    {
        var template = await templates.GetByIdAsync(command.TemplateId, cancellationToken) ?? throw new NotFoundException("Template", command.TemplateId);
        var responseType = TemplateParsing.ParseResponseType(command.ResponseType);
        await TemplateItemValidation.EnsureRatingScaleValidAsync(responseType, command.ResponseConfigJson, ratingScales, cancellationToken);
        await TemplateItemValidation.EnsureControlExistsAsync(command.ControlId, controls, cancellationToken);

        template.UpdateItem(command.ItemId, command.Prompt, command.ReferenceNotes, responseType,
            command.SectionName, command.IsRequired, command.DefaultAssignmentRuleJson,
            command.ResponseConfigJson, TemplateParsing.ParseRiskRating(command.RiskRating), command.ControlId);
        audit.Record(AuditEventTypes.TemplateItemEdited, AuditTargetTypes.Template, template.Id, payload: new { itemId = command.ItemId });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return template.ToDto();
    }
}

/// <summary>A Rating item must reference an existing rating scale; a control-linked item must reference an existing control.</summary>
internal static class TemplateItemValidation
{
    public static async Task EnsureRatingScaleValidAsync(ResponseType responseType, string? responseConfigJson, IRatingScaleRepository ratingScales, CancellationToken cancellationToken)
    {
        if (responseType != ResponseType.Rating)
        {
            return;
        }

        var ratingScaleId = TemplateParsing.ParseRatingScaleId(responseConfigJson)
            ?? throw new ConflictException("template.rating_scale_required", "A rating item must reference a rating scale.");
        if (await ratingScales.GetByIdAsync(ratingScaleId, cancellationToken) is null)
        {
            throw new NotFoundException("Rating scale", ratingScaleId);
        }
    }

    public static async Task EnsureControlExistsAsync(Guid? controlId, IControlRepository controls, CancellationToken cancellationToken)
    {
        if (controlId is { } id && await controls.GetByIdAsync(id, cancellationToken) is null)
        {
            throw new NotFoundException("Control", id);
        }
    }
}

public sealed record RemoveTemplateItemCommand(Guid TemplateId, Guid ItemId) : ICommand<Unit>;

public sealed class RemoveTemplateItemCommandHandler(ITemplateRepository templates, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<RemoveTemplateItemCommand, Unit>
{
    public async Task<Unit> Handle(RemoveTemplateItemCommand command, CancellationToken cancellationToken)
    {
        var template = await templates.GetByIdAsync(command.TemplateId, cancellationToken) ?? throw new NotFoundException("Template", command.TemplateId);
        template.RemoveItem(command.ItemId);
        audit.Record(AuditEventTypes.TemplateItemRemoved, AuditTargetTypes.Template, template.Id, payload: new { itemId = command.ItemId });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed record ReorderTemplateItemsCommand(Guid TemplateId, IReadOnlyList<Guid> OrderedItemIds) : ICommand<Unit>;

public sealed class ReorderTemplateItemsCommandHandler(ITemplateRepository templates, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<ReorderTemplateItemsCommand, Unit>
{
    public async Task<Unit> Handle(ReorderTemplateItemsCommand command, CancellationToken cancellationToken)
    {
        var template = await templates.GetByIdAsync(command.TemplateId, cancellationToken) ?? throw new NotFoundException("Template", command.TemplateId);
        template.ReorderItems(command.OrderedItemIds);
        audit.Record(AuditEventTypes.TemplateItemsReordered, AuditTargetTypes.Template, template.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed record ReorderTemplateSectionsCommand(Guid TemplateId, IReadOnlyList<string> OrderedSectionNames) : ICommand<Unit>;

public sealed class ReorderTemplateSectionsCommandHandler(ITemplateRepository templates, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<ReorderTemplateSectionsCommand, Unit>
{
    public async Task<Unit> Handle(ReorderTemplateSectionsCommand command, CancellationToken cancellationToken)
    {
        var template = await templates.GetByIdAsync(command.TemplateId, cancellationToken) ?? throw new NotFoundException("Template", command.TemplateId);
        template.ReorderSections(command.OrderedSectionNames);
        audit.Record(AuditEventTypes.TemplateSectionsReordered, AuditTargetTypes.Template, template.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed record AddTemplateSectionCommand(Guid TemplateId, string Name) : ICommand<TemplateDto>;

public sealed class AddTemplateSectionCommandHandler(ITemplateRepository templates, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<AddTemplateSectionCommand, TemplateDto>
{
    public async Task<TemplateDto> Handle(AddTemplateSectionCommand command, CancellationToken cancellationToken)
    {
        var template = await templates.GetByIdAsync(command.TemplateId, cancellationToken) ?? throw new NotFoundException("Template", command.TemplateId);
        template.AddSection(command.Name);
        audit.Record(AuditEventTypes.TemplateSectionAdded, AuditTargetTypes.Template, template.Id, payload: new { command.Name });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return template.ToDto();
    }
}

public sealed record RenameTemplateSectionCommand(Guid TemplateId, string CurrentName, string NewName) : ICommand<Unit>;

public sealed class RenameTemplateSectionCommandHandler(ITemplateRepository templates, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<RenameTemplateSectionCommand, Unit>
{
    public async Task<Unit> Handle(RenameTemplateSectionCommand command, CancellationToken cancellationToken)
    {
        var template = await templates.GetByIdAsync(command.TemplateId, cancellationToken) ?? throw new NotFoundException("Template", command.TemplateId);
        template.RenameSection(command.CurrentName, command.NewName);
        audit.Record(AuditEventTypes.TemplateSectionRenamed, AuditTargetTypes.Template, template.Id, payload: new { command.CurrentName, command.NewName });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed record RemoveTemplateSectionCommand(Guid TemplateId, string Name) : ICommand<Unit>;

public sealed class RemoveTemplateSectionCommandHandler(ITemplateRepository templates, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<RemoveTemplateSectionCommand, Unit>
{
    public async Task<Unit> Handle(RemoveTemplateSectionCommand command, CancellationToken cancellationToken)
    {
        var template = await templates.GetByIdAsync(command.TemplateId, cancellationToken) ?? throw new NotFoundException("Template", command.TemplateId);
        template.RemoveSection(command.Name);
        audit.Record(AuditEventTypes.TemplateSectionRemoved, AuditTargetTypes.Template, template.Id, payload: new { command.Name });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed record CreateNewDraftCommand(Guid TemplateId) : ICommand<TemplateDto>;

public sealed class CreateNewDraftCommandHandler(ITemplateRepository templates, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<CreateNewDraftCommand, TemplateDto>
{
    public async Task<TemplateDto> Handle(CreateNewDraftCommand command, CancellationToken cancellationToken)
    {
        var template = await templates.GetByIdAsync(command.TemplateId, cancellationToken) ?? throw new NotFoundException("Template", command.TemplateId);
        template.CreateNewDraft();
        audit.Record(AuditEventTypes.TemplateDraftCreated, AuditTargetTypes.Template, template.Id, payload: new { version = template.CurrentVersion });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return template.ToDto();
    }
}

public sealed record ArchiveTemplateCommand(Guid TemplateId) : ICommand<Unit>;

public sealed class ArchiveTemplateCommandHandler(ITemplateRepository templates, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<ArchiveTemplateCommand, Unit>
{
    public async Task<Unit> Handle(ArchiveTemplateCommand command, CancellationToken cancellationToken)
    {
        var template = await templates.GetByIdAsync(command.TemplateId, cancellationToken) ?? throw new NotFoundException("Template", command.TemplateId);
        template.Archive();
        audit.Record(AuditEventTypes.TemplateArchived, AuditTargetTypes.Template, template.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed record UnarchiveTemplateCommand(Guid TemplateId) : ICommand<Unit>;

public sealed class UnarchiveTemplateCommandHandler(ITemplateRepository templates, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<UnarchiveTemplateCommand, Unit>
{
    public async Task<Unit> Handle(UnarchiveTemplateCommand command, CancellationToken cancellationToken)
    {
        var template = await templates.GetByIdAsync(command.TemplateId, cancellationToken) ?? throw new NotFoundException("Template", command.TemplateId);
        template.Unarchive();
        audit.Record(AuditEventTypes.TemplateUnarchived, AuditTargetTypes.Template, template.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed record CloneTemplateCommand(Guid TemplateId, string NewName) : ICommand<TemplateDto>;

public sealed class CloneTemplateCommandValidator : AbstractValidator<CloneTemplateCommand>
{
    public CloneTemplateCommandValidator() => RuleFor(x => x.NewName).NotEmpty().MaximumLength(200);
}

public sealed class CloneTemplateCommandHandler(ITemplateRepository templates, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<CloneTemplateCommand, TemplateDto>
{
    public async Task<TemplateDto> Handle(CloneTemplateCommand command, CancellationToken cancellationToken)
    {
        var source = await templates.GetByIdAsync(command.TemplateId, cancellationToken) ?? throw new NotFoundException("Template", command.TemplateId);
        if (await templates.GetByNameAndTypeAsync(command.NewName.Trim(), source.AuditType, cancellationToken) is not null)
        {
            throw new ConflictException("template_name_taken", $"A template named '{command.NewName}' already exists for this audit type.");
        }

        var clone = Template.CreateDraft(command.NewName.Trim(), source.AuditType, source.Description, clonedFromTemplateId: source.Id);
        foreach (var section in source.Sections.OrderBy(s => s.OrderIndex))
        {
            clone.AddSection(section.Name);
        }

        foreach (var item in source.Items.OrderBy(i => i.OrderIndex))
        {
            clone.AddItem(item.Prompt, item.ReferenceNotes, item.ResponseType, item.SectionName, item.IsRequired,
                item.DefaultAssignmentRuleJson, item.ResponseConfigJson, item.RiskRating, item.ControlId);
        }

        templates.Add(clone);
        audit.Record(AuditEventTypes.TemplateCloned, AuditTargetTypes.Template, clone.Id, payload: new { clonedFrom = source.Id, clone.Name });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return clone.ToDto();
    }
}
