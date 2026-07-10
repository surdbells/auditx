using System.Text.Json;
using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.SavedViews.Dtos;
using AuditX.Application.SavedViews.Mapping;
using AuditX.Domain.SavedViews;
using FluentValidation;

namespace AuditX.Application.SavedViews.Commands;

internal static class SavedViewValidation
{
    /// <summary>The payload must be a JSON object — it is applied straight into a screen's filter form.</summary>
    public static bool BeAJsonObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

// ---- Create ----

public sealed record CreateSavedViewCommand(string ViewKey, string Name, string ParametersJson, bool IsShared)
    : ICommand<SavedViewDto>;

public sealed class CreateSavedViewCommandValidator : AbstractValidator<CreateSavedViewCommand>
{
    public CreateSavedViewCommandValidator()
    {
        RuleFor(x => x.ViewKey).NotEmpty().MaximumLength(60);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.ParametersJson).NotEmpty().MaximumLength(8000)
            .Must(SavedViewValidation.BeAJsonObject).WithMessage("Parameters must be a JSON object.");
    }
}

public sealed class CreateSavedViewCommandHandler(
    ISavedViewRepository views, ICurrentUser currentUser, IUnitOfWork unitOfWork)
    : ICommandHandler<CreateSavedViewCommand, SavedViewDto>
{
    public async Task<SavedViewDto> Handle(CreateSavedViewCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException();
        var viewKey = command.ViewKey.Trim();
        var name = command.Name.Trim();
        if (await views.OwnedNameExistsAsync(userId, viewKey, name, null, cancellationToken))
        {
            throw new ConflictException("saved_view.duplicate_name", "You already have a saved view with this name on this screen.");
        }

        var view = SavedView.Create(userId, viewKey, name, command.ParametersJson, command.IsShared);
        views.Add(view);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return view.ToDto(userId);
    }
}

// ---- Update (owner only) ----

public sealed record UpdateSavedViewCommand(Guid Id, string Name, string ParametersJson, bool IsShared, string Version)
    : ICommand<SavedViewDto>;

public sealed class UpdateSavedViewCommandValidator : AbstractValidator<UpdateSavedViewCommand>
{
    public UpdateSavedViewCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.ParametersJson).NotEmpty().MaximumLength(8000)
            .Must(SavedViewValidation.BeAJsonObject).WithMessage("Parameters must be a JSON object.");
        RuleFor(x => x.Version).NotEmpty();
    }
}

public sealed class UpdateSavedViewCommandHandler(
    ISavedViewRepository views, ICurrentUser currentUser, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateSavedViewCommand, SavedViewDto>
{
    public async Task<SavedViewDto> Handle(UpdateSavedViewCommand command, CancellationToken cancellationToken)
    {
        var view = await views.GetByIdAsync(command.Id, cancellationToken) ?? throw new NotFoundException("Saved view", command.Id);
        SavedViewAccess.EnsureOwner(view, currentUser.UserId);
        view.EnsureVersion(command.Version);

        var name = command.Name.Trim();
        if (await views.OwnedNameExistsAsync(view.OwnerUserId, view.ViewKey, name, view.Id, cancellationToken))
        {
            throw new ConflictException("saved_view.duplicate_name", "You already have a saved view with this name on this screen.");
        }

        view.Update(name, command.ParametersJson, command.IsShared);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return view.ToDto(currentUser.UserId);
    }
}

// ---- Delete (owner only, soft) ----

public sealed record DeleteSavedViewCommand(Guid Id, string Version) : ICommand<Unit>;

public sealed class DeleteSavedViewCommandHandler(
    ISavedViewRepository views, ICurrentUser currentUser, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<DeleteSavedViewCommand, Unit>
{
    public async Task<Unit> Handle(DeleteSavedViewCommand command, CancellationToken cancellationToken)
    {
        var view = await views.GetByIdAsync(command.Id, cancellationToken) ?? throw new NotFoundException("Saved view", command.Id);
        SavedViewAccess.EnsureOwner(view, currentUser.UserId);
        view.EnsureVersion(command.Version);

        view.SoftDelete(currentUser.UserId, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
