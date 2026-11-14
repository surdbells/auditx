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
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0).WithMessage("The sort position cannot be negative.");
    }
}

internal static class ReferenceDataSortPosition
{
    /// <summary>
    /// Each position in a list holds one value, so the order of a dropdown is never ambiguous. Refuses a position
    /// another live value already holds, naming it.
    /// </summary>
    public static async Task EnsureFreeAsync(IReferenceDataRepository items, string category, int sortOrder, Guid? exceptId, CancellationToken cancellationToken)
    {
        if (await items.FindBySortOrderAsync(category, sortOrder, exceptId, cancellationToken) is { } taken)
        {
            throw new ConflictException("reference_data.sort_order_taken",
                $"Sort position {sortOrder} is already used by \"{taken.Label}\". Choose a different position.");
        }
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

        await ReferenceDataSortPosition.EnsureFreeAsync(items, category, command.SortOrder, null, cancellationToken);

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
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0).WithMessage("The sort position cannot be negative.");
    }
}

public sealed class UpdateReferenceDataItemCommandHandler(IReferenceDataRepository items, IAuditRecorder audit, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateReferenceDataItemCommand, ReferenceDataItemDto>
{
    public async Task<ReferenceDataItemDto> Handle(UpdateReferenceDataItemCommand command, CancellationToken cancellationToken)
    {
        var item = await items.GetByIdAsync(command.Id, cancellationToken) ?? throw new NotFoundException("Reference-data item", command.Id);
        if (item.SortOrder != command.SortOrder)
        {
            await ReferenceDataSortPosition.EnsureFreeAsync(items, item.Category, command.SortOrder, item.Id, cancellationToken);
        }

        var before = new { item.Label, item.Description, item.SortOrder };
        item.Update(command.Label, command.Description, command.SortOrder, clock.UtcNow);
        audit.Record(AuditEventTypes.ReferenceDataItemUpdated, AuditTargetTypes.ReferenceDataItem, item.Id,
            before: before, after: new { item.Label, item.Description, item.SortOrder });
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
