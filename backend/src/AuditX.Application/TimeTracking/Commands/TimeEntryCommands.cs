using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Authorization;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.Common.Exceptions;
using AuditX.Application.Common.Messaging;
using AuditX.Application.TimeTracking.Dtos;
using AuditX.Application.TimeTracking.Mapping;
using AuditX.Domain.AuditTrail;
using AuditX.Domain.TimeTracking;
using FluentValidation;

namespace AuditX.Application.TimeTracking.Commands;

// ---- Log time ----

public sealed record LogTimeCommand(
    Guid AuditId, DateOnly WorkDate, decimal Hours, string Category, Guid? ChecklistItemId, string? Notes)
    : ICommand<TimeEntryDto>;

public sealed class LogTimeCommandValidator : AbstractValidator<LogTimeCommand>
{
    public LogTimeCommandValidator()
    {
        RuleFor(x => x.AuditId).NotEmpty();
        RuleFor(x => x.Hours).GreaterThan(0).LessThanOrEqualTo(TimeEntry.MaxHoursPerEntry);
        RuleFor(x => x.Category).NotEmpty();
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}

public sealed class LogTimeCommandHandler(
    IAuditRepository audits,
    ITimeEntryRepository entries,
    IPermissionResolver permissions,
    ICurrentUser currentUser,
    IAuditRecorder audit,
    IUnitOfWork unitOfWork)
    : ICommandHandler<LogTimeCommand, TimeEntryDto>
{
    public async Task<TimeEntryDto> Handle(LogTimeCommand command, CancellationToken cancellationToken)
    {
        var auditEntity = await audits.GetByIdAsync(command.AuditId, cancellationToken)
            ?? throw new NotFoundException("Audit", command.AuditId);
        await TimeEntryAccess.EnsureCanLogAsync(auditEntity, currentUser.UserId, permissions, cancellationToken);
        var userId = currentUser.UserId ?? throw new UnauthorizedException();

        if (command.ChecklistItemId is { } itemId && auditEntity.ChecklistItems.All(i => i.Id != itemId))
        {
            throw new NotFoundException("Checklist item", itemId);
        }

        var category = TimeEntryParsing.ParseCategory(command.Category);
        var entry = TimeEntry.Log(command.AuditId, userId, command.WorkDate, command.Hours, category, command.ChecklistItemId, command.Notes);
        entries.Add(entry);

        audit.Record(AuditEventTypes.TimeLogged, AuditTargetTypes.TimeEntry, entry.Id,
            after: new { entry.AuditId, entry.UserId, entry.WorkDate, entry.Hours, category = category.ToString() });

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return entry.ToDto();
    }
}

// ---- Amend a time entry ----

public sealed record AmendTimeEntryCommand(
    Guid Id, DateOnly WorkDate, decimal Hours, string Category, Guid? ChecklistItemId, string? Notes, string Version)
    : ICommand<TimeEntryDto>;

public sealed class AmendTimeEntryCommandValidator : AbstractValidator<AmendTimeEntryCommand>
{
    public AmendTimeEntryCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Hours).GreaterThan(0).LessThanOrEqualTo(TimeEntry.MaxHoursPerEntry);
        RuleFor(x => x.Category).NotEmpty();
        RuleFor(x => x.Notes).MaximumLength(1000);
        RuleFor(x => x.Version).NotEmpty();
    }
}

public sealed class AmendTimeEntryCommandHandler(
    ITimeEntryRepository entries,
    IAuditRepository audits,
    IPermissionResolver permissions,
    ICurrentUser currentUser,
    IAuditRecorder audit,
    IUnitOfWork unitOfWork)
    : ICommandHandler<AmendTimeEntryCommand, TimeEntryDto>
{
    public async Task<TimeEntryDto> Handle(AmendTimeEntryCommand command, CancellationToken cancellationToken)
    {
        var entry = await entries.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException("Time entry", command.Id);
        var auditEntity = await audits.GetByIdAsync(entry.AuditId, cancellationToken)
            ?? throw new NotFoundException("Audit", entry.AuditId);
        await TimeEntryAccess.EnsureCanModifyAsync(entry, auditEntity, currentUser.UserId, permissions, cancellationToken);
        entry.EnsureVersion(command.Version);

        if (command.ChecklistItemId is { } itemId && auditEntity.ChecklistItems.All(i => i.Id != itemId))
        {
            throw new NotFoundException("Checklist item", itemId);
        }

        entry.Amend(command.WorkDate, command.Hours, TimeEntryParsing.ParseCategory(command.Category), command.ChecklistItemId, command.Notes);
        audit.Record(AuditEventTypes.TimeEntryAmended, AuditTargetTypes.TimeEntry, entry.Id,
            after: new { entry.WorkDate, entry.Hours, category = entry.Category.ToString() });

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return entry.ToDto();
    }
}

// ---- Delete a time entry ----

public sealed record DeleteTimeEntryCommand(Guid Id, string Version) : ICommand<Unit>;

public sealed class DeleteTimeEntryCommandHandler(
    ITimeEntryRepository entries,
    IAuditRepository audits,
    IPermissionResolver permissions,
    ICurrentUser currentUser,
    IAuditRecorder audit,
    IClock clock,
    IUnitOfWork unitOfWork)
    : ICommandHandler<DeleteTimeEntryCommand, Unit>
{
    public async Task<Unit> Handle(DeleteTimeEntryCommand command, CancellationToken cancellationToken)
    {
        var entry = await entries.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException("Time entry", command.Id);
        var auditEntity = await audits.GetByIdAsync(entry.AuditId, cancellationToken)
            ?? throw new NotFoundException("Audit", entry.AuditId);
        await TimeEntryAccess.EnsureCanModifyAsync(entry, auditEntity, currentUser.UserId, permissions, cancellationToken);
        entry.EnsureVersion(command.Version);

        entry.SoftDelete(currentUser.UserId, clock.UtcNow);
        audit.Record(AuditEventTypes.TimeEntryDeleted, AuditTargetTypes.TimeEntry, entry.Id,
            before: new { entry.AuditId, entry.Hours });

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
