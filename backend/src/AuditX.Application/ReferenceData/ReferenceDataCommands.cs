using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.ReferenceData;
using FluentValidation;

namespace AuditX.Application.ReferenceData;

// ---- Create ----

public sealed record CreateReferenceDataItemCommand(string Category, string Code, string Label, string? Description, int SortOrder)
    : ICommand<ReferenceDataItemDto>;

public sealed class CreateReferenceDataItemCommandValidator : AbstractValidator<CreateReferenceDataItemCommand>
{
    public CreateReferenceDataItemCommandValidator()
    {
        RuleFor(x => x.Category).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Label).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public sealed class CreateReferenceDataItemCommandHandler(IReferenceDataRepository items, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<CreateReferenceDataItemCommand, ReferenceDataItemDto>
{
    public async Task<ReferenceDataItemDto> Handle(CreateReferenceDataItemCommand command, CancellationToken cancellationToken)
    {
        var category = command.Category.Trim();
        var code = command.Code.Trim();
        if (await items.ExistsAsync(category, code, cancellationToken))
        {
            throw new ConflictException("reference_data.code_taken", $"A '{category}' item with code '{code}' already exists.");
        }

        var item = ReferenceDataItem.Create(category, code, command.Label, command.Description, command.SortOrder);
        items.Add(item);
        audit.Record(AuditEventTypes.ReferenceDataItemCreated, AuditTargetTypes.ReferenceDataItem, item.Id, after: new { item.Category, item.Code, item.Label });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return item.ToDto();
    }
}

// ---- Update ----

public sealed record UpdateReferenceDataItemCommand(Guid Id, string Label, string? Description, int SortOrder)
    : ICommand<ReferenceDataItemDto>;

public sealed class UpdateReferenceDataItemCommandValidator : AbstractValidator<UpdateReferenceDataItemCommand>
{
    public UpdateReferenceDataItemCommandValidator()
    {
        RuleFor(x => x.Label).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public sealed class UpdateReferenceDataItemCommandHandler(IReferenceDataRepository items, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateReferenceDataItemCommand, ReferenceDataItemDto>
{
    public async Task<ReferenceDataItemDto> Handle(UpdateReferenceDataItemCommand command, CancellationToken cancellationToken)
    {
        var item = await items.GetByIdAsync(command.Id, cancellationToken) ?? throw new NotFoundException("Reference-data item", command.Id);
        item.Update(command.Label, command.Description, command.SortOrder);
        audit.Record(AuditEventTypes.ReferenceDataItemUpdated, AuditTargetTypes.ReferenceDataItem, item.Id, after: new { item.Label, item.SortOrder });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return item.ToDto();
    }
}

// ---- Archive / Reactivate ----

public sealed record ArchiveReferenceDataItemCommand(Guid Id) : ICommand<ReferenceDataItemDto>;

public sealed class ArchiveReferenceDataItemCommandHandler(IReferenceDataRepository items, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<ArchiveReferenceDataItemCommand, ReferenceDataItemDto>
{
    public async Task<ReferenceDataItemDto> Handle(ArchiveReferenceDataItemCommand command, CancellationToken cancellationToken)
    {
        var item = await items.GetByIdAsync(command.Id, cancellationToken) ?? throw new NotFoundException("Reference-data item", command.Id);
        item.Deactivate();
        audit.Record(AuditEventTypes.ReferenceDataItemArchived, AuditTargetTypes.ReferenceDataItem, item.Id, after: new { item.Category, item.Code });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return item.ToDto();
    }
}

public sealed record ReactivateReferenceDataItemCommand(Guid Id) : ICommand<ReferenceDataItemDto>;

public sealed class ReactivateReferenceDataItemCommandHandler(IReferenceDataRepository items, IAuditRecorder audit, IUnitOfWork unitOfWork)
    : ICommandHandler<ReactivateReferenceDataItemCommand, ReferenceDataItemDto>
{
    public async Task<ReferenceDataItemDto> Handle(ReactivateReferenceDataItemCommand command, CancellationToken cancellationToken)
    {
        var item = await items.GetByIdAsync(command.Id, cancellationToken) ?? throw new NotFoundException("Reference-data item", command.Id);
        item.Activate();
        audit.Record(AuditEventTypes.ReferenceDataItemReactivated, AuditTargetTypes.ReferenceDataItem, item.Id, after: new { item.Category, item.Code });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return item.ToDto();
    }
}
